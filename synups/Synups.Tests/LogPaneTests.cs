using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class LogPaneTests
{
    private static HistoryState Archive(Guid id) => new([], [], [new SessionRecord(id, DateTimeOffset.Parse("2026-10-22T12:00:00+02:00", CultureInfo.InvariantCulture), DateTimeOffset.UtcNow, null, TimeSpan.FromSeconds(1), false, false)], 0);
    [Fact]
    public async Task LocalUtcFixedAndStableCopiesPreserveExistingSessionFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-log-styles-" + Guid.NewGuid()); var id = Guid.NewGuid(); var archive = Archive(id);
        try
        {
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.StableSessionId);
            var original = await File.ReadAllBytesAsync(Path.Combine(root, $"Synups-{id}.json"), TestContext.Current.CancellationToken);
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.UtcSessionStart);
            Assert.True(File.Exists(Path.Combine(root, $"Synups-20261022T100000Z-{id}.json")));
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.LocalSessionStart);
            var local = archive.Sessions[0].StartedUtc.ToLocalTime().ToString("yyyyMMddTHHmmsszzz", CultureInfo.InvariantCulture).Replace(":", "");
            Assert.True(File.Exists(Path.Combine(root, $"Synups-{local}-{id}.json")));
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.FixedArchiveSnapshot);
            var fixedBefore = await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.json"), TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(fixedBefore); Assert.Equal(id, Assert.Single(json.RootElement.GetProperty("Sessions").EnumerateArray()).GetProperty("Id").GetGuid());
            Assert.Empty(json.RootElement.GetProperty("Outages").EnumerateArray());
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.FixedArchiveSnapshot);
            Assert.Equal(fixedBefore, await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.json"), TestContext.Current.CancellationToken));
            Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(root, $"Synups-{id}.json"), TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task OversizedFixedSnapshotPreservesOldFilesAndCleansTemporaryWrites()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-fixed-bound-" + Guid.NewGuid()); var id = Guid.NewGuid(); var archive = Archive(id);
        try
        {
            await AutomaticLogExporter.WriteAsync(root, archive, LogFilenameStyle.FixedArchiveSnapshot);
            var before = await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.json"), TestContext.Current.CancellationToken);
            var csvBefore = await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.csv"), TestContext.Current.CancellationToken);
            // Test-only records stress the write bound; never included in application storage or packages.
            var comment = new string('x', 1000); var utc = DateTimeOffset.UtcNow; var groupId = Guid.NewGuid();
            var rows = Enumerable.Range(0, 16000).Select(i => new OutageRecord(Guid.NewGuid(), id, groupId, "Bound test", utc.AddSeconds(i * 2), utc.AddSeconds(i * 2 - 1), utc.AddSeconds(i * 2 + 1), null, TimeSpan.FromSeconds(1), false, null, comment)).ToArray();
            await Assert.ThrowsAsync<IOException>(() => AutomaticLogExporter.WriteAsync(root, archive with { Outages = rows }, LogFilenameStyle.FixedArchiveSnapshot));
            Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.json"), TestContext.Current.CancellationToken));
            Assert.Equal(csvBefore, await File.ReadAllBytesAsync(Path.Combine(root, "Synups-fixed.csv"), TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(root, "*.tmp")); Assert.True(File.Exists(Path.Combine(root, $"Synups-{id}.json")));
        }
        finally { Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public async Task PaneAndNamingPreferencesApplyAndReloadWithoutChangingMonitoringConfiguration()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-pane-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var prefs = new PreferencesStore(Path.Combine(root, "prefs.json")); var store = new ConfigurationStore(Path.Combine(root, "config.json"));
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe()), store, prefs); var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.CanEdit);
            vm.PrefGroupHeight = 180; vm.PrefTargetHeight = 320; vm.PrefIncidentHeight = 380;
            vm.PrefLogName = vm.LogNameChoices.Single(c => c.Style == LogFilenameStyle.LocalSessionStart); await vm.SavePreferencesAsync();
            Assert.Equal(180, window.FindControl<ListBox>("GroupList")!.MaxHeight);
            Assert.Equal(320, window.FindControl<ListBox>("TargetList")!.MaxHeight);
            Assert.Equal(380, window.FindControl<ListBox>("IncidentTimeline")!.MaxHeight);
            var loaded = (await new PreferencesStore(Path.Combine(root, "prefs.json")).LoadAsync()).Preferences;
            Assert.Equal(180, loaded.GroupPaneHeight); Assert.Equal(LogFilenameStyle.LocalSessionStart, loaded.LogNaming);
            Assert.False(vm.IsRunning); Assert.False(File.Exists(store.FileName));
            Assert.Throws<ArgumentException>(() => (loaded with { TargetPaneHeight = double.NaN }).Validate());
            Assert.Throws<ArgumentException>(() => (loaded with { LogNaming = (LogFilenameStyle)999 }).Validate());
        }
        finally { window.Close(); await UntilAsync(() => !window.IsVisible); await vm.DisposeAsync(); Directory.Delete(root, true); }
    }
}
