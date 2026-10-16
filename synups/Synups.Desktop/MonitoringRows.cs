using System.Collections.ObjectModel;
using Synups.Core.Monitoring;

namespace Synups.Desktop;

public sealed class GroupRow(GroupConfiguration configuration) : ObservableObject
{
    public GroupConfiguration Configuration { get; } = configuration;
    public Guid Id => Configuration.Id;
    public string Name => Configuration.Name;
    public string TimingDescription => $"Poll {Configuration.PollMilliseconds / 1000d:g}s · timeout {Configuration.TimeoutMilliseconds}ms · fail after {Configuration.FailMilliseconds / 1000d:g}s";
    public ObservableCollection<TargetRow> Targets { get; } = new(configuration.Targets.Select(t => new TargetRow(t)));
    private string status = configuration.Enabled ? "Not checked" : "Disabled";
    public string Status { get => status; private set => Set(ref status, value); }
    public override string ToString() => Name;
    public void Apply(GroupSnapshot? snapshot, bool running)
    {
        Status = snapshot is null ? Configuration.Enabled ? "Not checked" : "Disabled" :
            $"{(!running && snapshot.Connectivity != GroupConnectivity.Disabled ? "Last: " : "")}{snapshot.Connectivity}" +
            (snapshot.ConfirmedOutage ? " · outage unresolved" : snapshot.Connectivity == GroupConnectivity.Disconnected ? " · failure grace period" : "");
        foreach (var target in Targets) target.Apply(snapshot?.Targets.FirstOrDefault(t => t.Id == target.Id), running);
    }
}

public sealed class TargetRow(TargetConfiguration configuration) : ObservableObject
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
    public void Apply(TargetSnapshot? sample, bool running)
    {
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

public sealed class OutageRow(OutageRecord record) : ObservableObject
{
    private OutageRecord record = record;
    public Guid Id => record.Id;
    public string GroupName => record.GroupName;
    public string Comment => record.Comment;
    public string Summary => $"Confirmed {TargetRow.DisplayTime(record.StartedUtc)} · " +
        (record.RecoveredUtc.HasValue ? $"recovered {TargetRow.DisplayTime(record.RecoveredUtc)}" :
            record.ObservationEndedUtc.HasValue ? "unresolved when observation stopped" : "unresolved") +
        $" · elapsed at last observation {record.Elapsed:g}" + (record.ContainsObservationGap ? " (includes uncertain observation gaps)" : "") +
        (record.SincePrevious is { } previous ? $" · since previous recovery {previous:g}" : "");
    public string Session => $"Session {record.SessionId}";
    public void Apply(OutageRecord value)
    {
        if (record == value) return;
        record = value; Changed(nameof(GroupName)); Changed(nameof(Comment)); Changed(nameof(Summary));
    }
    public override string ToString() => $"{GroupName} · {TargetRow.DisplayTime(record.StartedUtc)}";
}
