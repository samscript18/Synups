using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Synups.Core.Diagnostics;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Xunit;

namespace Synups.Tests;

public class DiagnosticWorkflowTests
{
    private static string Temp() { var d = Path.Combine(Path.GetTempPath(), "Synups-diagnostic-" + Guid.NewGuid()); Directory.CreateDirectory(d); return d; }
    public static DiagnosticRequest Request(string target = "127.0.0.1", DiagnosticKind kind = DiagnosticKind.TcpConnect) => new(Guid.NewGuid(), target, [kind], new(), new(true));
    public static DiagnosticTestResult Result(DiagnosticRequest r, DiagnosticOutcome outcome = DiagnosticOutcome.Success) => new(Guid.NewGuid(), r.Tests[0], r.Target, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, outcome, "TEST FIXTURE ONLY", 1,
        [new(DateTimeOffset.UtcNow, "Test fixture observation", DiagnosticCertainty.Observed, 1, "ms", "private.internal 10.1.2.3")]);

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(65536)]
    public void RejectsUnsafePorts(int port) => Assert.Throws<ArgumentException>(() => (Request() with { Parameters = new() { Port = port } }).ValidatedCopy());
    [Theory]
    [InlineData("https://example.com")] [InlineData("0.0.0.0")] [InlineData("224.0.0.1")] [InlineData("ff02::1")] [InlineData("name\nsecret")]
    public void RejectsHostileTargets(string target) => Assert.Throws<ArgumentException>(() => Request(target).ValidatedCopy());
    [Fact]
    public void ApprovalProbeCapsAndResolverConsentAreMandatory()
    {
        Assert.Throws<ArgumentException>(() => (Request() with { Consent = new() }).ValidatedCopy());
        Assert.Throws<ArgumentException>(() => (Request(kind: DiagnosticKind.TraceRoute) with { Parameters = new() { MaximumProbes = 1 } }).ValidatedCopy());
        Assert.Throws<ArgumentException>(() => (Request() with { Parameters = new() { ResolverAddress = "127.0.0.1" } }).ValidatedCopy());
    }
    [Fact]
    public async Task GenuineTcpListenerAndRefusalUseConnectWithoutApplicationPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        // Windows can surface the closed-port RST after a stack retry; allow its real refusal within the supported deadline.
        var r = Request() with { Parameters = new() { Port = port, ProbeTimeoutMilliseconds = 5000 } };
        var connected = await new TcpConnectTest().ExecuteAsync(r, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, connected.Outcome); Assert.Contains("Socket.ConnectAsync", connected.Method);
        using var accepted = await listener.AcceptSocketAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, accepted.Available); listener.Stop(); accepted.Close();
        var refused = await new TcpConnectTest().ExecuteAsync(r, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Refused, refused.Outcome); Assert.DoesNotContain("firewall blocked", refused.Error ?? "");
    }
    [Fact]
    public async Task RealRunnerPersistsToSqliteAndReopensWithoutManufacturingAnIncident()
    {
        var d = Temp(); var path = Path.Combine(d, "history.db"); Guid id;
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            await using (var store = new SqliteHistoryStore(path))
            await using (var runner = new DiagnosticRunner(store, [new TcpConnectTest()]))
            {
                var request = Request() with { Parameters = new() { Port = ((IPEndPoint)listener.LocalEndpoint).Port } };
                var session = await runner.RunAsync(request, cancellation: TestContext.Current.CancellationToken); id = session.Request.Id;
                Assert.Equal(DiagnosticState.Completed, session.State); Assert.Single(session.Results); Assert.Empty((await store.LoadAsync()).Outages);
            }
            await using var reopened = new SqliteHistoryStore(path); await reopened.InitializeDiagnosticsAsync();
            var saved = await reopened.FindDiagnosticSessionAsync(id); Assert.NotNull(saved); Assert.Equal(DiagnosticState.Completed, saved.State);
            Assert.Equal(id, Assert.Single(await reopened.ReadDiagnosticSessionsAsync()).Id);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task InterruptedCheckpointKeepsOriginalTimesAndPartialResultsWithoutFalseRecovery()
    {
        var d = Temp(); var path = Path.Combine(d, "history.db"); var r = Request(); var session = DiagnosticSession.Start(r) with { Results = [Result(r)] };
        try
        {
            await using (var store = new SqliteHistoryStore(path)) await store.SaveDiagnosticSessionAsync(session);
            await using var reopened = new SqliteHistoryStore(path); await reopened.InitializeDiagnosticsAsync();
            var saved = (await reopened.FindDiagnosticSessionAsync(r.Id))!;
            Assert.Equal(DiagnosticState.Interrupted, saved.State); Assert.Null(saved.CompletedUtc); Assert.Equal(session.StartedUtc, saved.StartedUtc); Assert.Single(saved.Results);
            Assert.Empty((await reopened.LoadAsync()).Outages); await reopened.InitializeDiagnosticsAsync();
            Assert.Equal(DiagnosticState.Interrupted, (await reopened.FindDiagnosticSessionAsync(r.Id))!.State);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task ForeignKeysRejectNonexistentIncidentAndProtectLinkedEvidenceFromRetention()
    {
        var d = Temp(); var path = Path.Combine(d, "history.db");
        try
        {
            await using var store = new SqliteHistoryStore(path); await store.LoadAsync();
            var bad = DiagnosticSession.Start(Request() with { IncidentId = Guid.NewGuid() });
            await Assert.ThrowsAsync<SqliteException>(() => store.SaveDiagnosticSessionAsync(bad)); Assert.Empty(await store.ReadDiagnosticSessionsAsync());
            var utc = DateTimeOffset.UtcNow.AddDays(-100); var monitor = new SessionRecord(Guid.NewGuid(), utc, utc.AddMinutes(1), utc, TimeSpan.FromMinutes(1), false, false);
            var outage = new OutageRecord(Guid.NewGuid(), monitor.Id, Guid.NewGuid(), "private group", utc, utc, utc.AddSeconds(30), utc.AddMinutes(1), TimeSpan.FromSeconds(30), false, null, "private comment");
            await store.CommitAsync(new([outage], [], monitor));
            var linked = DiagnosticSession.Start(Request() with { IncidentId = outage.Id }); await store.SaveDiagnosticSessionAsync(linked);
            await store.RetainAsync(30, DateTimeOffset.UtcNow);
            Assert.NotNull(await store.FindIncidentAsync(outage.Id)); Assert.Equal(outage.Id, (await store.FindDiagnosticSessionAsync(linked.Request.Id))!.Request.IncidentId);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task CompletedEvidenceCannotBeRewrittenAndCorruptRowFailsClosed()
    {
        var d = Temp(); var path = Path.Combine(d, "history.db"); var r = Request(); var session = DiagnosticSession.Start(r) with { Results = [Result(r)], State = DiagnosticState.Completed, CompletedUtc = DateTimeOffset.UtcNow };
        try
        {
            await using (var store = new SqliteHistoryStore(path))
            {
                await store.SaveDiagnosticSessionAsync(session); await store.SaveDiagnosticSessionAsync(session);
                await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDiagnosticSessionAsync(session with { State = DiagnosticState.Running }));
            }
            using (var c = new SqliteConnection($"Data Source={path};Pooling=False")) { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE diagnostic_sessions SET payload='{broken'"; cmd.ExecuteNonQuery(); }
            await using var reopened = new SqliteHistoryStore(path); await Assert.ThrowsAsync<InvalidDataException>(() => reopened.FindDiagnosticSessionAsync(r.Id));
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task BoundedRunnerRejectsTargetOverlapAndGlobalOverflowAndRetainsCancellation()
    {
        var d = Temp();
        try
        {
            await using var store = new SqliteHistoryStore(Path.Combine(d, "history.db")); var test = new WaitingTest();
            await using var runner = new DiagnosticRunner(store, [test], 1); using var cancel = new CancellationTokenSource();
            var running = runner.RunAsync(Request(), cancellation: cancel.Token); await test.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Request(), cancellation: TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Request("127.0.0.2"), cancellation: TestContext.Current.CancellationToken));
            cancel.Cancel(); var result = await running; Assert.Equal(DiagnosticState.Cancelled, result.State); Assert.Equal(DiagnosticOutcome.Cancelled, Assert.Single(result.Results).Outcome);
            Assert.Equal(DiagnosticState.Cancelled, (await store.FindDiagnosticSessionAsync(result.Request.Id))!.State);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task UnsupportedProtocolsCannotPretendTcpConnectWasRawSyn()
    {
        var d = Temp();
        try
        {
            await using var store = new SqliteHistoryStore(Path.Combine(d, "history.db")); await using var runner = new DiagnosticRunner(store, []);
            var session = await runner.RunAsync(Request(kind: DiagnosticKind.RawTcpSyn), cancellation: TestContext.Current.CancellationToken);
            Assert.Equal(DiagnosticOutcome.Unsupported, Assert.Single(session.Results).Outcome); Assert.Equal(DiagnosticState.Failed, session.State);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task BaselineRequiresSuccessAndComparisonRejectsIncompatibleWindows()
    {
        var d = Temp(); var r = Request(kind: DiagnosticKind.PingQuality); var result = Result(r) with { Statistics = new(5, 5, 5, 0, 10, 20, 30, 5, 2, 0, false) };
        var old = DiagnosticSession.Start(r) with { Results = [result], State = DiagnosticState.Completed, CompletedUtc = DateTimeOffset.UtcNow };
        try
        {
            await using var store = new SqliteHistoryStore(Path.Combine(d, "history.db")); await store.SaveDiagnosticSessionAsync(old);
            var baseline = new DiagnosticBaseline(Guid.NewGuid(), r.Id, r.Target, "Known good", DateTimeOffset.UtcNow); await store.SaveDiagnosticBaselineAsync(baseline);
            Assert.Single(await store.ReadDiagnosticBaselinesAsync());
            var next = old with { Request = r with { Id = Guid.NewGuid() }, Results = [result with { Statistics = result.Statistics with { AverageMilliseconds = 35 } }] };
            var c = DiagnosticEvidence.Compare(old, next, baseline.Id); Assert.Equal(15, Assert.Single(c.Differences, x => x.Name == "RTT difference").Value);
            var incompatible = DiagnosticEvidence.Compare(old, next with { Request = next.Request with { Parameters = new() { SampleCount = 8 } } }, baseline.Id);
            Assert.All(incompatible.Differences, x => Assert.Equal(DiagnosticCertainty.Unknown, x.Certainty));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDiagnosticBaselineAsync(baseline with { Id = Guid.NewGuid(), Target = "another.example" }));
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task ExportsShareRedactedSnapshotAndManifestHashesActualBytesWithoutOverwriting()
    {
        var d = Temp(); var r = Request("private.internal"); var s = DiagnosticSession.Start(r) with { Results = [Result(r)] };
        try
        {
            var evidence = DiagnosticEvidence.Preview(s);
            Assert.DoesNotContain("private.internal", Encoding.UTF8.GetString(DiagnosticEvidence.CanonicalJson(evidence)));
            Assert.DoesNotContain("10.1.2.3", DiagnosticEvidence.ToMarkdown(evidence)); Assert.Equal("private.internal", s.Request.Target);
            var export = await DiagnosticEvidence.ExportAsync(d, evidence, TestContext.Current.CancellationToken);
            using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(export.ManifestPath, TestContext.Current.CancellationToken));
            foreach (var file in manifest.RootElement.GetProperty("Files").EnumerateArray())
            {
                var bytes = await File.ReadAllBytesAsync(Path.Combine(export.Directory, file.GetProperty("File").GetString()!), TestContext.Current.CancellationToken);
                Assert.Equal(file.GetProperty("Sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
                Assert.Equal(file.GetProperty("Bytes").GetInt32(), bytes.Length);
            }
            using var reopened = JsonDocument.Parse(await File.ReadAllBytesAsync(export.Files.Single(f => f.EndsWith(".json")), TestContext.Current.CancellationToken));
            Assert.True(reopened.RootElement.GetProperty("Redacted").GetBoolean());
            Assert.StartsWith("%PDF-1.4", await File.ReadAllTextAsync(export.Files.Single(f => f.EndsWith(".pdf")), TestContext.Current.CancellationToken));
            var second = await DiagnosticEvidence.ExportAsync(d, evidence, TestContext.Current.CancellationToken); Assert.NotEqual(export.Directory, second.Directory);
            await Assert.ThrowsAsync<ArgumentException>(() => DiagnosticEvidence.ExportAsync("relative-path", evidence, TestContext.Current.CancellationToken));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DiagnosticEvidence.ExportAsync(d, evidence, cancel.Token));
            Assert.Empty(Directory.GetDirectories(d, ".*.partial"));
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task AdditiveVersionOneMigrationPreservesLegacySchemaAndPolicy()
    {
        var d = Temp(); var path = Path.Combine(d, "history.db");
        try
        {
            // Build the original schema with original production store semantics, then remove ONLY new empty diagnostics tables to recreate v1.
            await using (var store = new SqliteHistoryStore(path)) { await store.LoadAsync(); }
            using (var c = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "DROP TABLE diagnostic_observations; DROP TABLE diagnostic_results; DROP TABLE diagnostic_comparisons; DROP TABLE diagnostic_baselines; DROP TABLE diagnostic_sessions; PRAGMA user_version=1;"; cmd.ExecuteNonQuery();
            }
            await using var migrated = new SqliteHistoryStore(path); await migrated.InitializeDiagnosticsAsync(); Assert.Empty(await migrated.ReadDiagnosticSessionsAsync());
            Assert.Equal(HistoryCapacity.DefaultBytes, (await migrated.GetStorageUsageAsync(Path.Combine(d, "logs"))).ConfiguredBytes);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task CheckpointResultsAreAppendOnlyAndCannotBeReassignedAcrossSessions()
    {
        var d = Temp(); var request = Request(); var first = Result(request); var running = DiagnosticSession.Start(request) with { Results = [first] };
        try
        {
            await using var store = new SqliteHistoryStore(Path.Combine(d, "history.db"));
            await store.SaveDiagnosticSessionAsync(running);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDiagnosticSessionAsync(running with { Results = [] }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDiagnosticSessionAsync(running with { Results = [first with { Outcome = DiagnosticOutcome.Refused }] }));
            var other = DiagnosticSession.Start(Request()) with { Results = [first] };
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDiagnosticSessionAsync(other));
            Assert.Single(await store.ReadDiagnosticSessionsAsync());
            Assert.Equal(DiagnosticOutcome.Success, Assert.Single((await store.FindDiagnosticSessionAsync(request.Id))!.Results).Outcome);
        }
        finally { Directory.Delete(d, true); }
    }
    [Fact]
    public async Task CancelledHostnameCallerDoesNotReleaseNativeLookupCapacityOrAccumulateWorkers()
    {
        var native = new TaskCompletionSource<IPAddress[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new BoundedHostnameResolver(1, _ => { entered.TrySetResult(); return native.Task; });
        using var cancel = new CancellationTokenSource();
        var first = resolver.ResolveAsync("fixture.invalid", cancel.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(1, resolver.ActiveLookups);
        await Assert.ThrowsAsync<SocketException>(() => resolver.ResolveAsync("another.invalid", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TimeoutException>(() => resolver.DrainAsync(TimeSpan.FromMilliseconds(10)));
        native.SetResult([IPAddress.Loopback]); await resolver.DrainAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(0, resolver.ActiveLookups);
        Assert.Equal(IPAddress.Loopback, Assert.Single(await resolver.ResolveAsync("127.0.0.1", TestContext.Current.CancellationToken)));
    }
    [Fact]
    public async Task FailedNativeHostnameLookupReleasesItsBoundedSlot()
    {
        var calls = 0;
        var resolver = new BoundedHostnameResolver(1, _ => Interlocked.Increment(ref calls) == 1
            ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)) : Task.FromResult(new[] { IPAddress.IPv6Loopback }));
        await Assert.ThrowsAsync<SocketException>(() => resolver.ResolveAsync("missing.invalid", TestContext.Current.CancellationToken));
        Assert.Equal(IPAddress.IPv6Loopback, Assert.Single(await resolver.ResolveAsync("fixture.invalid", TestContext.Current.CancellationToken)));
        Assert.Equal(0, resolver.ActiveLookups);
    }
    private sealed class WaitingTest : IDiagnosticTest
    {
        public DiagnosticKind Kind => DiagnosticKind.TcpConnect;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
        { Entered.SetResult(); await Task.Delay(Timeout.Infinite, cancellation); return Result(request); }
    }
}
