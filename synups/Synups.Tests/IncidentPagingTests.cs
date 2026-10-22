using System.Text;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Synups.Tests;

[Collection(StorageCollection.Name)]
public class IncidentPagingTests
{
    [Fact]
    public async Task MalformedOldIncidentPageFailsWithoutResetOrEvidenceLoss()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-page-corrupt-"+Guid.NewGuid(), "history.db"); var data = Fixture(250);
        try
        {
            await using var store = new SqliteHistoryStore(path); await store.LoadAsync(); await store.CommitAsync(new(data.Rows, [], data.Session));
            await using (var edit = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource=path, Pooling=false }.ToString()))
            {
                await edit.OpenAsync(TestContext.Current.CancellationToken); using var command = edit.CreateCommand();
                command.CommandText="UPDATE outages SET payload='{bad' WHERE id=$id"; command.Parameters.AddWithValue("$id",data.Rows[0].Id.ToString()); command.ExecuteNonQuery();
            }
            var bytes = await ReadDatabaseBytesAsync(path);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => store.ReadIncidentPageAsync(new(50, OldestFirst:true)));
            Assert.Equal(bytes, await ReadDatabaseBytesAsync(path));
            Assert.Equal(50,(await store.ReadIncidentPageAsync(new(50))).Items.Count);
            var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe(), history: store), new ConfigurationStore(Path.Combine(Path.GetDirectoryName(path)!,"config.json")));
            try
            {
                await vm.InitializeAsync(); vm.PrefLogs = false; await vm.SavePreferencesAsync();
                await using var csv = new MemoryStream(); await vm.ExportAsync(csv);
                Assert.Contains("destination may be partial", vm.Message); Assert.Contains("Source history is preserved", vm.Message);
                Assert.Equal(bytes, await ReadDatabaseBytesAsync(path));
            }
            finally { await vm.DisposeAsync(); }
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!,true); }
    }
    // SQLite retains an open read/write handle. Permit that handle on Windows while
    // comparing bytes at quiescent boundaries; these reads do not mutate the store.
    private static async Task<byte[]> ReadDatabaseBytesAsync(string path)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        await using var copy = new MemoryStream();
        await input.CopyToAsync(copy, TestContext.Current.CancellationToken);
        return copy.ToArray();
    }

    private static (SessionRecord Session, OutageRecord[] Rows) Fixture(int count, bool interrupted = false)
    {
        var utc = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero); var id = Guid.NewGuid();
        var session = new SessionRecord(id, utc, interrupted ? null : utc.AddHours(1), utc.AddMinutes(50), TimeSpan.FromHours(1), false, false);
        var rows = Enumerable.Range(0, count).Select(i => new OutageRecord(Guid.NewGuid(), id, Guid.NewGuid(), "Fixture group " + i,
            utc.AddSeconds(i), utc.AddSeconds(i - 1), interrupted && i == 0 ? null : utc.AddSeconds(i + 1), null,
            TimeSpan.FromSeconds(1), false, null, "Fixture, \"quoted\" comment")).ToArray();
        return (session, rows);
    }

    [Fact]
    public async Task PagesAreBoundedUniqueAndStableAcrossUpdatesAndNewInsertions()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-pages-"+Guid.NewGuid(), "history.db");
        try
        {
            await using var store = new SqliteHistoryStore(path); await store.LoadAsync();
            var data = Fixture(620); await store.CommitAsync(new(data.Rows, [], data.Session));
            Assert.Equal(200, (await store.LoadAsync()).Outages.Count);
            var first = await store.ReadIncidentPageAsync(new(50)); Assert.Equal(50, first.Items.Count);
            Assert.Equal(data.Rows[^1].Id, first.Items[0].Incident.Id);
            var inserted = Fixture(1); await store.CommitAsync(new(inserted.Rows, [], inserted.Session));
            var changed = data.Rows[0] with { Comment = "Changed without moving the cursor" };
            await store.CommitAsync(new([changed], [], data.Session));
            var seen = first.Items.Select(i => i.Incident.Id).ToList(); var cursor = first.NextCursor;
            while (cursor is not null)
            {
                var page = await store.ReadIncidentPageAsync(new(50, cursor, UpperBound: first.UpperBound));
                Assert.InRange(page.Items.Count, 1, 50); seen.AddRange(page.Items.Select(i => i.Incident.Id)); cursor = page.NextCursor;
            }
            Assert.Equal(620, seen.Count); Assert.Equal(seen.Count, seen.Distinct().Count());
            Assert.DoesNotContain(inserted.Rows[0].Id, seen); Assert.Equal(data.Rows[0].Id, seen[^1]);
            Assert.Equal(changed.Comment, (await store.FindIncidentAsync(changed.Id))!.Comment);
            Assert.Equal(621, (await store.LoadAsync(includeAllEvents: true)).Outages.Count);
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadIncidentPageAsync(new(101)));
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadIncidentPageAsync(new(50, -1)));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task RestartRecoversOldUnresolvedIncidentOutsideTheRecentCacheWithoutFalseRecovery()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-old-active-"+Guid.NewGuid(), "history.db"); var data = Fixture(620, interrupted: true);
        try
        {
            await using (var seed = new SqliteHistoryStore(path)) { await seed.LoadAsync(); await seed.CommitAsync(new(data.Rows, [], data.Session)); }
            await using (var store = new SqliteHistoryStore(path))
            {
                Assert.Equal(200, (await store.LoadAsync()).Outages.Count);
                var first = Assert.Single((await store.ReadIncidentPageAsync(new(1, OldestFirst: true))).Items);
                Assert.True(first.SessionInterrupted); Assert.Null(first.Incident.RecoveredUtc);
                Assert.Equal(data.Session.LastObservedUtc, first.Incident.ObservationEndedUtc); Assert.True(first.Incident.ContainsObservationGap);
                var events = (await store.LoadAsync(true)).Events; Assert.Single(events); Assert.Equal(MonitorEventKind.Interrupted, events[0].Kind);
            }
            await using var reopened = new SqliteHistoryStore(path);
            Assert.Single((await reopened.LoadAsync(true)).Events);
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task EngineCacheIsBoundedWhileOldCommentAndFullStreamingCsvRemainAvailable()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-streamed-history-"+Guid.NewGuid(), "history.db"); var data = Fixture(2000);
        try
        {
            await using (var seed = new SqliteHistoryStore(path)) { await seed.LoadAsync(); await seed.CommitAsync(new(data.Rows, [], data.Session)); }
            await using var engine = new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(path));
            await engine.InitializeHistoryAsync(); Assert.Equal(200, engine.Capture().Outages.Count);
            await engine.UpdateCommentAsync(data.Rows[0].Id, "Old, \"durable\" comment");
            Assert.InRange(engine.Capture().Outages.Count, 1, 200);
            var oldest = Assert.Single((await engine.ReadIncidentPageAsync(new(1, OldestFirst: true))).Items);
            Assert.Equal("Old, \"durable\" comment", oldest.Incident.Comment);
            await using var csv = new MemoryStream(); await engine.ExportIncidentsAsync(csv);
            var text = Encoding.UTF8.GetString(csv.ToArray());
            Assert.Equal(2001, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Contains(data.Rows[0].Id.ToString(), text); Assert.Contains(data.Rows[^1].Id.ToString(), text);
            Assert.Contains("\"Old, \"\"durable\"\" comment\"", text);
            Assert.True(text.IndexOf(data.Rows[0].Id.ToString(), StringComparison.Ordinal) < text.IndexOf(data.Rows[^1].Id.ToString(), StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void TimelineDistinguishesRecoveryStopInterruptionAndExactUtc()
    {
        var record = Fixture(1).Rows[0]; var row = new OutageRow(record);
        Assert.True(row.IsRecovered); Assert.Contains("Observed recovery", row.State); Assert.Contains(record.StartedUtc.ToString("O"), row.Details);
        row.Apply(record with { RecoveredUtc = null }); Assert.True(row.IsConfirmed);
        row.Apply(record with { RecoveredUtc = null, ObservationEndedUtc = record.StartedUtc }); Assert.True(row.IsEndedUnresolved); Assert.Contains("observation ended", row.State);
        row.Apply(record with { RecoveredUtc = null, ObservationEndedUtc = record.StartedUtc, ContainsObservationGap = true }, true);
        Assert.Contains("interrupted", row.State); Assert.Contains("Recovery UTC: Not observed", row.Details);
    }

    [AvaloniaFact]
    public async Task NativePresentationPagesRealSqliteAndKeepsExistingRowsOnRefresh()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-timeline-ui-"+Guid.NewGuid()); var path = Path.Combine(directory, "history.db"); var data = Fixture(120);
        try
        {
            await using (var seed = new SqliteHistoryStore(path)) { await seed.LoadAsync(); await seed.CommitAsync(new(data.Rows, [], data.Session)); }
            var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(path)), new ConfigurationStore(Path.Combine(directory, "configuration.json")));
            try
            {
                await vm.InitializeAsync(); Assert.Equal(50, vm.Outages.Count); var row = vm.Outages[0];
                await vm.LatestIncidentsAsync(); Assert.Same(row, vm.Outages[0]);
                await vm.OlderIncidentsAsync(); Assert.Equal(50, vm.Outages.Count); Assert.DoesNotContain(vm.Outages, o => o.Id == row.Id);
                await vm.OlderIncidentsAsync(); Assert.Equal(20, vm.Outages.Count); Assert.False(vm.CanReadOlderIncidents);
                await vm.LatestIncidentsAsync(); Assert.Equal(data.Rows[^1].Id, vm.Outages[0].Id);
            }
            finally { await vm.DisposeAsync(); }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
