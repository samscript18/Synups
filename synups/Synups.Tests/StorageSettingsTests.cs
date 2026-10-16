using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public sealed class StorageSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Synups-storage-ui-" + Guid.NewGuid());
    private string Database => Path.Combine(root, "history.db");
    private string Preferences => Path.Combine(root, "preferences.json");
    private PreferencesStore PreferenceStore => new(Preferences, Path.Combine(root, "logs"));
    public StorageSettingsTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private MainWindowViewModel CreateVm() => new(new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(Database)),
        new ConfigurationStore(Path.Combine(root, "config.json")), PreferenceStore);
    private async Task<Guid> SeedOldHistory()
    {
        await using var store = new SqliteHistoryStore(Database); await store.LoadAsync();
        var old = DateTimeOffset.UtcNow.AddDays(-60);
        var session = new SessionRecord(Guid.NewGuid(), old, old, old, TimeSpan.Zero, false, false);
        await store.CommitAsync(new([], [new(1, session.Id, null, old, MonitorEventKind.SessionStopped, "Old history")], session));
        return session.Id;
    }

    [AvaloniaFact]
    public async Task CapacitySettingsBindValidateAndPersistWithoutChangingPreferencesFile()
    {
        await PreferenceStore.SaveAsync(new() { AutomaticLogs = false, LogDirectory = root });
        var preferencesBefore = await File.ReadAllBytesAsync(Preferences, TestContext.Current.CancellationToken);
        var vm = CreateVm();
        try
        {
            await vm.InitializeAsync();
            var window = new MainWindow { DataContext = vm };
            Assert.Equal("1024", window.FindControl<TextBox>("CapacityEditor")!.Text);
            Assert.True(window.FindControl<Button>("ApplyCapacity")!.IsEnabled);
            Assert.Contains("WAL:", vm.StorageDetails); Assert.Contains("95%", vm.StorageDetails);
            foreach (var invalid in new[] { "255", "10241", "999999999999999999999", "1.5" })
            {
                vm.PrefCapacityMiB = invalid; await vm.SaveCapacityAsync();
                Assert.Equal(HistoryCapacity.DefaultBytes, vm.StorageUsage!.ConfiguredBytes);
                Assert.Contains("whole number", vm.Message);
            }
            vm.PrefCapacityMiB = "2048"; await vm.SaveCapacityAsync();
            Assert.Equal(2048 * HistoryCapacity.MiB, vm.StorageUsage!.EffectiveLimitBytes);
            Assert.Equal(preferencesBefore, await File.ReadAllBytesAsync(Preferences, TestContext.Current.CancellationToken));
        }
        finally { await vm.DisposeAsync(); }
        var reopened = CreateVm();
        try { await reopened.InitializeAsync(); Assert.Equal("2048", reopened.PrefCapacityMiB); }
        finally { await reopened.DisposeAsync(); }
    }

    [AvaloniaFact]
    public async Task KeepAllIsDefaultAndCleanupAndRetentionRequireConfirmation()
    {
        var id = await SeedOldHistory(); var vm = CreateVm();
        try
        {
            await vm.InitializeAsync(); Assert.Equal("0", vm.PrefRetentionDays); Assert.Single(vm.Activity);
            await vm.CleanupHistoryAsync(); Assert.Single(vm.Activity); Assert.Contains("Confirm", vm.Message);
            await vm.BackupHistoryAsync(); Assert.Contains("Confirm", vm.Message);
            Assert.False(Directory.Exists(Database + ".backups"));
            vm.PrefRetentionDays = "30"; await vm.SavePreferencesAsync();
            Assert.Equal(0, vm.SavedPreferences.RetentionDays); Assert.Single(vm.Activity);
            await vm.CleanupHistoryAsync(confirmed: true);
            Assert.Empty(vm.Activity); Assert.Equal(0, vm.SavedPreferences.RetentionDays);
            var backup = Assert.Single(Directory.GetFiles(Database + ".backups", "*.db"));
            await using var read = new SqliteHistoryStore(backup);
            Assert.Equal(id, Assert.Single((await read.LoadAsync(true)).Sessions).Id);
        }
        finally { await vm.DisposeAsync(); }
    }

    [AvaloniaFact]
    public async Task BackupFailureCancelsCleanupAndPreservesAllRecords()
    {
        var id = await SeedOldHistory();
        await File.WriteAllTextAsync(Database + ".backups", "existing unrelated file", TestContext.Current.CancellationToken);
        var vm = CreateVm();
        try
        {
            await vm.InitializeAsync(); await vm.CleanupHistoryAsync(confirmed: true);
            Assert.Single(vm.Activity); Assert.DoesNotContain("cleaned", vm.Message);
            Assert.Equal(id, Assert.Single(vm.Activity).SessionId);
        }
        finally { await vm.DisposeAsync(); }
        await using var reopened = new SqliteHistoryStore(Database);
        Assert.Equal(id, Assert.Single((await reopened.LoadAsync()).Sessions).Id);
    }

    [AvaloniaFact]
    public async Task ConfirmedAutomaticRetentionPersists()
    {
        await SeedOldHistory(); var vm = CreateVm();
        try
        {
            await vm.InitializeAsync(); vm.PrefRetentionDays = "30";
            await vm.SavePreferencesAsync(confirmRetention: true);
            Assert.Empty(vm.Activity); Assert.Equal(30, (await PreferenceStore.LoadAsync()).Preferences.RetentionDays);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task StopImmediatelyRefreshesUsageAfterAutomaticLogsAreWritten()
    {
        var config = new ConfigurationStore(Path.Combine(root, "config.json")); await config.SaveAsync(Config(Group()));
        var vm = CreateVm();
        try
        {
            await vm.InitializeAsync(); Assert.Equal(0, vm.StorageUsage!.DerivedLogBytes);
            await vm.StartAsync(); await vm.StopAsync();
            Assert.True(vm.StorageUsage!.DerivedLogBytes > 0);
            Assert.Equal(Directory.GetFiles(Path.Combine(root, "logs")).Sum(p => new FileInfo(p).Length), vm.StorageUsage.DerivedLogBytes);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task OptInRetentionRunsDailyDuringMonitoringAndKeepsTheActiveSession()
    {
        var disk = new SqliteHistoryStore(Database);
        await disk.LoadAsync(); var clock = new ManualClock();
        var old = clock.UtcNow.AddHours(-1);
        var prior = new SessionRecord(Guid.NewGuid(), old, old, old, TimeSpan.Zero, false, false);
        await disk.CommitAsync(new([], [new(1, prior.Id, null, old, MonitorEventKind.SessionStopped, "Prior session")], prior));
        await using var engine = new MonitoringService(new ScriptedProbe(), clock, history: disk);
        await engine.InitializeHistoryAsync(); await engine.SetAutomaticRetentionAsync(1);
        Assert.Contains(engine.Capture().Sessions!, s => s.Id == prior.Id);
        await engine.StartAsync(Config(Group())); await UntilAsync(() => clock.PendingDelays == 1);
        var active = engine.Capture().SessionId;
        clock.Advance(86_400_000);
        await UntilAsync(() => !engine.Capture().Sessions!.Any(s => s.Id == prior.Id));
        Assert.Contains(engine.Capture().Sessions!, s => s.Id == active && s.EndedUtc is null);
        Assert.Equal(SessionState.Running, engine.Capture().State);
        await engine.StopAsync(); Assert.False(engine.HasUnsavedEvidence);
    }
}
