using Microsoft.Data.Sqlite;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class HistoryTests
{
    [Fact]
    public async Task EngineHistoryCommentsAndIdsSurviveNewServiceAndStore()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        Guid id; Guid[] eventIds;
        try
        {
            var clock = new ManualClock();
            await using (var engine = new MonitoringService(new ScriptedProbe(Fail, Fail, Ok), clock, history: new SqliteHistoryStore(path)))
            {
                await engine.StartAsync(Config(Group())); await UntilAsync(() => clock.PendingDelays == 1);
                clock.Advance(1000); await UntilAsync(() => clock.PendingDelays == 1);
                id = Assert.Single(engine.Capture().Outages).Id;
                await engine.UpdateCommentAsync(id, "Persistent, \"quoted\"\ncomment");
                clock.Advance(1000); await UntilAsync(() => engine.Capture().Outages[0].RecoveredUtc.HasValue);
                await engine.StopAsync();
                eventIds = engine.Capture().Events.Select(e => e.Id).ToArray();
                var archive = await engine.ReadArchiveAsync();
                await AutomaticLogExporter.WriteAsync(Path.Combine(directory, "logs"), archive);
                Assert.Single(Directory.GetFiles(Path.Combine(directory, "logs"), "*.json"));
                Assert.Single(Directory.GetFiles(Path.Combine(directory, "logs"), "*.csv"));
                Assert.Contains("Persistent", await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(directory, "logs"), "*.csv")[0], TestContext.Current.CancellationToken));
            }
            await using var restarted = new MonitoringService(new ScriptedProbe(), history: new SqliteHistoryStore(path));
            await restarted.InitializeHistoryAsync();
            var restored = Assert.Single(restarted.Capture().Outages);
            Assert.Equal(id, restored.Id); Assert.Contains("Persistent", restored.Comment); Assert.NotNull(restored.RecoveredUtc);
            Assert.Equal(eventIds, restarted.Capture().Events.Select(e => e.Id));
            Assert.Equal(SessionState.Stopped, restarted.Capture().State);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InterruptedSessionKeepsLastObservationWithoutFalseRecoveryAndIsIdempotent()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        try
        {
            var utc = DateTimeOffset.UtcNow; var session = new SessionRecord(Guid.NewGuid(), utc.AddMinutes(-1), null, utc, TimeSpan.FromMinutes(1), false, false);
            var outage = new OutageRecord(Guid.NewGuid(), session.Id, Guid.NewGuid(), "Router", utc.AddSeconds(-10), utc.AddSeconds(-20), null, null, TimeSpan.FromSeconds(10), false, null, "Keep");
            await using (var store = new SqliteHistoryStore(path))
            {
                await store.LoadAsync(); await store.CommitAsync(new([outage], [], session));
            }
            Guid interruptionId;
            await using (var restarted = new SqliteHistoryStore(path))
            {
                var state = await restarted.LoadAsync(); var row = Assert.Single(state.Outages);
                Assert.Null(row.RecoveredUtc); Assert.Equal(utc, row.ObservationEndedUtc); Assert.Equal(TimeSpan.FromSeconds(10), row.Elapsed);
                Assert.True(row.ContainsObservationGap); Assert.True(Assert.Single(state.Sessions).Interrupted);
                interruptionId = Assert.Single(state.Events).Id;
                Assert.DoesNotContain(state.Events, e => e.Kind == MonitorEventKind.Recovery);
            }
            await using var reopened = new SqliteHistoryStore(path);
            Assert.Equal(interruptionId, Assert.Single((await reopened.LoadAsync()).Events).Id);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CorruptDatabaseFailsClosedWithoutResettingEvidence()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        try
        {
            byte[] bytes = "corrupt evidence - do not overwrite"u8.ToArray();
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            await using var store = new SqliteHistoryStore(path);
            await Assert.ThrowsAsync<SqliteException>(() => store.LoadAsync());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task IdempotentRetryAndInterruptedTransactionDoNotDuplicateOrPartiallyCommit()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        try
        {
            var session = new SessionRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, TimeSpan.Zero, false, false);
            var e = new MonitorEvent(1, session.Id, null, DateTimeOffset.UtcNow, MonitorEventKind.SessionStopped, "Stopped");
            var batch = new HistoryBatch([], [e], session);
            await using (var store = new SqliteHistoryStore(path)) { await store.LoadAsync(); await store.CommitAsync(batch); await store.CommitAsync(batch); }
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open(); using var tx = connection.BeginTransaction(); using var command = connection.CreateCommand();
                command.Transaction = tx; command.CommandText = "DELETE FROM events; DELETE FROM sessions"; command.ExecuteNonQuery();
                // Simulates interruption before COMMIT: disposal rolls the entire transaction back.
            }
            await using var reopened = new SqliteHistoryStore(path); var saved = await reopened.LoadAsync();
            Assert.Equal(e.Id, Assert.Single(saved.Events).Id); Assert.Single(saved.Sessions);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task OptInRetentionNeverDeletesUnresolvedIncidents()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        try
        {
            await using var store = new SqliteHistoryStore(path); await store.LoadAsync();
            var now = DateTimeOffset.UtcNow; var old = now.AddDays(-50);
            var recoveredSession = new SessionRecord(Guid.NewGuid(), old, old.AddMinutes(1), old.AddMinutes(1), TimeSpan.FromMinutes(1), false, false);
            var unresolvedSession = recoveredSession with { Id = Guid.NewGuid() };
            var recovered = new OutageRecord(Guid.NewGuid(), recoveredSession.Id, Guid.NewGuid(), "Recovered", old, old, old.AddSeconds(10), null, TimeSpan.FromSeconds(10), false, null);
            var unresolved = recovered with { Id = Guid.NewGuid(), SessionId = unresolvedSession.Id, RecoveredUtc = null, ObservationEndedUtc = old.AddMinutes(1) };
            await store.CommitAsync(new([recovered], [], recoveredSession)); await store.CommitAsync(new([unresolved], [], unresolvedSession));
            await store.RetainAsync(30, now);
            Assert.Equal(unresolved.Id, Assert.Single((await store.LoadAsync()).Outages).Id);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task WriteFailureRetainsEvidenceBlocksCloseAndCanBeRetried()
    {
        var clock = new ManualClock(); var disk = new FailingHistory();
        var service = new MonitoringService(new ScriptedProbe(Fail, Fail), clock, history: disk);
        try
        {
            await service.StartAsync(Config(Group())); await UntilAsync(() => clock.PendingDelays == 1);
            disk.FailWrites = true; clock.Advance(1000);
            await UntilAsync(() => service.Capture().State == SessionState.Faulted);
            Assert.True(service.HasUnsavedEvidence); Assert.Single(service.Capture().Outages);
            await Assert.ThrowsAsync<IOException>(() => service.DisposeAsync().AsTask());
            disk.FailWrites = false;
            await service.StopAsync();
            Assert.False(service.HasUnsavedEvidence);
        }
        finally { disk.FailWrites = false; await service.DisposeAsync(); }
    }

    [Fact]
    public async Task StoreCapacityFailureRollsBackBatchAndWriterLeasePreventsSecondOwner()
    {
        var directory = Temp(); var path = Path.Combine(directory, "history.db");
        try
        {
            await using var store = new SqliteHistoryStore(path, maxBytes: 128 * 1024); await store.LoadAsync();
            await using (var second = new SqliteHistoryStore(path))
                await Assert.ThrowsAnyAsync<IOException>(() => second.LoadAsync());
            var utc = DateTimeOffset.UtcNow;
            var session = new SessionRecord(Guid.NewGuid(), utc, utc, utc, TimeSpan.Zero, false, false);
            var initial = new MonitorEvent(1, session.Id, null, utc, MonitorEventKind.SessionStopped, "Kept");
            await store.CommitAsync(new([], [initial], session));
            var large = Enumerable.Range(0, 60).Select(_ => new OutageRecord(Guid.NewGuid(), session.Id, Guid.NewGuid(), "Group", utc, utc,
                null, utc, TimeSpan.Zero, false, null, new string('x', 4000))).ToArray();
            await Assert.ThrowsAsync<SqliteException>(() => store.CommitAsync(new(large, [], session)));
            var after = await store.LoadAsync();
            Assert.Empty(after.Outages); Assert.Equal(initial.Id, Assert.Single(after.Events).Id);
            Assert.True(new FileInfo(path).Length <= 128 * 1024);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void ExportableStatisticsUseObservedRecordsWithoutUptimeClaims()
    {
        var utc = DateTimeOffset.UtcNow; var group = Guid.NewGuid();
        var a = new OutageRecord(Guid.NewGuid(), Guid.NewGuid(), group, "Group", utc, utc, utc, null, TimeSpan.FromSeconds(2), false, null);
        var b = a with { Id = Guid.NewGuid(), RecoveredUtc = null, Elapsed = TimeSpan.FromSeconds(4), ContainsObservationGap = true };
        var stats = Assert.Single(AutomaticLogExporter.Statistics([a, b]));
        Assert.Equal(2, stats.Confirmed); Assert.Equal(1, stats.Unresolved); Assert.Equal(6, stats.SumElapsedSeconds);
        Assert.Equal(3, stats.MeanSeconds); Assert.True(stats.ContainsGaps);
    }
    private static string Temp() { var p = Path.Combine(Path.GetTempPath(), "Synups-history-" + Guid.NewGuid()); Directory.CreateDirectory(p); return p; }
}

internal sealed class FailingHistory : IHistoryStore
{
    public bool FailWrites { get; set; }
    public Task<HistoryState> LoadAsync(bool includeAllEvents = false) => Task.FromResult(new HistoryState([], [], [], 0));
    public Task CommitAsync(HistoryBatch batch) => FailWrites ? Task.FromException(new IOException("Disk full")) : Task.CompletedTask;
    public Task<int> RetainAsync(int days, DateTimeOffset utcNow) => Task.FromResult(0);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
