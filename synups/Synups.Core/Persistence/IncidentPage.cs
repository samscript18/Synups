using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public sealed record IncidentItem(OutageRecord Incident, bool SessionInterrupted);
/// <summary>Stable insertion-order cursor, independent of UTC clock jumps and updates to existing records.</summary>
public sealed record IncidentQuery(int Limit = 50, long? Cursor = null, bool OldestFirst = false, long? UpperBound = null);
public sealed record IncidentPage(IReadOnlyList<IncidentItem> Items, long? NextCursor, long UpperBound);
public interface IIncidentHistoryReader
{
    Task<IncidentPage> ReadIncidentPageAsync(IncidentQuery query);
    Task<OutageRecord?> FindIncidentAsync(Guid id);
}
