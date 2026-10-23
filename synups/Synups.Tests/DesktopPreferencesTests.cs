using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Synups.Core.Alerts;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Synups.Desktop.Integrations;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

[Collection(StorageCollection.Name)]
public class DesktopPreferencesTests
{
    [Fact]
    public void AlertsRespectPreferencesDeduplicateAndDoNotReplayHistory()
    {
        var session = Guid.NewGuid(); var group = Guid.NewGuid(); var utc = DateTimeOffset.UtcNow;
        MonitorEvent Event(long seq, MonitorEventKind kind) => new(seq, session, group, utc, kind, "Actual transition");
        var events = new[] { Event(1, MonitorEventKind.OutageStarted), Event(2, MonitorEventKind.Recovery) };
        var policy = new AlertPolicy();
        Assert.Single(policy.Select(events, new() { NotifyRecovery = false }));
        Assert.Empty(policy.Select(events, new())); // Enabling a preference cannot replay old transitions.
        policy.IgnoreExisting(events);
        Assert.Empty(policy.Select(events, new()));
        var later = Event(3, MonitorEventKind.Recovery);
        Assert.Equal(later.Id, Assert.Single(policy.Select(events.Append(later), new())).EventId);
        Assert.Empty(policy.Select(events.Append(later), new()));
    }

    [Fact]
    public void ErrorAlertsAreRateLimitedUsingMonotonicTime()
    {
        var clock = new ManualClock(); var policy = new AlertPolicy(clock); var group = Guid.NewGuid(); var session = Guid.NewGuid();
        MonitorEvent ErrorEvent(long seq) => new(seq, session, group, clock.UtcNow, MonitorEventKind.ProbeError, "Permission denied");
        Assert.Single(policy.Select([ErrorEvent(1)], new()));
        clock.JumpUtc(TimeSpan.FromDays(1));
        Assert.Empty(policy.Select([ErrorEvent(2)], new()));
        clock.Advance(60_000);
        Assert.Single(policy.Select([ErrorEvent(3)], new()));
        Assert.Empty(policy.Select([ErrorEvent(4)], new() { NotifyErrors = false }));
    }

    [Fact]
    public async Task PreferencesRoundtripAndCorruptionRecoveryPreserveOriginalBytes()
    {
        var root = Temp(); var path = Path.Combine(root, "prefs.json"); var store = new PreferencesStore(path);
        try
        {
            var defaults = (await store.LoadAsync()).Preferences;
            Assert.False(defaults.MonitorOnLaunch); Assert.False(defaults.LaunchMinimized); Assert.False(defaults.NativeNotifications);
            var expected = defaults with { MonitorOnLaunch = true, ConfirmExit = true, AlwaysOnTop = true, Placement = new(900, 600, 1, 2, true) };
            await store.SaveAsync(expected);
            Assert.Equal(expected, (await new PreferencesStore(path).LoadAsync()).Preferences);
            await File.WriteAllTextAsync(path, "{bad", TestContext.Current.CancellationToken);
            var corrupt = await store.LoadAsync(); Assert.NotNull(corrupt.Error); Assert.False(corrupt.Preferences.MonitorOnLaunch);
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(defaults));
            Assert.Equal("{bad", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            await store.SaveAsync(defaults, recover: true);
            Assert.Equal("{bad", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(root, "*.bak")), TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DesktopPolicyPreventsHiddenUnrestorableWindowAndClampsPlacement()
    {
        var p = new AppPreferences { MinimizeToTray = true, MonitorOnLaunch = true };
        Assert.False(DesktopPolicy.HideOnMinimize(p, restorableTray: false));
        Assert.True(DesktopPolicy.HideOnMinimize(p, restorableTray: true));
        Assert.False(DesktopPolicy.StartOnLaunch(p, validProfileAndHistory: false));
        Assert.False(DesktopPolicy.StartOnLaunch(new(), validProfileAndHistory: true));
        var safe = DesktopPolicy.Clamp(new(3000, 2000, -40000, 6000, true), [new(100, 100, 1920, 1080, 2)]);
        Assert.Equal(960, safe.Width); Assert.Equal(540, safe.Height); Assert.Equal(100, safe.X); Assert.Equal(100, safe.Y);
        Assert.True(safe.Maximized);
        Assert.Throws<ArgumentException>(() => new AppPreferences { Placement = new(double.NaN, 800, 0, 0, false) }.Validate());
        Assert.Throws<ArgumentException>(() => new AppPreferences { RetentionDays = -1 }.Validate());
        Assert.Throws<ArgumentException>(() => new AppPreferences { LogDirectory = "relative" }.Validate());
    }

    [Fact]
    public async Task NativeDeliveryOwnsCancellationAndReportsAdapterFailure()
    {
        var sink = new BlockingSink(); var delivery = new AlertDelivery(sink);
        delivery.Enqueue(new(Guid.NewGuid(), "Test-only", "No OS notification", MonitorEventKind.OutageStarted));
        await sink.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await delivery.DisposeAsync(); Assert.True(sink.Drained);
        await using var failed = new AlertDelivery(new FailedSink());
        failed.Enqueue(new(Guid.NewGuid(), "Test-only", "Failure", MonitorEventKind.ProbeError));
        await UntilAsync(() => failed.Status.Contains("denied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogFailureDoesNotDamageHistoryAndMissingCsvIsRebuilt()
    {
        var root = Temp();
        try
        {
            var session = new SessionRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, TimeSpan.Zero, false, false);
            var record = new MonitorEvent(1, session.Id, null, DateTimeOffset.UtcNow, MonitorEventKind.SessionStopped, "Session ended");
            var archive = new HistoryState([], [record], [session], 1);
            var blocked = Path.Combine(root, "blocked"); await File.WriteAllTextAsync(blocked, "file", TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<IOException>(() => AutomaticLogExporter.WriteAsync(blocked, archive));
            var logs = Path.Combine(root, "logs"); await AutomaticLogExporter.WriteAsync(logs, archive);
            var csv = Assert.Single(Directory.GetFiles(logs, "*.csv")); File.Delete(csv);
            await AutomaticLogExporter.WriteAsync(logs, archive); Assert.True(File.Exists(csv));
            var json = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(logs, "*.json")), TestContext.Current.CancellationToken);
            Assert.Contains("Session ended", json); Assert.Single(archive.Events);
        }
        finally { Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task SavedAutoStartTopmostAndExitConfirmationAreAppliedByWindow()
    {
        var root = Temp(); var path = Path.Combine(root, "config.json"); var prefs = new PreferencesStore(Path.Combine(root, "prefs.json"));
        var store = new ConfigurationStore(path); await store.SaveAsync(Config(Group()));
        await prefs.SaveAsync(new() { MonitorOnLaunch = true, AlwaysOnTop = true, ConfirmExit = true, AutomaticLogs = false, LogDirectory = root });
        var probe = new ScriptedProbe(); var service = new MonitoringService(probe);
        var vm = new MainWindowViewModel(service, store, prefs); var window = new MainWindow { DataContext = vm, ExitConfirmation = () => Task.FromResult(false) };
        try
        {
            window.Show(); await UntilAsync(() => vm.IsRunning && !probe.Addresses.IsEmpty);
            Assert.True(window.Topmost); Assert.True(window.IsVisible);
            window.Hide(); // A tray Exit must make its confirmation owner reachable.
            window.ExitConfirmation = () => { Assert.True(window.IsVisible); return Task.FromResult(false); };
            window.Close(); await Task.Delay(30, TestContext.Current.CancellationToken);
            Assert.True(window.IsVisible); Assert.True(vm.IsRunning);
            window.ExitConfirmation = () => Task.FromResult(true); window.Close(); await UntilAsync(() => !window.IsVisible);
            Assert.Equal(SessionState.Stopped, service.Capture().State);
            Assert.NotNull((await prefs.LoadAsync()).Preferences.Placement);
        }
        finally { window.ExitConfirmation = () => Task.FromResult(true); await vm.DisposeAsync(); window.Close(); Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task CorruptPreferencesNeverAutoStartOrGetOverwrittenByPlacement()
    {
        var root = Temp(); var path = Path.Combine(root, "config.json"); var prefsPath = Path.Combine(root, "prefs.json");
        var store = new ConfigurationStore(path); await store.SaveAsync(Config(Group()));
        await File.WriteAllTextAsync(prefsPath, "bad", TestContext.Current.CancellationToken);
        var probe = new ScriptedProbe(); var vm = new MainWindowViewModel(new MonitoringService(probe), store, new PreferencesStore(prefsPath, root));
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.CanStart); Assert.Empty(probe.Addresses);
            Assert.NotNull(vm.PreferencesError);
            window.Close(); await UntilAsync(() => !window.IsVisible);
            Assert.Equal("bad", await File.ReadAllTextAsync(prefsPath, TestContext.Current.CancellationToken));
        }
        finally { await vm.DisposeAsync(); window.Close(); Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public async Task CorruptHistoryBlocksMonitoringPreservesEvidenceAndAllowsSafeClose()
    {
        var root = Temp(); var path = Path.Combine(root, "history.db");
        await File.WriteAllTextAsync(path, "corrupt evidence", TestContext.Current.CancellationToken);
        var config = new ConfigurationStore(Path.Combine(root, "config.json")); await config.SaveAsync(Config(Group()));
        var engine = new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(path));
        var vm = new MainWindowViewModel(engine, config); var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.HistoryStatus.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
            Assert.False(vm.CanStart); Assert.Empty(vm.Outages);
            window.Close(); await UntilAsync(() => !window.IsVisible);
            Assert.Equal("corrupt evidence", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Contains("unavailable", vm.HistoryStatus, StringComparison.OrdinalIgnoreCase);
        }
        finally { await vm.DisposeAsync(); window.Close(); Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task LogDirectoryFailureKeepsWindowOpenUntilUserChoosesFallback()
    {
        var root = Temp(); var path = Path.Combine(root, "config.json"); var blocked = Path.Combine(root, "blocked");
        await File.WriteAllTextAsync(blocked, "existing file", TestContext.Current.CancellationToken);
        var config = new ConfigurationStore(path); await config.SaveAsync(Config(Group()));
        var preferences = new PreferencesStore(Path.Combine(root, "prefs.json")); await preferences.SaveAsync(new() { LogDirectory = blocked });
        var engine = new MonitoringService(new ScriptedProbe()); var vm = new MainWindowViewModel(engine, config, preferences);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.CanStart); await vm.StartAsync();
            window.Close(); await UntilAsync(() => vm.Message.Contains("Could not finish shutdown", StringComparison.Ordinal));
            Assert.True(window.IsVisible); Assert.Equal(SessionState.Stopped, engine.Capture().State);
            vm.PrefLogs = false; await vm.SavePreferencesAsync(); // Explicit user decision; durable source is unaffected.
            window.Close(); await UntilAsync(() => !window.IsVisible);
        }
        finally { vm.Refresh(); vm.PrefLogs = false; await vm.SavePreferencesAsync(); await vm.DisposeAsync(); window.Close(); await UntilAsync(() => !window.IsVisible); Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task RetentionRemovesMiddleActivityWithoutRemovingUnresolvedEvidence()
    {
        var root = Temp(); var path = Path.Combine(root, "history.db"); var utc = DateTimeOffset.UtcNow;
        var old = utc.AddDays(-30); var keepId = Guid.NewGuid();
        var first = new SessionRecord(Guid.NewGuid(), old, old.AddMinutes(1), old.AddMinutes(1), TimeSpan.FromMinutes(1), false, false);
        var second = first with { Id = Guid.NewGuid() };
        var recent = first with { Id = Guid.NewGuid(), StartedUtc = utc, EndedUtc = utc };
        var incident = new OutageRecord(keepId, first.Id, Guid.NewGuid(), "Unresolved", old, old, null, old.AddMinutes(1), TimeSpan.FromMinutes(1), false, null);
        await using (var seed = new SqliteHistoryStore(path))
        {
            await seed.LoadAsync();
            await seed.CommitAsync(new([incident], [new(1, first.Id, null, old, MonitorEventKind.SessionStopped, "Kept")], first));
            await seed.CommitAsync(new([], [new(2, second.Id, null, old, MonitorEventKind.SessionStopped, "Eligible")], second));
            await seed.CommitAsync(new([], [new(3, recent.Id, null, utc, MonitorEventKind.SessionStopped, "Recent")], recent));
        }
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(path)), new ConfigurationStore(Path.Combine(root, "config.json")));
        try
        {
            await vm.InitializeAsync(); Assert.Equal(3, vm.Activity.Count);
            vm.PrefRetentionDays = "1"; await vm.SavePreferencesAsync(confirmRetention: true);
            Assert.Equal(new long[] { 1, 3 }, vm.Activity.Select(e => e.Sequence));
            Assert.Equal(keepId, Assert.Single(vm.Outages).Id);
        }
        finally { await vm.DisposeAsync(); Directory.Delete(root, true); }
    }

    private static string Temp() { var p = Path.Combine(Path.GetTempPath(), "Synups-desktop-" + Guid.NewGuid()); Directory.CreateDirectory(p); return p; }
    private sealed class BlockingSink : INativeAlertSink
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Drained { get; private set; }
        public async Task<NativeAlertResult> SendAsync(AlertMessage message, CancellationToken token)
        { Started.SetResult(); try { await Task.Delay(Timeout.Infinite, token); return new(true, "Unexpected"); } finally { Drained = true; } }
    }
    private sealed class FailedSink : INativeAlertSink
    {
        public Task<NativeAlertResult> SendAsync(AlertMessage message, CancellationToken token) => Task.FromResult(new NativeAlertResult(false, "Permission denied; in-app fallback available."));
    }
}
