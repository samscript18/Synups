using Microsoft.Data.Sqlite;
using Synups.Core.Diagnostics;
using Synups.Core.Persistence;
using Xunit;
using static Synups.Tests.DiagnosticWorkflowTests;

namespace Synups.Tests;

public sealed class DiagnosticHardeningTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Synups-hardening-" + Guid.NewGuid());
    private string Database => Path.Combine(root, "history.db");
    public DiagnosticHardeningTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static DiagnosticSession Complete(DateTimeOffset? utc = null)
    {
        var request = Request(); var at = utc ?? DateTimeOffset.UtcNow;
        return DiagnosticSession.Start(request) with { StartedUtc = at, CompletedUtc = at, State = DiagnosticState.Completed, Results = [Result(request)] };
    }
    [Fact]
    public async Task DeletionProtectsActiveAndReferencedEvidenceAndBaselineDeletionIsAtomic()
    {
        await using var store = new SqliteHistoryStore(Database);
        var first = Complete(); var current = Complete(); var active = DiagnosticSession.Start(Request());
        foreach (var session in new[] { first, current, active }) await store.SaveDiagnosticSessionAsync(session);
        var baseline = new DiagnosticBaseline(Guid.NewGuid(), first.Request.Id, first.Request.Target, "Known good", DateTimeOffset.UtcNow);
        await store.SaveDiagnosticBaselineAsync(baseline);
        await store.SaveDiagnosticComparisonAsync(DiagnosticEvidence.Compare(first, current, baseline.Id));
        foreach (var id in new[] { first.Request.Id, current.Request.Id, active.Request.Id })
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteDiagnosticSessionAsync(id));
        Assert.True(await store.DeleteDiagnosticBaselineAsync(baseline.Id));
        Assert.False(await store.DeleteDiagnosticBaselineAsync(baseline.Id));
        Assert.NotNull(await store.FindDiagnosticSessionAsync(first.Request.Id));
        Assert.True(await store.DeleteDiagnosticSessionAsync(first.Request.Id));
        Assert.True(await store.DeleteDiagnosticSessionAsync(current.Request.Id));
        Assert.False(await store.DeleteDiagnosticSessionAsync(current.Request.Id));
        using var raw = new SqliteConnection($"Data Source={Database};Pooling=False"); raw.Open();
        foreach (var table in new[] { "diagnostic_results", "diagnostic_observations", "diagnostic_baselines", "diagnostic_comparisons" })
        {
            using var check = raw.CreateCommand(); check.CommandText = $"SELECT COUNT(*) FROM {table}"; Assert.Equal(0L, check.ExecuteScalar());
        }
        using var foreignKeys = raw.CreateCommand(); foreignKeys.CommandText = "PRAGMA foreign_key_check";
        using var reader = foreignKeys.ExecuteReader(); Assert.False(reader.Read());
    }
    [Fact]
    public async Task RetentionPreservesBaselinesComparisonsRecentAndUnfinishedCheckpoints()
    {
        await using var store = new SqliteHistoryStore(Database);
        var old = DateTimeOffset.UtcNow.AddDays(-100);
        var eligible = Complete(old); var pinned = Complete(old); var compared = Complete(old); var recent = Complete();
        var unfinished = DiagnosticSession.Start(Request()) with { StartedUtc = old };
        foreach (var s in new[] { eligible, pinned, compared, recent, unfinished }) await store.SaveDiagnosticSessionAsync(s);
        var baseline = new DiagnosticBaseline(Guid.NewGuid(), pinned.Request.Id, pinned.Request.Target, "Keep", old);
        await store.SaveDiagnosticBaselineAsync(baseline);
        await store.SaveDiagnosticComparisonAsync(DiagnosticEvidence.Compare(pinned, compared, baseline.Id));
        await store.RetainAsync(30, DateTimeOffset.UtcNow);
        Assert.Null(await store.FindDiagnosticSessionAsync(eligible.Request.Id));
        foreach (var s in new[] { pinned, compared, recent, unfinished }) Assert.NotNull(await store.FindDiagnosticSessionAsync(s.Request.Id));
    }
    [Fact]
    public async Task PagingIncludesEverySessionWhenTimestampsTie()
    {
        await using var store = new SqliteHistoryStore(Database); var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 103; i++) await store.SaveDiagnosticSessionAsync(Complete(at));
        var ids = new List<Guid>(); DiagnosticSessionSummary? last = null;
        while (true)
        {
            var rows = await store.ReadDiagnosticSessionsAsync(50, last?.StartedUtc, beforeId: last?.Id);
            if (rows.Count == 0) break;
            ids.AddRange(rows.Select(r => r.Id)); last = rows[^1];
        }
        Assert.Equal(103, ids.Count); Assert.Equal(103, ids.Distinct().Count());
    }
    [Fact]
    public async Task SqliteBusyDoesNotCompleteOrLoseCheckpointAndRetrySucceeds()
    {
        await using var store = new SqliteHistoryStore(Database);
        var running = DiagnosticSession.Start(Request()); await store.SaveDiagnosticSessionAsync(running);
        var terminal = running with { State = DiagnosticState.Completed, CompletedUtc = DateTimeOffset.UtcNow, Results = [Result(running.Request)] };
        using (var raw = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            raw.Open(); using var tx = raw.BeginTransaction();
            var failure = await Assert.ThrowsAsync<SqliteException>(() => store.SaveDiagnosticSessionAsync(terminal));
            Assert.Equal(5, failure.SqliteErrorCode); tx.Rollback();
        }
        Assert.Equal(DiagnosticState.Running, (await store.FindDiagnosticSessionAsync(running.Request.Id))!.State);
        await store.SaveDiagnosticSessionAsync(terminal);
        Assert.Equal(DiagnosticState.Completed, (await store.FindDiagnosticSessionAsync(running.Request.Id))!.State);
    }
    [Fact]
    public async Task RealSqliteFullRollsBackChildrenAndRepeatedRestartsRetainInterruptedCheckpoint()
    {
        var running = DiagnosticSession.Start(Request());
        await using (var store = new SqliteHistoryStore(Database, 128 * 1024))
        {
            await store.SaveDiagnosticSessionAsync(running);
            var large = Result(running.Request) with { Observations = [new(DateTimeOffset.UtcNow, "Bounded test text", DiagnosticCertainty.Observed, Text: new string('x', 100_000))] };
            var failure = await Assert.ThrowsAsync<SqliteException>(() => store.SaveDiagnosticSessionAsync(running with { Results = [large], CompletedUtc = DateTimeOffset.UtcNow, State = DiagnosticState.Completed }));
            Assert.Equal(13, failure.SqliteErrorCode);
            Assert.Empty((await store.FindDiagnosticSessionAsync(running.Request.Id))!.Results);
        }
        for (var i = 0; i < 5; i++)
        {
            await using var reopened = new SqliteHistoryStore(Database); await reopened.InitializeDiagnosticsAsync();
            var saved = (await reopened.FindDiagnosticSessionAsync(running.Request.Id))!;
            Assert.Equal(DiagnosticState.Interrupted, saved.State); Assert.Null(saved.CompletedUtc); Assert.Empty(saved.Results);
            Assert.Equal(running.StartedUtc, saved.StartedUtc); Assert.Empty((await reopened.LoadAsync()).Outages);
        }
    }
    [Fact]
    public async Task LargeExportCancellationRemovesPartialAndUnicodeRemainsExplicit()
    {
        var session = Complete();
        var large = session.Results[0] with { Observations = Enumerable.Range(0, 2000).Select(i => new DiagnosticObservation(DateTimeOffset.UtcNow, "TEST FIXTURE ONLY Unicode 日本語 café 🙂", DiagnosticCertainty.Observed, Text: new string('x', 500))).ToArray() };
        var evidence = DiagnosticEvidence.Preview(session with { Results = [large] }, redact: false);
        using var cancel = new CancellationTokenSource();
        var export = DiagnosticEvidence.ExportAsync(root, evidence, cancel.Token);
        while (!Directory.EnumerateDirectories(root).Any() && !export.IsCompleted) await Task.Delay(1, TestContext.Current.CancellationToken);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        var small = DiagnosticEvidence.Preview(session with { Results = [large with { Observations = large.Observations.Take(1).ToArray() }] }, redact: false);
        var result = await DiagnosticEvidence.ExportAsync(root, small, TestContext.Current.CancellationToken);
        Assert.Contains("日本語 café 🙂", await File.ReadAllTextAsync(Path.Combine(result.Directory, "evidence.md"), TestContext.Current.CancellationToken));
        Assert.Contains("u65E5", System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(Path.Combine(result.Directory, "evidence.pdf"), TestContext.Current.CancellationToken)));
    }
    [Fact]
    public async Task LargeEvidenceUsesConsistentRedactionAndHashesEveryActualArtifact()
    {
        var session = Complete();
        var text = "PRIVATE-HARDENING-SENTINEL 日本語 café 🙂";
        var result = session.Results[0] with { Observations = Enumerable.Range(0, 2000).Select(i => new DiagnosticObservation(DateTimeOffset.UtcNow, "TEST FIXTURE ONLY", DiagnosticCertainty.Observed, i, "ms", text + new string('x', 300))).ToArray() };
        var fixture = session with { Results = [result] };
        var parent = Environment.GetEnvironmentVariable("SYNUPS_LARGE_EXPORT_EVIDENCE");
        if (parent is not null) Directory.CreateDirectory(parent);
        foreach (var redacted in new[] { true, false })
        {
            var exported = await DiagnosticEvidence.ExportAsync(parent ?? root, DiagnosticEvidence.Preview(fixture, redact: redacted), TestContext.Current.CancellationToken);
            using var manifest = System.Text.Json.JsonDocument.Parse(await File.ReadAllBytesAsync(exported.ManifestPath, TestContext.Current.CancellationToken));
            foreach (var file in manifest.RootElement.GetProperty("Files").EnumerateArray())
            {
                var bytes = await File.ReadAllBytesAsync(Path.Combine(exported.Directory, file.GetProperty("File").GetString()!), TestContext.Current.CancellationToken);
                Assert.Equal(file.GetProperty("Bytes").GetInt32(), bytes.Length);
                Assert.Equal(file.GetProperty("Sha256").GetString(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
                if (redacted) Assert.DoesNotContain("PRIVATE-HARDENING-SENTINEL", System.Text.Encoding.UTF8.GetString(bytes));
            }
        }
    }

}
