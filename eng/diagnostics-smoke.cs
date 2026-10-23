// Engineering-only real network/persistence verification. Not part of shipped projects.
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Synups.Core.Diagnostics;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;

internal static class DiagnosticsSmoke
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private sealed record IncidentFingerprint(int Count, int Comments, string PayloadSha256, string CommentsSha256, Guid? FirstId);
    private sealed record ExportCheckpoint(string Directory, string ManifestSha256);
    private sealed record Checkpoint(IncidentFingerprint Before, Guid[] Sessions, string[] SessionHashes,
        Guid? BaselineId, Guid? ComparisonId, string? ComparisonHash, ExportCheckpoint[] Exports, bool RequiredPassed, Guid? DnsSession);

    public static async Task<int> Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args[1] is not ("observe" or "reload")) throw new ArgumentException("Expected OUTPUT observe|reload [--query-public-dns].");
        var output = Path.GetFullPath(args[0]);
        try
        {
            if (args[1] == "observe") await Observe(output, args.Contains("--query-public-dns"));
            else await Reload(output);
            return 0;
        }
        catch (Exception ex)
        {
            await Write(Path.Combine(output, args[1] + "-failure.json"), new { Stage = args[1], Passed = false, ErrorType = ex.GetType().Name, Utc = DateTimeOffset.UtcNow });
            Console.Error.WriteLine(ex); // Console logs stay in the explicitly local-only output directory.
            return 1;
        }
    }

    private static async Task Observe(string output, bool publicDns)
    {
        var database = Path.Combine(output, "data", "history.v1.db");
        Require(!File.Exists(Path.Combine(output, "checkpoint.json")), "Refusing to overwrite previous smoke evidence.");
        var concurrency = await ConcurrentMonitoring(output);
        var before = Fingerprint(database);
        var exports = new List<ExportCheckpoint>(); var sessions = new List<DiagnosticSession>();
        DiagnosticBaseline? baseline = null; DiagnosticComparison? comparison = null; DiagnosticSession? dns = null;
        await using (var store = new SqliteHistoryStore(database))
        await using (var runner = new DiagnosticRunner(store))
        {
            await store.InitializeDiagnosticsAsync();
            var incident = before.FirstId is { } incidentId ? await store.FindIncidentAsync(incidentId) : null;
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using var stopListener = new CancellationTokenSource();
            var accepts = Accept(listener, stopListener.Token);
            try
            {
                var parameters = new DiagnosticParameters { Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                    SampleCount = 3, ProbeTimeoutMilliseconds = 500, IntervalMilliseconds = 10,
                    MaximumHops = 4, ProbesPerHop = 1, SessionTimeoutSeconds = 30, MaximumProbes = 16, ResolveHopNames = false };
                for (var index = 0; index < 2; index++)
                {
                    var request = new DiagnosticRequest(Guid.NewGuid(), "127.0.0.1",
                        [DiagnosticKind.TcpConnect, DiagnosticKind.PingQuality, DiagnosticKind.TraceRoute, DiagnosticKind.NetworkConfiguration],
                        parameters, new(true), incident?.Id);
                    var session = await runner.RunAsync(request);
                    sessions.Add(session);
                    Console.WriteLine($"LOOPBACK_SESSION_{index + 1}={session.State}; " + string.Join(", ", session.Results.Select(r => $"{r.Kind}={r.Outcome}")));
                }
            }
            finally { stopListener.Cancel(); listener.Stop(); await accepts; }

            if (sessions[0].State == DiagnosticState.Completed)
            {
                baseline = new(Guid.NewGuid(), sessions[0].Request.Id, sessions[0].Request.Target, "Engineering real loopback baseline", DateTimeOffset.UtcNow);
                await store.SaveDiagnosticBaselineAsync(baseline);
                comparison = DiagnosticEvidence.Compare(sessions[0], sessions[1], baseline.Id);
                await store.SaveDiagnosticComparisonAsync(comparison);
            }
            foreach (var session in sessions)
            {
                var preview = DiagnosticEvidence.Preview(session, incident, redact: true,
                    comparison: session.Request.Id == sessions[1].Request.Id ? comparison : null);
                var exported = await DiagnosticEvidence.ExportAsync(output, preview);
                VerifyExport(exported.Directory);
                exports.Add(new(Path.GetRelativePath(output, exported.Directory), Hash(File.ReadAllBytes(exported.ManifestPath))));
            }

            if (publicDns)
            {
                // This explicit CLI flag authorizes one configured-resolver A query for example.com.
                dns = await runner.RunAsync(new(Guid.NewGuid(), "example.com", [DiagnosticKind.Dns],
                    new() { ProbeTimeoutMilliseconds = 1000, SessionTimeoutSeconds = 10, MaximumProbes = 1 }, new(true)));
                await Write(Path.Combine(output, "LOCAL_ONLY-public-dns.json"), dns);
                var exported = await DiagnosticEvidence.ExportAsync(output, DiagnosticEvidence.Preview(dns, redact: true));
                VerifyExport(exported.Directory);
                exports.Add(new(Path.GetRelativePath(output, exported.Directory), Hash(File.ReadAllBytes(exported.ManifestPath))));
            }
        }
        var after = Fingerprint(database);
        Require(before == after, "Original incident payloads/comments changed during diagnostics.");
        var required = sessions.Count == 2 && sessions.All(HasRequiredMeasurements) && baseline is not null && comparison is not null;
        var checkpoint = new Checkpoint(before, sessions.Select(s => s.Request.Id).ToArray(), sessions.Select(SessionHash).ToArray(),
            baseline?.Id, comparison?.Id, comparison is null ? null : Hash(DiagnosticEvidence.CanonicalJson(comparison)), exports.ToArray(), required, dns?.Request.Id);
        await Write(Path.Combine(output, "checkpoint.json"), checkpoint);
        await Write(Path.Combine(output, "report.json"), new
        {
            Version = 1, Utc = DateTimeOffset.UtcNow, Stage = "observe", ConcurrentMonitoring = concurrency, RequiredLoopbackMeasurementsPassed = required,
            Runtime = RuntimeInformation.FrameworkDescription, Platform = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), ApplicationVersion = sessions[0].ApplicationVersion,
            IncidentPayloadsUnchanged = before == after, OriginalIncidents = before.Count, OriginalComments = before.Comments,
            IncidentLink = before.FirstId.HasValue ? "Linked the first existing genuine incident" : "No existing incident; none manufactured",
            Sessions = sessions.Select(s => new { Id = s.Request.Id, State = s.State.ToString(), IncidentId = s.Request.IncidentId,
                Results = s.Results.Select(r => new { Test = r.Kind.ToString(), Outcome = r.Outcome.ToString(), r.ElapsedMilliseconds,
                    Samples = r.Statistics is { } stats ? new { stats.Attempted, stats.Completed, stats.Successful, stats.Unanswered,
                        stats.MinimumMilliseconds, stats.AverageMilliseconds, stats.MaximumMilliseconds, stats.StandardDeviationMilliseconds, stats.JitterMilliseconds, stats.LossPercent, stats.Partial } : null,
                    HopCount = r.Hops?.Count, InterfaceCount = r.Network?.Interfaces.Count }) }),
            BaselineId = baseline?.Id, ComparisonId = comparison?.Id,
            PublicDnsRequested = publicDns, PublicDnsOutcome = dns?.Results.FirstOrDefault()?.Outcome.ToString(),
            Exports = exports, Before = before, After = after,
            Limitations = new[] { "Engineering Core harness, not a packaged executable or native UI acceptance.",
                "Loopback verifies destination ICMP/TTL behavior, not a multi-hop Internet path or IPv6.",
                "Snapshot is read-only; default-route availability and OS permission limitations remain in local persisted evidence.",
                "No outage or incident was manufactured. Source data and raw database/logs are local-only; redacted exports can be reviewed separately." }
        });
        Require(required, "Required real loopback measurements were missing; partial results and redacted exports were preserved.");
        Console.WriteLine("REAL_DIAGNOSTIC_OBSERVE=PASS");
    }

    private static async Task<object> ConcurrentMonitoring(string output)
    {
        await using var store = new SqliteHistoryStore(Path.Combine(output, "concurrency.db"));
        await using var engine = new MonitoringService(history: store);
        await using var runner = new DiagnosticRunner(store);
        await engine.StartAsync(new(1, [new(Guid.NewGuid(), "Real loopback monitoring", true, 250, 2000, 500,
            [new(Guid.NewGuid(), "Loopback", "127.0.0.1")])]));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(3000);
        var baselineSeconds = clock.Elapsed.TotalSeconds;
        var baselinePolls = engine.Diagnostics.CompletedPolls;
        var before = engine.Diagnostics.CompletedPolls;
        clock.Restart();
        var runs = new[] { "127.0.0.1", "localhost" }.Select(target => runner.RunAsync(new(Guid.NewGuid(), target,
            [DiagnosticKind.PingQuality], new() { SampleCount = 50, IntervalMilliseconds = 50, ProbeTimeoutMilliseconds = 500, SessionTimeoutSeconds = 10 }, new(true)))).ToArray();
        var maximumWorkers = 0;
        while (clock.Elapsed < TimeSpan.FromSeconds(3))
        {
            maximumWorkers = Math.Max(maximumWorkers, engine.Diagnostics.ActiveGroupWorkers);
            await Task.Delay(10);
        }
        var concurrentSeconds = clock.Elapsed.TotalSeconds;
        var concurrentPolls = engine.Diagnostics.CompletedPolls - before;
        var sessions = await Task.WhenAll(runs);
        await engine.StopAsync();
        Require(baselinePolls >= 3 && concurrentPolls >= 3 && maximumWorkers <= 4 && !engine.HasUnsavedEvidence && engine.Capture().Outages.Count == 0,
            "Monitoring liveness, worker cap or persistence changed while diagnostics ran.");
        Require(sessions.All(s => s.State == DiagnosticState.Completed), "Concurrent loopback diagnostics did not complete successfully.");
        return new { BaselinePolls = baselinePolls, BaselineSeconds = baselineSeconds, ConcurrentPolls = concurrentPolls,
            ConcurrentSeconds = concurrentSeconds, ObservedMaximumGroupWorkers = maximumWorkers, DiagnosticSessions = sessions.Length,
            UnsavedEvidence = engine.HasUnsavedEvidence, Outages = engine.Capture().Outages.Count,
            Scope = "Actual loopback ICMP, production monitoring and two diagnostics sharing SQLite. Short liveness/sample-rate observation; not physical-network throughput or a performance benchmark." };
    }

    private static async Task Reload(string output)
    {
        var checkpoint = JsonSerializer.Deserialize<Checkpoint>(await File.ReadAllTextAsync(Path.Combine(output, "checkpoint.json")))!;
        var database = Path.Combine(output, "data", "history.v1.db");
        var before = Fingerprint(database); Require(before == checkpoint.Before, "Incident evidence changed before restart.");
        await using (var store = new SqliteHistoryStore(database))
        {
            await store.InitializeDiagnosticsAsync();
            for (var i = 0; i < checkpoint.Sessions.Length; i++)
            {
                var session = await store.FindDiagnosticSessionAsync(checkpoint.Sessions[i]) ?? throw new IOException("Saved session missing after reopening.");
                Require(SessionHash(session) == checkpoint.SessionHashes[i], "Persisted session measurements changed after reopening.");
                Require(session.Request.IncidentId == checkpoint.Before.FirstId, "Incident link did not survive restart.");
                Require(HasRequiredMeasurements(session), "Reopened session lacks required actual measurements.");
            }
            Require(checkpoint.BaselineId.HasValue && (await store.ReadDiagnosticBaselinesAsync(100)).Any(b => b.Id == checkpoint.BaselineId && b.SessionId == checkpoint.Sessions[0]), "Baseline did not survive restart.");
            if (checkpoint.DnsSession is { } dnsId) Require(await store.FindDiagnosticSessionAsync(dnsId) is not null, "Opt-in DNS session did not survive restart.");
        }
        using (var connection = ReadOnly(database))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT payload FROM diagnostic_comparisons WHERE id=$id"; command.Parameters.AddWithValue("$id", checkpoint.ComparisonId?.ToString());
            var payload = command.ExecuteScalar() as string ?? throw new IOException("Comparison did not survive restart.");
            var comparison = JsonSerializer.Deserialize<DiagnosticComparison>(payload)!;
            Require(Hash(DiagnosticEvidence.CanonicalJson(comparison)) == checkpoint.ComparisonHash, "Comparison changed after restart.");
        }
        foreach (var export in checkpoint.Exports)
        {
            var directory = Path.Combine(output, export.Directory); VerifyExport(directory);
            Require(Hash(File.ReadAllBytes(Path.Combine(directory, "manifest.json"))) == export.ManifestSha256, "Export manifest changed after restart.");
        }
        var after = Fingerprint(database); Require(after == checkpoint.Before, "Original incident evidence changed on reopen.");
        await Write(Path.Combine(output, "reload-report.json"), new { Version = 1, Utc = DateTimeOffset.UtcNow, Passed = true,
            SeparateProcessRestart = true, ExactSessionsAndResultsReloaded = true, BaselineAndComparisonReloaded = true,
            OriginalIncidentsAndCommentsUnchanged = true, ManifestAndExportHashesVerified = true, Before = before, After = after });
        Console.WriteLine("REAL_DIAGNOSTIC_RESTART_EXPORT_HASHES=PASS");
    }

    private static bool HasRequiredMeasurements(DiagnosticSession session) => session.State == DiagnosticState.Completed &&
        session.Results.Count == 4 && session.Results.All(r => r.Outcome == DiagnosticOutcome.Success) &&
        session.Results.Any(r => r.Kind == DiagnosticKind.PingQuality && r.Statistics is { Attempted: 3, Completed: 3, Successful: 3, Partial: false, AverageMilliseconds: not null }) &&
        session.Results.Any(r => r.Kind == DiagnosticKind.TraceRoute && r.Hops is { Count: > 0 } hops && hops.Any(h => h.Addresses.Contains("127.0.0.1") && h.RttMilliseconds.Count > 0)) &&
        session.Results.Any(r => r.Kind == DiagnosticKind.NetworkConfiguration && r.Network is { Interfaces.Count: > 0 });

    private static async Task Accept(TcpListener listener, CancellationToken cancellation)
    {
        try { while (!cancellation.IsCancellationRequested) { using var socket = await listener.AcceptSocketAsync(cancellation); } }
        catch (Exception ex) when (cancellation.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    private static IncidentFingerprint Fingerprint(string path)
    {
        using var payloads = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); using var comments = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var count = 0; var withComments = 0; Guid? first = null;
        if (File.Exists(path))
        {
            using var connection = ReadOnly(path); using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='outages'";
            if (Convert.ToInt32(command.ExecuteScalar()) > 0)
            {
                command.CommandText = "SELECT id,payload,recovered,observed_end,last_observed FROM outages ORDER BY id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    count++; first ??= Guid.Parse(reader.GetString(0));
                    for (var column = 0; column < 5; column++) Append(payloads, reader.IsDBNull(column) ? "<null>" : Convert.ToString(reader.GetValue(column), System.Globalization.CultureInfo.InvariantCulture)!);
                    using var doc = JsonDocument.Parse(reader.GetString(1));
                    var comment = doc.RootElement.TryGetProperty("Comment", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
                    if (comment.Length > 0) withComments++;
                    Append(comments, reader.GetString(0)); Append(comments, comment);
                }
            }
        }
        return new(count, withComments, Convert.ToHexStringLower(payloads.GetHashAndReset()), Convert.ToHexStringLower(comments.GetHashAndReset()), first);
        static void Append(IncrementalHash hash, string value) { var bytes = Encoding.UTF8.GetBytes(value); hash.AppendData(BitConverter.GetBytes(bytes.Length)); hash.AppendData(bytes); }
    }

    private static SqliteConnection ReadOnly(string path)
    { var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()); connection.Open(); return connection; }
    private static void VerifyExport(string directory)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.json")));
        var files = document.RootElement.GetProperty("Files").EnumerateArray().ToArray();
        Require(files.Length == 3 && files.Select(f => f.GetProperty("File").GetString()!).ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] { "evidence.json", "evidence.md", "evidence.pdf" }), "Export manifest must contain JSON, Markdown and PDF exactly once.");
        foreach (var entry in files)
        {
            var file = entry.GetProperty("File").GetString()!;
            Require(file is "evidence.json" or "evidence.md" or "evidence.pdf", "Unexpected manifest path.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, file));
            Require(bytes.Length == entry.GetProperty("Bytes").GetInt32() && Hash(bytes) == entry.GetProperty("Sha256").GetString(), "Export bytes/hash mismatch.");
            if (file == "evidence.pdf") Require(Encoding.ASCII.GetString(bytes).StartsWith("%PDF-1.4\n", StringComparison.Ordinal) && Encoding.ASCII.GetString(bytes).EndsWith("%%EOF\n", StringComparison.Ordinal), "PDF envelope is invalid.");
        }
        using var evidence = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "evidence.json")));
        Require(evidence.RootElement.GetProperty("Redacted").GetBoolean(), "Default export was not redacted.");
        Require(evidence.RootElement.GetProperty("Session").GetProperty("Request").GetProperty("Target").GetString() == "[redacted target]", "Export target was not redacted.");
    }
    private static string SessionHash(DiagnosticSession session) => Hash(DiagnosticEvidence.CanonicalJson(session));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static Task Write<T>(string path, T value) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json) + "\n");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
