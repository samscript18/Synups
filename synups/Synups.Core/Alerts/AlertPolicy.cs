using Synups.Core.Monitoring;
using Synups.Core.Persistence;

namespace Synups.Core.Alerts;

public sealed record AlertMessage(Guid EventId, string Title, string Body, MonitorEventKind Kind);
public sealed class AlertPolicy(IMonitorClock? clock = null)
{
    private readonly IMonitorClock clock = clock ?? new MonitorClock();
    private long lastSequence;
    private readonly Dictionary<Guid, long> lastError = new();
    public void IgnoreExisting(IEnumerable<MonitorEvent> events) => lastSequence = events.Select(e => e.Sequence).DefaultIfEmpty(0).Max();
    public IReadOnlyList<AlertMessage> Select(IEnumerable<MonitorEvent> events, AppPreferences preferences)
    {
        var selected = new List<AlertMessage>();
        foreach (var e in events.OrderBy(e => e.Sequence))
        {
            if (e.Sequence <= lastSequence) continue;
            lastSequence = e.Sequence;
            var enabled = e.Kind switch
            {
                MonitorEventKind.OutageStarted => preferences.NotifyOutages,
                MonitorEventKind.Recovery => preferences.NotifyRecovery,
                MonitorEventKind.ProbeError => preferences.NotifyErrors,
                _ => false,
            };
            if (!enabled) continue;
            if (e.Kind == MonitorEventKind.ProbeError)
            {
                var key = e.GroupId ?? Guid.Empty;
                if (lastError.TryGetValue(key, out var at) && this.clock.Elapsed(at) < TimeSpan.FromMinutes(1)) continue;
                lastError[key] = this.clock.Timestamp;
            }
            selected.Add(new(e.Id, e.Kind == MonitorEventKind.OutageStarted ? "Synups: ICMP outage confirmed" :
                e.Kind == MonitorEventKind.Recovery ? "Synups: ICMP connectivity recovered" : "Synups: monitoring error", e.Message, e.Kind));
        }
        return selected;
    }
}

public sealed record WorkArea(int X, int Y, int Width, int Height, double Scale);
public static class DesktopPolicy
{
    public static bool HideOnMinimize(AppPreferences preferences, bool restorableTray) => preferences.MinimizeToTray && restorableTray;
    public static bool StartOnLaunch(AppPreferences preferences, bool validProfileAndHistory) => preferences.MonitorOnLaunch && validProfileAndHistory;
    public static WindowPlacement Clamp(WindowPlacement placement, IReadOnlyList<WorkArea> screens)
    {
        if (screens.Count == 0) return placement with { X = 0, Y = 0, Width = 1000, Height = 720 };
        var screen = screens.FirstOrDefault(s => placement.X >= s.X && placement.Y >= s.Y && placement.X < s.X + s.Width && placement.Y < s.Y + s.Height) ?? screens[0];
        var width = Math.Clamp(placement.Width, Math.Min(820, screen.Width / screen.Scale), screen.Width / screen.Scale);
        var height = Math.Clamp(placement.Height, Math.Min(560, screen.Height / screen.Scale), screen.Height / screen.Scale);
        return placement with { Width = width, Height = height, X = Math.Clamp(placement.X, screen.X, screen.X + screen.Width - (int)(width * screen.Scale)),
            Y = Math.Clamp(placement.Y, screen.Y, screen.Y + screen.Height - (int)(height * screen.Scale)) };
    }
}
