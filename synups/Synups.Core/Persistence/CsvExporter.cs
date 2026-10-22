using System.Globalization;
using System.Text;
using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public static class CsvExporter
{
    private const string Header = "SessionId,OutageId,GroupId,PeerGroup,StartedUtc,FirstFailureUtc,RecoveredUtc,ObservationEndedUtc,ElapsedSeconds,ContainsObservationGap,SincePreviousSeconds,Comment";
    public static async Task WriteAsync(Stream destination, IReadOnlyList<OutageRecord> outages)
    {
        using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
        await writer.WriteLineAsync(Header).ConfigureAwait(false);
        // Service records are in occurrence order, even if the user's UTC clock moves backwards.
        foreach (var o in outages)
            await WriteRowAsync(writer, o).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    public static async Task WriteAsync(Stream destination, IAsyncEnumerable<OutageRecord> outages)
    {
        using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
        await writer.WriteLineAsync(Header).ConfigureAwait(false);
        await foreach (var row in outages.ConfigureAwait(false)) await WriteRowAsync(writer, row).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static Task WriteRowAsync(StreamWriter writer, OutageRecord o)
    {
            var values = new[] { o.SessionId.ToString(), o.Id.ToString(), o.GroupId.ToString(), o.GroupName,
                o.StartedUtc.ToString("O", CultureInfo.InvariantCulture), o.FirstFailureUtc.ToString("O", CultureInfo.InvariantCulture),
                o.RecoveredUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "", o.ObservationEndedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "",
                o.Elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), o.ContainsObservationGap ? "true" : "false",
                o.SincePrevious?.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) ?? "", o.Comment };
        return writer.WriteLineAsync(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
