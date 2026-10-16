using Synups.Core.Persistence;

namespace Synups.Core.Monitoring;

/// <summary>Owns polling workers and serialized evidence writes; presentation reads immutable snapshots.</summary>
public sealed class MonitoringService : IAsyncDisposable
{
    private readonly IConnectivityProbe probe;
    private readonly IMonitorClock clock;
    private readonly IHistoryStore? history;
    private readonly int maxConcurrency;
    private readonly TimeSpan shutdownTimeout;
    private readonly object gate = new();
    private readonly SemaphoreSlim lifecycle = new(1, 1), evidence = new(1, 1);
    private readonly Dictionary<Guid, GroupSnapshot> groups = new();
    private readonly List<OutageRecord> outages = new();
    private readonly List<MonitorEvent> events = new();
    private readonly Dictionary<Guid, SessionRecord> sessions = new();
    private readonly Queue<HistoryBatch> pending = new();
    private List<MonitorEvent>? collecting;
    private CancellationTokenSource? cancellation;
    private Task? runner;
    private SessionState state;
    private Guid? sessionId;
    private long sequence, historyRevision, startedTimestamp;
    private DateTimeOffset? lastMeasurement;
    private string? error, persistenceError;
    private bool disposed, historyLoaded;
    public bool HasDurableHistory => history is not null;
    public bool HistoryReady { get { lock (gate) return history is null || historyLoaded; } }
    public bool HasUnsavedEvidence { get { lock (gate) return pending.Count > 0; } }
    public string? PersistenceError { get { lock (gate) return persistenceError; } }

    public MonitoringService(IConnectivityProbe? probe = null, IMonitorClock? clock = null, int maxConcurrency = 4,
        TimeSpan? shutdownTimeout = null, IHistoryStore? history = null)
    {
        this.probe = probe ?? new IcmpProbe(); this.clock = clock ?? new MonitorClock(); this.history = history;
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(7);
        if (this.shutdownTimeout <= TimeSpan.Zero || this.shutdownTimeout > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
        this.maxConcurrency = maxConcurrency is >= 1 and <= 64 ? maxConcurrency : throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
    }

    public MonitoringSnapshot Capture()
    {
        lock (gate) return new(sessionId, state, groups.Values.ToArray(), outages.ToArray(), events.ToArray(), historyRevision,
            lastMeasurement, persistenceError ?? error, sessions.Values.ToArray());
    }
    public async Task InitializeHistoryAsync()
    {
        await evidence.WaitAsync().ConfigureAwait(false);
        try
        {
            if (history is null || historyLoaded) return;
            var saved = await history.LoadAsync().ConfigureAwait(false);
            lock (gate)
            {
                outages.AddRange(saved.Outages); events.AddRange(saved.Events); sequence = saved.LastSequence;
                foreach (var session in saved.Sessions) sessions[session.Id] = session;
                historyLoaded = true; persistenceError = null; historyRevision++;
            }
        }
        catch (Exception ex) { lock (gate) persistenceError = $"History unavailable; existing files preserved: {ex.Message}"; throw; }
        finally { evidence.Release(); }
    }
    public async Task StartAsync(MonitorConfiguration configuration)
    {
        var config = configuration.ValidatedCopy();
        if (!config.Groups.Any(g => g.Enabled)) throw new ArgumentException("Enable at least one group with a target before starting.");
        await lifecycle.WaitAsync().ConfigureAwait(false);
        var began = false;
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (runner is { IsCompleted: false }) throw new InvalidOperationException("Monitoring is already running or still stopping.");
            await InitializeHistoryAsync().ConfigureAwait(false);
            await RetryPersistenceAsync().ConfigureAwait(false);
            cancellation?.Dispose(); cancellation = new();
            var id = Guid.NewGuid();
            var monitors = config.Groups.Where(g => g.Enabled).Select(g => new GroupMonitor(g, id, probe, clock)).ToArray();
            began = true;
            await ChangeAsync(() =>
            {
                sessionId = id; state = SessionState.Running; error = null; lastMeasurement = null; groups.Clear(); startedTimestamp = clock.Timestamp;
                sessions[id] = new(id, clock.UtcNow, null, null, TimeSpan.Zero, false, false);
                foreach (var g in config.Groups)
                    groups.Add(g.Id, g.Enabled ? monitors.Single(m => m.Snapshot.Id == g.Id).Snapshot :
                        new(g.Id, g.Name, GroupConnectivity.Disabled, false, null,
                            g.Targets.Select(t => new TargetSnapshot(t.Id, new(ProbeOutcome.NotChecked), null, null, false)).ToArray()));
                AddEvent(null, MonitorEventKind.SessionStarted, "Monitoring session started by user or explicit launch preference.");
            }, []).ConfigureAwait(false);
            var token = cancellation.Token;
            runner = Task.Run(() => RunSessionAsync(monitors, token));
        }
        catch when (began) { lock (gate) state = SessionState.Faulted; throw; }
        finally { lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        if (runner is { IsCompleted: false })
        {
            lock (gate) state = SessionState.Stopping;
            cancellation!.Cancel();
            try { await runner.WaitAsync(shutdownTimeout).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                lock (gate) error = "Still waiting for a worker to drain. Restart is blocked; retry Stop.";
                throw new TimeoutException("A monitoring worker did not drain within the shutdown deadline. It remains owned; retry Stop before closing.");
            }
        }
        if (runner is null && sessionId is { } id && sessions[id].EndedUtc is null)
            await ChangeAsync(() =>
            {
                sessions[id] = sessions[id] with { EndedUtc = clock.UtcNow }; state = SessionState.Stopped;
                AddEvent(null, MonitorEventKind.SessionStopped, "Session ended before probes could start.");
            }, []).ConfigureAwait(false);
        await RetryPersistenceAsync().ConfigureAwait(false);
    }
    private async Task RunSessionAsync(GroupMonitor[] monitors, CancellationToken token)
    {
        using var slots = new SemaphoreSlim(maxConcurrency);
        try { await Task.WhenAll(monitors.Select(m => RunGroupAsync(m, slots, token))).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { lock (gate) { error = $"Monitoring stopped: {ex.Message}"; state = SessionState.Faulted; } cancellation!.Cancel(); }
        finally
        {
            try
            {
                Guid[] active;
                lock (gate) active = outages.Where(o => o.SessionId == sessionId && o.RecoveredUtc is null && o.ObservationEndedUtc is null).Select(o => o.Id).ToArray();
                await ChangeAsync(() =>
                {
                    var session = sessions[sessionId!.Value];
                    foreach (var id in active)
                    {
                        var i = outages.FindIndex(o => o.Id == id);
                        outages[i] = outages[i] with { ObservationEndedUtc = session.LastObservedUtc ?? outages[i].StartedUtc };
                    }
                    sessions[session.Id] = session with { EndedUtc = clock.UtcNow };
                    if (state != SessionState.Faulted) { state = SessionState.Stopped; error = null; }
                    AddEvent(null, MonitorEventKind.SessionStopped, "Monitoring stopped; unresolved outages were not marked recovered.");
                }, active).ConfigureAwait(false);
            }
            catch { lock (gate) state = SessionState.Faulted; } // Pending evidence remains owned for retry; close refuses to discard it.
        }
    }
    private async Task RunGroupAsync(GroupMonitor monitor, SemaphoreSlim slots, CancellationToken token)
    {
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var delay = monitor.TimeUntilDue;
                if (delay > TimeSpan.Zero) await clock.DelayAsync(delay, token).ConfigureAwait(false);
                await slots.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var result = await monitor.PollAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await ChangeAsync(() =>
                    {
                        groups[result.Snapshot.Id] = result.Snapshot;
                        if (result.Snapshot.LastMeasurementUtc is { } measured) lastMeasurement = measured;
                        var session = sessions[sessionId!.Value];
                        sessions[session.Id] = session with { LastObservedUtc = lastMeasurement, Elapsed = clock.Elapsed(startedTimestamp), ContainsObservationGap = session.ContainsObservationGap || result.ObservationGap };
                        if (result.ChangedOutage is { } changed)
                        {
                            var index = outages.FindIndex(o => o.Id == changed.Id);
                            if (index < 0) outages.Add(changed); else outages[index] = changed with { Comment = outages[index].Comment };
                            historyRevision++;
                        }
                        if (result.OutageStarted) AddEvent(result.Snapshot.Id, MonitorEventKind.OutageStarted, $"{result.Snapshot.Name}: ICMP outage confirmed.");
                        if (result.Recovered) AddEvent(result.Snapshot.Id, MonitorEventKind.Recovery, $"{result.Snapshot.Name}: ICMP connectivity recovered.");
                        if (result.ObservationGap) AddEvent(result.Snapshot.Id, MonitorEventKind.ObservationGap, $"{result.Snapshot.Name}: observation gap; failure grace period reset.");
                        foreach (var message in result.Errors) AddEvent(result.Snapshot.Id, MonitorEventKind.ProbeError, message);
                    }, result.ChangedOutage is { } changedOutage ? [changedOutage.Id] : []).ConfigureAwait(false);
                }
                finally { slots.Release(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { cancellation!.Cancel(); throw; }
    }
    public Task UpdateCommentAsync(Guid outageId, string comment)
    {
        if (HasUnsavedEvidence) throw new IOException("Retry pending evidence before editing comments.");
        if (comment.Length > 4000) throw new ArgumentException("Comments are limited to 4000 characters.");
        return ChangeAsync(() =>
        {
            var i = outages.FindIndex(o => o.Id == outageId);
            if (i < 0) throw new ArgumentException("Outage record no longer exists.");
            outages[i] = outages[i] with { Comment = comment };
            AddEvent(outages[i].GroupId, MonitorEventKind.Comment, $"Outage {outageId} comment: {comment}", outages[i].SessionId);
        }, [outageId]);
    }
    private async Task ChangeAsync(Action change, Guid[] changedIds)
    {
        await evidence.WaitAsync().ConfigureAwait(false);
        try
        {
            HistoryBatch batch;
            lock (gate)
            {
                collecting = new(); change();
                batch = new(outages.Where(o => changedIds.Contains(o.Id)).ToArray(), collecting.ToArray(), sessionId.HasValue ? sessions[sessionId.Value] : null);
                collecting = null;
                if (history is not null) pending.Enqueue(batch);
            }
            await FlushCoreAsync().ConfigureAwait(false);
        }
        finally { evidence.Release(); }
    }
    private async Task FlushCoreAsync()
    {
        if (history is null) return;
        while (true)
        {
            HistoryBatch batch;
            lock (gate) { if (pending.Count == 0) { persistenceError = null; return; } batch = pending.Peek(); }
            try { await history.CommitAsync(batch).ConfigureAwait(false); }
            catch (Exception ex)
            {
                lock (gate) persistenceError = $"History write failed. Monitoring must stop; unsaved evidence is retained for Retry. {ex.Message}";
                cancellation?.Cancel(); throw new IOException(persistenceError, ex);
            }
            lock (gate) pending.Dequeue();
        }
    }
    public async Task RetryPersistenceAsync()
    {
        await evidence.WaitAsync().ConfigureAwait(false);
        try { await FlushCoreAsync().ConfigureAwait(false); }
        finally { evidence.Release(); }
    }
    public async Task<HistoryState> ReadArchiveAsync()
    {
        await evidence.WaitAsync().ConfigureAwait(false);
        try
        {
            await FlushCoreAsync().ConfigureAwait(false);
            if (history is not null) return await history.LoadAsync(includeAllEvents: true).ConfigureAwait(false);
            lock (gate) return new(outages.ToArray(), events.ToArray(), sessions.Values.ToArray(), sequence);
        }
        finally { evidence.Release(); }
    }
    public async Task ApplyRetentionAsync(int days)
    {
        if (runner is { IsCompleted: false }) throw new InvalidOperationException("Stop before applying history retention.");
        if (history is null) return;
        await evidence.WaitAsync().ConfigureAwait(false);
        try
        {
            await history.RetainAsync(days, clock.UtcNow).ConfigureAwait(false);
            await FlushCoreAsync().ConfigureAwait(false);
            var saved = await history.LoadAsync().ConfigureAwait(false);
            lock (gate) { outages.Clear(); outages.AddRange(saved.Outages); events.Clear(); events.AddRange(saved.Events); sessions.Clear(); foreach (var s in saved.Sessions) sessions[s.Id] = s; historyRevision++; }
        }
        finally { evidence.Release(); }
    }
    private void AddEvent(Guid? group, MonitorEventKind kind, string message, Guid? eventSession = null)
    {
        var e = new MonitorEvent(++sequence, eventSession ?? sessionId!.Value, group, clock.UtcNow, kind, message);
        events.Add(e); collecting?.Add(e);
        if (events.Count > 500) events.RemoveAt(0);
        historyRevision++;
    }
    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            await StopCoreAsync().ConfigureAwait(false);
            if (history is not null) await history.DisposeAsync().ConfigureAwait(false);
            disposed = true; cancellation?.Dispose();
        }
        finally { lifecycle.Release(); }
    }
}
