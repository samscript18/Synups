namespace Synups.Core.Monitoring;

public sealed record MonitoringDiagnostics(int ActiveGroupWorkers, long CompletedPolls, int PendingEvidenceBatches);

public enum GroupConnectivity { NotChecked, Connected, Disconnected, Unavailable, Disabled }
public enum SessionState { Stopped, Running, Stopping, Faulted }
public enum MonitorEventKind { SessionStarted, SessionStopped, OutageStarted, Recovery, ProbeError, ObservationGap, Comment, Interrupted }

public sealed record TargetSnapshot(Guid Id, ProbeResult Result, DateTimeOffset? CheckedAtUtc,
    DateTimeOffset? LastSuccessUtc, bool CheckedInLatestCycle);
public sealed record GroupSnapshot(Guid Id, string Name, GroupConnectivity Connectivity, bool ConfirmedOutage,
    DateTimeOffset? LastMeasurementUtc, IReadOnlyList<TargetSnapshot> Targets);
public sealed record OutageRecord(Guid Id, Guid SessionId, Guid GroupId, string GroupName,
    DateTimeOffset StartedUtc, DateTimeOffset FirstFailureUtc, DateTimeOffset? RecoveredUtc,
    DateTimeOffset? ObservationEndedUtc, TimeSpan Elapsed, bool ContainsObservationGap,
    TimeSpan? SincePrevious, string Comment = "");
public sealed record MonitorEvent(long Sequence, Guid SessionId, Guid? GroupId, DateTimeOffset Utc,
    MonitorEventKind Kind, string Message)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
public sealed record MonitoringSnapshot(Guid? SessionId, SessionState State, IReadOnlyList<GroupSnapshot> Groups,
    IReadOnlyList<OutageRecord> Outages, IReadOnlyList<MonitorEvent> Events, long HistoryRevision,
    DateTimeOffset? LastMeasurementUtc, string? Error, IReadOnlyList<SessionRecord>? Sessions = null);
public sealed record GroupPollResult(GroupSnapshot Snapshot, OutageRecord? ChangedOutage,
    bool OutageStarted, bool Recovered, IReadOnlyList<string> Errors, bool ObservationGap);

public sealed record SessionRecord(Guid Id, DateTimeOffset StartedUtc, DateTimeOffset? EndedUtc,
    DateTimeOffset? LastObservedUtc, TimeSpan Elapsed, bool Interrupted, bool ContainsObservationGap);
