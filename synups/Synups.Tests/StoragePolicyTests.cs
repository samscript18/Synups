using Microsoft.Data.Sqlite;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public sealed class StoragePolicyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Synups-storage-" + Guid.NewGuid());
    private string Database => Path.Combine(root, "history.db");
    private string Logs => Path.Combine(root, "logs");
    public StoragePolicyTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private SqliteConnection Raw()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        connection.Open(); return connection;
    }
    private static object? Sql(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    private static HistoryBatch Sample(long sequence = 1, DateTimeOffset? at = null, bool recovered = true, bool interrupted = false)
    {
        var utc = at ?? DateTimeOffset.UtcNow;
        var session = new SessionRecord(Guid.NewGuid(), utc.AddMinutes(-1), utc, utc, TimeSpan.FromMinutes(1), interrupted, interrupted);
        var outage = new OutageRecord(Guid.NewGuid(), session.Id, Guid.NewGuid(), "Stored router", utc.AddSeconds(-30), utc.AddSeconds(-40),
            recovered ? utc : null, recovered ? null : utc, TimeSpan.FromSeconds(30), interrupted, null, "Keep historical comment");
        return new([outage], [new(sequence, session.Id, null, utc, MonitorEventKind.SessionStopped, "Stored transition")], session);
    }

    [Fact]
    public async Task LegacyDatabaseUpgradesInPlaceAndUsesItsExistingPageSize()
    {
        using (var raw = Raw()) Sql(raw, "PRAGMA page_size=8192; CREATE TABLE legacy_fixture(value TEXT)");
        var sample = Sample();
        await using (var old = new SqliteHistoryStore(Database, 64 * HistoryCapacity.MiB))
        { await old.LoadAsync(); await old.CommitAsync(sample); }
        // A real v1 schema with no storage policy, as written by Phase 3.
        using (var raw = Raw()) Sql(raw, "DROP TABLE storage_policy");
        await using var migrated = new SqliteHistoryStore(Database);
        var records = await migrated.LoadAsync(true);
        Assert.Equal(sample.Outages, records.Outages); Assert.Equal(sample.Events, records.Events); Assert.Equal(sample.Session, Assert.Single(records.Sessions));
        var usage = await migrated.GetStorageUsageAsync(Logs);
        Assert.Equal(8192, usage.PageSize); Assert.Equal(HistoryCapacity.DefaultBytes, usage.ConfiguredBytes);
        Assert.Equal(HistoryCapacity.DefaultBytes, usage.EffectiveLimitBytes);
        Assert.Equal(0, usage.BackupBytes); // Migration never archives/resets history.
    }

    [Theory]
    [InlineData(256)]
    [InlineData(2048)]
    [InlineData(10240)]
    public async Task ConfiguredCapacityIsAppliedAndSurvivesNewStore(int mib)
    {
        var sample = Sample();
        await using (var store = new SqliteHistoryStore(Database))
        {
            await store.LoadAsync(); await store.CommitAsync(sample);
            await store.SetCapacityAsync(mib * HistoryCapacity.MiB);
            var usage = await store.GetStorageUsageAsync(Logs);
            Assert.Equal(mib * HistoryCapacity.MiB, usage.EffectiveLimitBytes);
        }
        await using var reopened = new SqliteHistoryStore(Database);
        Assert.Equal(sample.Outages, (await reopened.LoadAsync()).Outages);
        var restored = await reopened.GetStorageUsageAsync(Logs);
        Assert.Equal(mib * HistoryCapacity.MiB, restored.ConfiguredBytes);
        Assert.Equal(restored.ConfiguredBytes, restored.EffectiveLimitBytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(255L * 1024 * 1024)]
    [InlineData(256L * 1024 * 1024 + 1)]
    [InlineData(10241L * 1024 * 1024)]
    [InlineData(long.MaxValue)]
    public async Task InvalidCapacityLeavesPolicyAndEvidenceUnchanged(long bytes)
    {
        await using var store = new SqliteHistoryStore(Database);
        await store.LoadAsync(); var sample = Sample(); await store.CommitAsync(sample);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SetCapacityAsync(bytes));
        Assert.Equal(HistoryCapacity.DefaultBytes, (await store.GetStorageUsageAsync(Logs)).EffectiveLimitBytes);
        Assert.Equal(sample.Events, (await store.LoadAsync()).Events);
    }

    [Fact]
    public async Task FailedCapacityTransactionRestoresConnectionLimitAndDurableSetting()
    {
        await using (var store = new SqliteHistoryStore(Database))
        {
            await store.LoadAsync(); await store.SetCapacityAsync(HistoryCapacity.MinimumBytes);
            using (var raw = Raw()) Sql(raw, "CREATE TRIGGER fail_policy BEFORE UPDATE ON storage_policy BEGIN SELECT RAISE(ABORT,'injected policy write failure'); END");
            await Assert.ThrowsAsync<SqliteException>(() => store.SetCapacityAsync(HistoryCapacity.MaximumBytes));
            var usage = await store.GetStorageUsageAsync(Logs);
            Assert.Equal(HistoryCapacity.MinimumBytes, usage.ConfiguredBytes); Assert.Equal(usage.ConfiguredBytes, usage.EffectiveLimitBytes);
        }
        await using var reopened = new SqliteHistoryStore(Database);
        Assert.Equal(HistoryCapacity.MinimumBytes, (await reopened.GetStorageUsageAsync(Logs)).ConfiguredBytes);
    }

    [Fact]
    public async Task PreExistingLargerDatabaseIsPreservedAndLoweringBelowAllocationIsRefused()
    {
        using (var raw = Raw()) Sql(raw, "PRAGMA page_size=65536; CREATE TABLE legacy_fixture(value TEXT)");
        var sample = Sample();
        await using (var seed = new SqliteHistoryStore(Database)) { await seed.LoadAsync(); await seed.CommitAsync(sample); }
        using (var raw = Raw())
        {
            Sql(raw, "DROP TABLE storage_policy; CREATE TABLE legacy_payload(payload BLOB); INSERT INTO legacy_payload VALUES(zeroblob(269484032));");
        }
        await using var store = new SqliteHistoryStore(Database, HistoryCapacity.MinimumBytes);
        Assert.Equal(sample.Outages, (await store.LoadAsync()).Outages);
        var larger = await store.GetStorageUsageAsync(Logs);
        Assert.True(larger.AllocatedBytes > larger.ConfiguredBytes);
        Assert.Equal(larger.AllocatedBytes, larger.EffectiveLimitBytes);
        Assert.Equal(95, larger.WarningLevel);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetCapacityAsync(HistoryCapacity.MinimumBytes));
        Assert.Equal(larger.ConfiguredBytes, (await store.GetStorageUsageAsync(Logs)).ConfiguredBytes);
        await store.SetCapacityAsync(HistoryCapacity.DefaultBytes);
        Assert.Equal(sample.Outages, (await store.LoadAsync()).Outages);
        using var verify = Raw(); Assert.Equal(269484032L, Sql(verify, "SELECT length(payload) FROM legacy_payload"));
    }

    [Fact]
    public void ThresholdsAreExactAndOscillationDoesNotRepeatNotifications()
    {
        StorageUsage Usage(long bytes, long limit = 1000) => new(limit, limit, 1, bytes, 0, bytes, 0, 0, 0, 0, 0, null, "test-only");
        var policy = new StorageWarningPolicy();
        Assert.Equal(0, Usage(799).WarningLevel); Assert.Equal(80, Usage(800).WarningLevel);
        Assert.Equal(80, policy.Select(Usage(800))); Assert.Equal(0, policy.Select(Usage(949)));
        Assert.Equal(0, policy.Select(Usage(100))); Assert.Equal(0, policy.Select(Usage(800)));
        Assert.Equal(95, Usage(950).WarningLevel); Assert.Equal(95, policy.Select(Usage(950)));
        Assert.Equal(0, policy.Select(Usage(1000))); Assert.Equal(0, policy.Select(Usage(800)));
        Assert.Equal(0, policy.Select(Usage(1000, 2000)));
        Assert.Equal(80, policy.Select(Usage(1600, 2000)));
    }

    [Fact]
    public async Task UsageIncludesRealWalShmBackupsAndDerivedLogsAndBackupContainsWalCommit()
    {
        await using var store = new SqliteHistoryStore(Database); await store.LoadAsync();
        var sample = Sample(); await store.CommitAsync(sample);
        await AutomaticLogExporter.WriteAsync(Logs, await store.LoadAsync(true));
        using var walWriter = Raw();
        Sql(walWriter, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE wal_evidence(value TEXT); INSERT INTO wal_evidence VALUES('committed in WAL');");
        Assert.True(new FileInfo(Database + "-wal").Length > 0);
        var backup = await store.BackupAsync();
        using (var read = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False"))
        { read.Open(); Assert.Equal("committed in WAL", Sql(read, "SELECT value FROM wal_evidence")); Assert.Equal("ok", Sql(read, "PRAGMA quick_check")); }
        var usage = await store.GetStorageUsageAsync(Logs);
        Assert.Equal(new FileInfo(Database).Length, usage.DatabaseBytes);
        Assert.Equal(new FileInfo(Database + "-wal").Length, usage.WalBytes); Assert.True(usage.WalBytes > 0);
        Assert.Equal(new FileInfo(Database + "-shm").Length, usage.ShmBytes); Assert.True(usage.ShmBytes > 0);
        Assert.Equal(new FileInfo(backup).Length, usage.BackupBytes);
        Assert.Equal(Directory.GetFiles(Logs).Sum(p => new FileInfo(p).Length), usage.DerivedLogBytes);
        Assert.Equal(usage.DatabaseBytes + usage.WalBytes + usage.ShmBytes + usage.JournalBytes + usage.BackupBytes + usage.DerivedLogBytes, usage.TotalDiskBytes);
        Assert.True(usage.TotalDiskBytes > usage.AllocatedBytes); Assert.True(usage.VolumeAvailableBytes > 0);
        Assert.Empty(Directory.GetFiles(usage.BackupDirectory, "*.tmp"));
    }

    [Fact]
    public async Task VerifiedBackupReloadsAllHistoryAndPolicyIndependently()
    {
        await using var store = new SqliteHistoryStore(Database); await store.LoadAsync();
        await store.SetCapacityAsync(HistoryCapacity.MinimumBytes);
        var sample = Sample(); await store.CommitAsync(sample);
        var backup = await store.BackupAsync();
        var second = await store.BackupAsync(); Assert.NotEqual(backup, second);
        await using var restored = new SqliteHistoryStore(backup);
        var saved = await restored.LoadAsync(true);
        Assert.Equal(sample.Outages, saved.Outages); Assert.Equal(sample.Events, saved.Events); Assert.Equal(sample.Session, Assert.Single(saved.Sessions));
        Assert.Equal(HistoryCapacity.MinimumBytes, (await restored.GetStorageUsageAsync(Logs)).EffectiveLimitBytes);
    }

    [Fact]
    public async Task RetentionPreservesUnresolvedInterruptedAndRecentSessionsAndReusesPages()
    {
        await using var store = new SqliteHistoryStore(Database); await store.LoadAsync();
        var now = DateTimeOffset.UtcNow; var old = now.AddDays(-60);
        var eligible = Sample(1, old); var unresolved = Sample(2, old, recovered: false); var interrupted = Sample(3, old, interrupted: true); var recent = Sample(4, now);
        foreach (var batch in new[] { eligible, unresolved, interrupted, recent }) await store.CommitAsync(batch);
        var before = await store.GetStorageUsageAsync(Logs);
        Assert.True(await store.RetainAsync(30, now) > 0);
        var kept = await store.LoadAsync(true);
        Assert.Equal(new[] { unresolved.Session!.Id, interrupted.Session!.Id, recent.Session!.Id }, kept.Sessions.Select(s => s.Id));
        Assert.Equal(new long[] { 2, 3, 4 }, kept.Events.Select(e => e.Sequence));
        Assert.Equal(4, kept.LastSequence); Assert.Equal(0, await store.RetainAsync(30, now));
        Assert.Equal(before.DatabaseBytes, (await store.GetStorageUsageAsync(Logs)).DatabaseBytes);
    }

    [Fact]
    public async Task RealSqliteFullKeepsPendingBatchesAndIncreasingCapacityAllowsRetryAndMonitoring()
    {
        var disk = new SqliteHistoryStore(Database, 128 * 1024); await disk.LoadAsync();
        using (var raw = Raw()) Sql(raw, "CREATE TABLE pressure(payload BLOB); CREATE TRIGGER allocate_space BEFORE INSERT ON events BEGIN INSERT INTO pressure VALUES(zeroblob(200000)); END");
        var clock = new ManualClock();
        await using var engine = new MonitoringService(new ScriptedProbe(Fail, Fail, Ok), clock, history: disk);
        var failure = await Assert.ThrowsAsync<IOException>(() => engine.StartAsync(Config(Group())));
        Assert.Equal(13, Assert.IsType<SqliteException>(failure.InnerException).SqliteErrorCode);
        Assert.True(engine.HasUnsavedEvidence); Assert.Empty((await disk.LoadAsync()).Events);
        await Assert.ThrowsAsync<IOException>(() => engine.ApplyRetentionAsync(1));
        await Assert.ThrowsAsync<IOException>(() => engine.BackupHistoryAsync());
        await Assert.ThrowsAsync<IOException>(() => engine.DisposeAsync().AsTask());
        var ids = engine.Capture().Events.Select(e => e.Id).ToArray();
        await engine.SetHistoryCapacityAsync(HistoryCapacity.MinimumBytes);
        await engine.RetryPersistenceAsync(); await engine.StopAsync();
        Assert.False(engine.HasUnsavedEvidence); Assert.Null(engine.PersistenceError);
        Assert.Equal(ids, (await engine.ReadArchiveAsync()).Events.Select(e => e.Id));
        await engine.StartAsync(Config(Group())); await UntilAsync(() => clock.PendingDelays == 1);
        clock.Advance(1000); await UntilAsync(() => clock.PendingDelays == 1);
        Assert.Single(engine.Capture().Outages);
        clock.Advance(1000); await UntilAsync(() => engine.Capture().Outages[0].RecoveredUtc.HasValue);
        await engine.StopAsync(); Assert.False(engine.HasUnsavedEvidence);
    }

    [Fact]
    public async Task RunningMonitorRefusesCapacityChangeAndKeepsItsExistingPolicy()
    {
        var clock = new ManualClock();
        await using var engine = new MonitoringService(new ScriptedProbe(), clock, history: new SqliteHistoryStore(Database));
        await engine.StartAsync(Config(Group())); await UntilAsync(() => clock.PendingDelays == 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SetHistoryCapacityAsync(HistoryCapacity.MaximumBytes));
        Assert.Equal(SessionState.Running, engine.Capture().State);
        Assert.Equal(HistoryCapacity.DefaultBytes, (await engine.GetStorageUsageAsync(Logs)).ConfiguredBytes);
        await engine.StopAsync();
    }

    [Fact]
    public async Task CorruptedHistoryCannotBeResetByCapacityChangeOrBackup()
    {
        byte[] original = "broken history evidence"u8.ToArray();
        await File.WriteAllBytesAsync(Database, original, TestContext.Current.CancellationToken);
        await using var store = new SqliteHistoryStore(Database);
        await Assert.ThrowsAsync<SqliteException>(() => store.SetCapacityAsync(HistoryCapacity.MinimumBytes));
        await Assert.ThrowsAsync<SqliteException>(() => store.BackupAsync());
        Assert.Equal(original, await File.ReadAllBytesAsync(Database, TestContext.Current.CancellationToken));
    }
}
