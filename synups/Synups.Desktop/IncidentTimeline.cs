using Synups.Core.Persistence;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    private Task timelineRead = Task.CompletedTask;
    private bool timelineReading, timelineDirty, timelineClosing;
    private long? timelineCursor, timelineUpper, timelineNext;
    private long pageUpper;
    private long timelineRevision = -1;
    private string timelineMessage = "Loading persisted incident history…";
    public string TimelineMessage { get => timelineMessage; private set => Set(ref timelineMessage, value); }
    public bool CanReadTimeline => !timelineReading && !timelineClosing && !busy && !disposed && service.HistoryReady;
    public bool CanReadOlderIncidents => CanReadTimeline && timelineNext.HasValue;
    public Task LatestIncidentsAsync()
    {
        if (!CanReadTimeline) return Task.CompletedTask;
        timelineCursor = null; timelineUpper = null;
        return RequestTimelineRefresh(force: true);
    }
    public Task OlderIncidentsAsync()
    {
        if (!CanReadOlderIncidents) return Task.CompletedTask;
        timelineCursor = timelineNext; timelineUpper = pageUpper;
        return RequestTimelineRefresh(force: true);
    }
    private Task RequestTimelineRefresh(bool force = false)
    {
        if (timelineClosing || disposed || !service.HistoryReady) return Task.CompletedTask;
        if (!force && timelineRevision == service.HistoryRevision) return timelineRead;
        timelineDirty = true;
        if (timelineReading) return timelineRead;
        timelineReading = true; TimelineControlsChanged();
        timelineRead = ReadAsync(); return timelineRead;
        async Task ReadAsync()
        {
            try
            {
                do
                {
                    timelineDirty = false;
                    var revision = service.HistoryRevision;
                    var page = await service.ReadIncidentPageAsync(new(50, timelineCursor, UpperBound: timelineUpper));
                    timelineRevision = revision;
                    var ids = page.Items.Select(i => i.Incident.Id).ToHashSet();
                    foreach (var removed in Outages.Where(o => !ids.Contains(o.Id)).ToArray()) Outages.Remove(removed);
                    for (var index = 0; index < page.Items.Count; index++)
                    {
                        var item = page.Items[index]; var row = Outages.FirstOrDefault(o => o.Id == item.Incident.Id);
                        if (row is null) { row = new(item.Incident, item.SessionInterrupted); Outages.Insert(index, row); }
                        else { var oldIndex = Outages.IndexOf(row); if (oldIndex != index) Outages.Move(oldIndex, index); row.Apply(item.Incident, item.SessionInterrupted); }
                    }
                    if (SelectedOutage is null || !ids.Contains(SelectedOutage.Id)) SelectedOutage = Outages.FirstOrDefault();
                    timelineNext = page.NextCursor; pageUpper = page.UpperBound;
                    TimelineMessage = $"Showing {page.Items.Count} incidents, newest recorded first · " +
                        (page.NextCursor.HasValue ? "older incidents available." : "end of this history range.") +
                        " Local dates below; exact UTC is included in incident details and CSV. Retention can remove eligible records; Latest refreshes the range.";
                    Changed(nameof(HasNoOutages)); Changed(nameof(CanExport));
                } while (timelineDirty && !timelineClosing);
            }
            catch (Exception ex) { TimelineMessage = $"Timeline could not be read; existing evidence is preserved. {ex.Message}"; }
            finally { timelineReading = false; TimelineControlsChanged(); }
        }
    }
    private void TimelineControlsChanged()
    { Changed(nameof(CanReadTimeline)); Changed(nameof(CanReadOlderIncidents)); Changed(nameof(HasNoOutages)); }
}
