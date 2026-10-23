namespace Synups.Core.Diagnostics;

/// <summary>Owns only diagnostic work. Full queues reject explicitly; monitoring has its own workers and budget.</summary>
public sealed class DiagnosticRunner : IDiagnosticRunner
{
    private readonly IDiagnosticRepository repository;
    private readonly IReadOnlyDictionary<DiagnosticKind, IDiagnosticTest> tests;
    private readonly SemaphoreSlim slots;
    private readonly CancellationTokenSource shutdown = new();
    private readonly object gate = new();
    private readonly HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Task<DiagnosticSession>> owned = [];
    private bool stopping, disposed;
    public DiagnosticRunner(IDiagnosticRepository repository, IEnumerable<IDiagnosticTest>? tests = null, int maximumConcurrency = 2)
    {
        this.repository = repository;
        if (maximumConcurrency is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        slots = new(maximumConcurrency);
        this.tests = (tests ?? new IDiagnosticTest[] { new TcpConnectTest(), new DnsDiagnosticTest(), new PingQualityTest(), new TraceRouteTest(), new NetworkConfigurationTest() }).ToDictionary(t => t.Kind);
    }
    public Task<DiagnosticSession> RunAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress = null, CancellationToken cancellation = default)
    {
        request = request.ValidatedCopy();
        cancellation.ThrowIfCancellationRequested();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (stopping) throw new InvalidOperationException("Diagnostics are shutting down.");
            // Backpressure: no unbounded queue, one run per exact canonicalized endpoint and two global runs by default.
            var key = System.Net.IPAddress.TryParse(request.Target, out var ip) ? ip.ToString() : request.Target.TrimEnd('.').ToLowerInvariant();
            if (targets.Contains(key)) throw new InvalidOperationException("A diagnostic for this target is already running.");
            if (!slots.Wait(0)) throw new InvalidOperationException("Diagnostic capacity is busy. Wait for a running session or cancel it.");
            targets.Add(key);
            Task<DiagnosticSession>? task = null;
            task = Task.Run(async () =>
            {
                // Acquisition lock ensures the task is assigned and registered before any completion cleanup.
                lock (gate) { }
                try { return await ExecuteAsync(request, progress, cancellation).ConfigureAwait(false); }
                finally { lock (gate) { owned.Remove(task!); targets.Remove(key); slots.Release(); } }
            });
            owned.Add(task);
            return task;
        }
    }
    private async Task<DiagnosticSession> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, shutdown.Token);
        linked.CancelAfter(TimeSpan.FromSeconds(request.Parameters.SessionTimeoutSeconds));
        var token = linked.Token;
        await repository.InitializeDiagnosticsAsync().ConfigureAwait(false);
        var session = DiagnosticSession.Start(request);
        await repository.SaveDiagnosticSessionAsync(session).ConfigureAwait(false);
        var results = new List<DiagnosticTestResult>();
        foreach (var kind in request.Tests)
        {
            if (token.IsCancellationRequested) break;
            try { progress?.Report(new(request.Id, kind, $"Running {kind}", results.Count, request.Tests.Count)); } catch { /* Presentation cannot change observations. */ }
            var start = DateTimeOffset.UtcNow; var timer = System.Diagnostics.Stopwatch.GetTimestamp();
            DiagnosticTestResult result;
            try
            {
                result = tests.TryGetValue(kind, out var test)
                    ? await test.ExecuteAsync(request, progress, token).ConfigureAwait(false)
                    : new(Guid.NewGuid(), kind, request.Target, start, DateTimeOffset.UtcNow, DiagnosticOutcome.Unsupported,
                        "Capability check; no packets sent", 0, [new(start, "Capability", DiagnosticCertainty.Unknown, Text: "This protocol-specific implementation is unavailable. TCP connect is not a raw SYN or UDP probe.")]);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                result = new(Guid.NewGuid(), kind, request.Target, start, DateTimeOffset.UtcNow, DiagnosticOutcome.Cancelled, "Cancelled before adapter returned measurements",
                    System.Diagnostics.Stopwatch.GetElapsedTime(timer).TotalMilliseconds, [], "No result was invented for the unfinished operation.");
            }
            catch (Exception ex)
            {
                result = new(Guid.NewGuid(), kind, request.Target, start, DateTimeOffset.UtcNow, DiagnosticOutcome.Failed, "Adapter failure",
                    System.Diagnostics.Stopwatch.GetElapsedTime(timer).TotalMilliseconds, [], ex.Message);
            }
            results.Add(result);
            session = session with { Results = results.ToArray() };
            // Persist completed/partial child results before starting another network call. No transaction spans network I/O.
            await repository.SaveDiagnosticSessionAsync(session).ConfigureAwait(false);
            try { progress?.Report(new(request.Id, kind, "Result saved", results.Count, request.Tests.Count, result)); } catch { }
        }
        var cancelled = cancellation.IsCancellationRequested || shutdown.IsCancellationRequested;
        var deadline = token.IsCancellationRequested && !cancelled;
        var final = cancelled ? DiagnosticState.Cancelled : deadline || results.Count < request.Tests.Count || results.Any(r => r.Outcome == DiagnosticOutcome.Cancelled)
            ? DiagnosticState.Partial : results.All(r => r.Outcome == DiagnosticOutcome.Success) ? DiagnosticState.Completed
            : results.Any(r => r.Outcome == DiagnosticOutcome.Success) ? DiagnosticState.Partial : DiagnosticState.Failed;
        session = session with { CompletedUtc = DateTimeOffset.UtcNow, State = final, Analysis = PathAnalysis.AnalyzeEvidence(results), Error = deadline ? "Session deadline reached. Retained completed observations; unattempted probes have no measurements." : null };
        await repository.SaveDiagnosticSessionAsync(session).ConfigureAwait(false);
        try { progress?.Report(new(request.Id, null, session.State.ToString(), results.Count, request.Tests.Count)); } catch { }
        return session;
    }
    public async ValueTask DisposeAsync()
    {
        Task[] tasks;
        lock (gate) { if (disposed) return; stopping = true; shutdown.Cancel(); tasks = owned.ToArray(); }
        var draining = System.Diagnostics.Stopwatch.StartNew();
        try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(7)).ConfigureAwait(false); }
        catch (TimeoutException) { throw new TimeoutException("A diagnostic worker remains owned while its OS call drains. Retry shutdown; it was not abandoned."); }
        catch (Exception) when (tasks.All(t => t.IsCompleted)) { /* Caller has already received each run's persistence error. */ }
        await BoundedHostnameResolver.Shared.DrainAsync(TimeSpan.FromSeconds(7) - draining.Elapsed > TimeSpan.Zero ? TimeSpan.FromSeconds(7) - draining.Elapsed : TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
        lock (gate) { disposed = true; shutdown.Dispose(); slots.Dispose(); }
    }
}
