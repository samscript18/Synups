using Synups.Core.Persistence;

namespace Synups.Desktop;

public sealed record LogNameChoice(LogFilenameStyle Style, string Label) { public override string ToString() => Label; }
public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<LogNameChoice> LogNameChoices { get; } = [new(LogFilenameStyle.StableSessionId, "Stable session IDs"), new(LogFilenameStyle.UtcSessionStart, "UTC session-start timestamps"), new(LogFilenameStyle.LocalSessionStart, "Local session-start timestamps with offset"), new(LogFilenameStyle.FixedArchiveSnapshot, "Fixed combined snapshot + preserved per-session copies")];
    private LogNameChoice? prefLogName;
    public LogNameChoice? PrefLogName { get => prefLogName; set => Set(ref prefLogName, value); }
    private double prefGroupHeight = 200, prefTargetHeight = 360, prefIncidentHeight = 440;
    public double PrefGroupHeight { get => prefGroupHeight; set => Set(ref prefGroupHeight, value); }
    public double PrefTargetHeight { get => prefTargetHeight; set => Set(ref prefTargetHeight, value); }
    public double PrefIncidentHeight { get => prefIncidentHeight; set => Set(ref prefIncidentHeight, value); }
}
