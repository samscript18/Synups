using Synups.Core.Monitoring;

namespace Synups.Desktop;

public sealed record CopyValue(string Column, string Value);
public static class ClipboardText
{
    public static string Row(IEnumerable<CopyValue> fields) => string.Join('\t', fields.Select(f => Escape(f.Value)));
    private static string Escape(string value) => value.IndexOfAny(['\t', '\r', '\n', '"']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}

public sealed partial class TargetRow
{
    public IReadOnlyList<CopyValue> CopyValues =>
    [
        new("Name", Name), new("Address", Address), new("Status", Status),
        new("Checked UTC", lastSample?.CheckedAtUtc?.ToString("O") ?? "Not observed"),
        new("Last success UTC", lastSample?.LastSuccessUtc?.ToString("O") ?? "Not observed"),
        new("Response ms", lastSample?.Result is { Outcome: ProbeOutcome.Success, LatencyMilliseconds: { } latency }
            ? latency.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Not available"),
        new("Freshness", lastSample?.CheckedAtUtc is null ? "Not checked" : lastSample.CheckedInLatestCycle ? "Latest completed cycle" : "Stale / not probed in latest cycle"),
        new("Details", Details), new("Target ID", Id.ToString()),
    ];
}
public sealed partial class OutageRow
{
    public IReadOnlyList<CopyValue> CopyValues =>
    [
        new("Group", record.GroupName), new("State", State), new("Confirmed UTC", record.StartedUtc.ToString("O")),
        new("First failure UTC", record.FirstFailureUtc.ToString("O")), new("Recovery UTC", record.RecoveredUtc?.ToString("O") ?? "Not observed"),
        new("Observation ended UTC", record.ObservationEndedUtc?.ToString("O") ?? "Not ended"), new("Observed duration", record.Elapsed.ToString("g")),
        new("Since previous recovery", record.SincePrevious?.ToString("g") ?? "Not observed"),
        new("Comment", record.Comment), new("Incident ID", record.Id.ToString()), new("Session ID", record.SessionId.ToString()),
        new("Observation gaps", record.ContainsObservationGap.ToString()),
    ];
}
public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<string> TargetCopyColumns { get; } = ["Name", "Address", "Status", "Checked UTC", "Last success UTC", "Response ms", "Freshness", "Details", "Target ID", "Group", "Group status", "Group timing", "Last confirmed change"];
    public IReadOnlyList<string> IncidentCopyColumns { get; } = ["Group", "State", "Confirmed UTC", "First failure UTC", "Recovery UTC", "Observation ended UTC", "Observed duration", "Since previous recovery", "Comment", "Incident ID", "Session ID", "Observation gaps"];
    private string targetCopyColumn = "Address", incidentCopyColumn = "Comment";
    public string TargetCopyColumn { get => targetCopyColumn; set { if (TargetCopyColumns.Contains(value)) Set(ref targetCopyColumn, value); } }
    public string IncidentCopyColumn { get => incidentCopyColumn; set { if (IncidentCopyColumns.Contains(value)) Set(ref incidentCopyColumn, value); } }
    public IReadOnlyList<CopyValue> SelectedTargetValues => SelectedTarget is null ? [] : SelectedTarget.CopyValues.Concat(new CopyValue[]
    { new("Group", SelectedGroup?.Name ?? "Not selected"), new("Group status", SelectedGroup?.Status ?? "Not selected"),
      new("Group timing", SelectedGroup?.TimingDescription ?? "Not selected"), new("Last confirmed change", SelectedGroup?.TransitionDescription ?? "Not observed") }).ToArray();
    public string? SelectedTargetValue => SelectedTargetValues.FirstOrDefault(v => v.Column == TargetCopyColumn)?.Value;
    public string? SelectedTargetRow => SelectedTarget is null ? null : ClipboardText.Row(SelectedTargetValues);
    public string? SelectedIncidentValue => SelectedOutage?.CopyValues.FirstOrDefault(v => v.Column == IncidentCopyColumn)?.Value;
    public string? SelectedIncidentRow => SelectedOutage is null ? null : ClipboardText.Row(SelectedOutage.CopyValues);
}
