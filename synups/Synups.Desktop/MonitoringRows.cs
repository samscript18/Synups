using System.Collections.ObjectModel;
using Synups.Core.Monitoring;

namespace Synups.Desktop;

public sealed record WorkspacePage(string Title, string Description, string IconData)
{
    public override string ToString() => Title;
}

public sealed partial class GroupRow(GroupConfiguration configuration) : ObservableObject
{
    public GroupConfiguration Configuration { get; } = configuration;
    public Guid Id => Configuration.Id;
    public string Name => Configuration.Name;
    public string TimingDescription => $"Poll {Configuration.PollMilliseconds / 1000d:g}s · timeout {Configuration.TimeoutMilliseconds}ms · fail after {Configuration.FailMilliseconds / 1000d:g}s";
    public string TargetDescription => $"{Targets.Count} {(Targets.Count == 1 ? "target" : "targets")} · one responding peer establishes group connectivity";
    public ObservableCollection<TargetRow> Targets { get; } = new(configuration.Targets.Select(t => new TargetRow(t)));
    private string status = configuration.Enabled ? "Not checked" : "Disabled";
    public string Status { get => status; private set => Set(ref status, value); }
    private GroupConnectivity connectivity = configuration.Enabled ? GroupConnectivity.NotChecked : GroupConnectivity.Disabled;
    private bool running, outage;
    public bool IsConnected => running && connectivity == GroupConnectivity.Connected && !outage;
    public bool HasOutage => outage;
    public bool IsUncertain => running && !outage && connectivity is GroupConnectivity.Unavailable or GroupConnectivity.Disconnected;
    private string transitionDescription = "No confirmed failure/recovery observed in this session.";
    public string TransitionDescription { get => transitionDescription; private set => Set(ref transitionDescription, value); }
    public override string ToString() => Name;
    public void Apply(GroupSnapshot? snapshot, bool running)
    {
        TransitionDescription = snapshot?.ConfirmedStateChangedUtc is { } changed
            ? $"Last confirmed change {TargetRow.DisplayTime(changed)} · observed time since change {snapshot.SinceConfirmedStateChange:g}"
            : "No confirmed failure/recovery observed in this session.";
        var nextConnectivity = snapshot?.Connectivity ?? (Configuration.Enabled ? GroupConnectivity.NotChecked : GroupConnectivity.Disabled);
        var nextOutage = snapshot?.ConfirmedOutage == true;
        if (connectivity != nextConnectivity || this.running != running || outage != nextOutage)
        {
            connectivity = nextConnectivity; this.running = running; outage = nextOutage;
            Changed(nameof(IsConnected)); Changed(nameof(HasOutage)); Changed(nameof(IsUncertain));
        }
        Status = snapshot is null ? Configuration.Enabled ? "Not checked" : "Disabled" :
            $"{(!running && snapshot.Connectivity != GroupConnectivity.Disabled ? "Last: " : "")}{(snapshot.Connectivity == GroupConnectivity.NotChecked ? "Not checked" : snapshot.Connectivity)}" +
            (snapshot.ConfirmedOutage ? " · outage unresolved" : snapshot.Connectivity == GroupConnectivity.Disconnected ? " · failure grace period" : "");
        foreach (var target in Targets) target.Apply(snapshot?.Targets.FirstOrDefault(t => t.Id == target.Id), running);
    }
}

public sealed partial class TargetRow(TargetConfiguration configuration) : ObservableObject
{
    public TargetConfiguration Configuration { get; } = configuration;
    public Guid Id => Configuration.Id;
    public string Name => Configuration.Name;
    public string Address => Configuration.Address;
    private string status = "Not checked";
    private string details = "No measurements or successful responses yet.";
    public string Status { get => status; private set => Set(ref status, value); }
    public string Details { get => details; private set => Set(ref details, value); }
    public override string ToString() => $"{Name} · {Address}";
    private TargetSnapshot? lastSample;
    public void Apply(TargetSnapshot? sample, bool running)
    {
        lastSample = sample;
        if (sample?.CheckedAtUtc is null) { Status = "Not checked"; Details = "No measurements or successful responses yet."; return; }
        Status = sample.Result.Outcome switch
        {
            ProbeOutcome.Success => "Responded",
            ProbeOutcome.Failure => "No ICMP connectivity",
            _ => "Unavailable / probe error",
        };
        Status += !running ? " · stopped sample" : sample.CheckedInLatestCycle ? " · latest cycle" : " · stale sample";
        Details = $"Checked {DisplayTime(sample.CheckedAtUtc)} · last success {DisplayTime(sample.LastSuccessUtc)} · " +
            (sample.Result.LatencyMilliseconds is { } latency ? $"response {latency} ms" : "latency unavailable") +
            (sample.Result.Detail is { } detail ? $" · {detail}" : "");
    }
    public static string DisplayTime(DateTimeOffset? time) => time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "Not observed";
}

public sealed partial class OutageRow(OutageRecord record, bool interrupted = false) : ObservableObject
{
    private OutageRecord record = record;
    private bool interrupted = interrupted;
    public Guid Id => record.Id;
    public string GroupName => record.GroupName;
    public string Comment => record.Comment;
    public bool IsRecovered => record.RecoveredUtc.HasValue;
    public bool IsConfirmed => !IsRecovered && record.ObservationEndedUtc is null;
    public bool IsEndedUnresolved => !IsRecovered && !IsConfirmed;
    public string State => IsRecovered ? "Observed recovery" : interrupted ? "Unresolved · interrupted" : IsEndedUnresolved ? "Unresolved · observation ended" : "Confirmed · awaiting recovery";
    public string Details => $"Incident {record.Id}\nGroup {record.GroupName} ({record.GroupId})\nSession {record.SessionId}\n" +
        $"First failed observation UTC: {record.FirstFailureUtc:O}\nConfirmed UTC: {record.StartedUtc:O}\n" +
        $"Recovery UTC: {record.RecoveredUtc?.ToString("O") ?? "Not observed"}\nObservation ended UTC: {record.ObservationEndedUtc?.ToString("O") ?? "Not ended"}\n" +
        $"Observed duration since confirmation: {record.Elapsed:g}\nContains observation gaps: {record.ContainsObservationGap}\nSession interrupted: {interrupted}\nComment: {record.Comment}";
    public string Summary => $"Confirmed {TargetRow.DisplayTime(record.StartedUtc)} · " +
        (record.RecoveredUtc.HasValue ? $"recovered {TargetRow.DisplayTime(record.RecoveredUtc)}" :
            record.ObservationEndedUtc.HasValue ? "unresolved when observation stopped" : "unresolved") +
        $" · elapsed at last observation {record.Elapsed:g}" + (record.ContainsObservationGap ? " (includes uncertain observation gaps)" : "") +
        (record.SincePrevious is { } previous ? $" · since previous recovery {previous:g}" : "");
    public string Session => $"Session {record.SessionId}";
    public void Apply(OutageRecord value, bool? sessionInterrupted = null)
    {
        if (record == value && (sessionInterrupted is null || sessionInterrupted == interrupted)) return;
        record = value; interrupted = sessionInterrupted ?? interrupted;
        foreach (var name in new[] { nameof(GroupName), nameof(Comment), nameof(Summary), nameof(State), nameof(Details), nameof(IsRecovered), nameof(IsConfirmed), nameof(IsEndedUnresolved) }) Changed(name);
    }
    public override string ToString() => $"{GroupName} · {TargetRow.DisplayTime(record.StartedUtc)}";
}
