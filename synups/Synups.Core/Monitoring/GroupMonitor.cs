using System.Net;

namespace Synups.Core.Monitoring;

/// <summary>
/// Async port of Bill Menees' WirePeep PeerGroupState/LocationState selection intent (MIT).
/// One owner calls PollAsync at a time; MonitoringService enforces this. UTC is evidence, never elapsed time.
/// </summary>
public sealed class GroupMonitor
{
    private readonly GroupConfiguration config;
    private readonly Guid sessionId;
    private readonly IConnectivityProbe probe;
    private readonly IMonitorClock clock;
    private readonly TargetConfiguration[] targets;
    private readonly Dictionary<Guid, TargetSnapshot> samples = new();
    private readonly Dictionary<Guid, long> lastProbed = new();
    private long cycle = -1;
    private long? lastStarted;
    public TimeSpan TimeUntilDue => lastStarted.HasValue
        ? TimeSpan.FromMilliseconds(config.PollMilliseconds) - clock.Elapsed(lastStarted.Value) : TimeSpan.Zero;
    private long? failedSince, confirmedAt, previousRecovery, lastCompleted;
    private DateTimeOffset? firstFailureUtc;
    private OutageRecord? active;
    public GroupSnapshot Snapshot { get; private set; }

    public GroupMonitor(GroupConfiguration config, Guid sessionId, IConnectivityProbe probe, IMonitorClock clock)
    {
        this.config = config; this.sessionId = sessionId; this.probe = probe; this.clock = clock;
        targets = config.Targets.OrderBy(t => t.Name, StringComparer.CurrentCulture).ToArray();
        foreach (var t in targets) samples.Add(t.Id, new(t.Id, new(ProbeOutcome.NotChecked), null, null, false));
        Snapshot = CreateSnapshot(GroupConnectivity.NotChecked, null);
    }

    public async Task<GroupPollResult> PollAsync(CancellationToken cancellationToken)
    {
        if (lastStarted.HasValue && clock.Elapsed(lastStarted.Value) <= TimeSpan.FromMilliseconds(config.PollMilliseconds - 20))
            return new(Snapshot, null, false, false, Array.Empty<string>(), false);
        lastStarted = clock.Timestamp;
        var pollTimestamp = lastStarted.Value;
        var errors = new List<string>();
        var gap = lastCompleted.HasValue && clock.Elapsed(lastCompleted.Value) >
            TimeSpan.FromMilliseconds(Math.Max(config.PollMilliseconds * 3L, config.PollMilliseconds + 10_000L));
        if (gap) { failedSince = null; firstFailureUtc = null; }
        cycle++;
        foreach (var id in samples.Keys.ToArray()) samples[id] = samples[id] with { CheckedInLatestCycle = false };
        var outcomes = new List<ProbeOutcome>();
        DateTimeOffset? measured = Snapshot.LastMeasurementUtc;
        for (var i = 0; i < targets.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = targets[(int)((cycle + i) % targets.Length)];
            if (lastProbed.TryGetValue(target.Id, out var last) &&
                clock.Elapsed(last) <= TimeSpan.FromMilliseconds(config.PollMilliseconds - 20)) continue;
            ProbeResult result;
            try { result = await probe.ProbeAsync(IPAddress.Parse(target.Address), TimeSpan.FromMilliseconds(config.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { result = IcmpProbe.MapException(ex); }
            cancellationToken.ThrowIfCancellationRequested();
            var now = clock.UtcNow;
            var prior = samples[target.Id];
            lastProbed[target.Id] = pollTimestamp;
            samples[target.Id] = new(target.Id, result, now,
                result.Outcome == ProbeOutcome.Success ? now : prior.LastSuccessUtc, true);
            measured = now;
            outcomes.Add(result.Outcome);
            if (result.Outcome == ProbeOutcome.Unavailable &&
                (prior.Result.Outcome != result.Outcome || prior.Result.Error != result.Error || prior.Result.Detail != result.Detail))
                errors.Add($"{config.Name} / {target.Name}: {result.Detail ?? result.Error.ToString()}");
            if (result.Outcome == ProbeOutcome.Success) break;
        }
        var state = outcomes.Count == 0 ? Snapshot.Connectivity : outcomes.Contains(ProbeOutcome.Success)
            ? GroupConnectivity.Connected : outcomes.Contains(ProbeOutcome.Unavailable) || outcomes.Contains(ProbeOutcome.NotChecked)
                ? GroupConnectivity.Unavailable : GroupConnectivity.Disconnected;
        var utc = clock.UtcNow;
        var started = false;
        var recovered = false;
        OutageRecord? changed = null;
        if (outcomes.Count > 0)
        {
            if (state == GroupConnectivity.Connected)
            {
                failedSince = null; firstFailureUtc = null;
                if (active is not null)
                {
                    changed = active with { RecoveredUtc = utc, Elapsed = clock.Elapsed(confirmedAt!.Value), ContainsObservationGap = active.ContainsObservationGap || gap };
                    active = null; confirmedAt = null; previousRecovery = clock.Timestamp; recovered = true;
                }
            }
            else if (state == GroupConnectivity.Unavailable)
            {
                failedSince = null; firstFailureUtc = null;
                if (active is not null) active = active with { ContainsObservationGap = true };
            }
            else if (state == GroupConnectivity.Disconnected)
            {
                if (failedSince is null) { failedSince = clock.Timestamp; firstFailureUtc = utc; }
                if (active is null && clock.Elapsed(failedSince.Value) >= TimeSpan.FromMilliseconds(config.FailMilliseconds))
                {
                    confirmedAt = clock.Timestamp;
                    active = new(Guid.NewGuid(), sessionId, config.Id, config.Name, utc, firstFailureUtc!.Value,
                        null, null, TimeSpan.Zero, gap, previousRecovery.HasValue ? clock.Elapsed(previousRecovery.Value) : null);
                    started = true;
                }
            }
            if (active is not null)
            {
                active = active with { Elapsed = clock.Elapsed(confirmedAt!.Value), ContainsObservationGap = active.ContainsObservationGap || gap };
                changed = active;
            }
            lastCompleted = clock.Timestamp;
        }
        Snapshot = CreateSnapshot(state, measured);
        return new(Snapshot, changed, started, recovered, errors.AsReadOnly(), gap);
    }

    private GroupSnapshot CreateSnapshot(GroupConnectivity connectivity, DateTimeOffset? measured) =>
        new(config.Id, config.Name, connectivity, active is not null, measured,
            Array.AsReadOnly(targets.Select(t => samples[t.Id]).ToArray()));
}
