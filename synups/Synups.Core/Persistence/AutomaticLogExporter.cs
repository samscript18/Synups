using System.Globalization;
using System.Text;
using System.Text.Json;
using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public sealed record GroupStatistics(Guid GroupId, string GroupName, int Confirmed, int Recovered, int Unresolved,
    double SumElapsedSeconds, double? MinSeconds, double? MaxSeconds, double? MeanSeconds, bool ContainsGaps);
public static class AutomaticLogExporter
{
    public static IReadOnlyList<GroupStatistics> Statistics(IEnumerable<OutageRecord> outages) => outages.GroupBy(o => o.GroupId).Select(g =>
        new GroupStatistics(g.Key, g.First().GroupName, g.Count(), g.Count(o => o.RecoveredUtc.HasValue), g.Count(o => !o.RecoveredUtc.HasValue),
            g.Sum(o => o.Elapsed.TotalSeconds), g.Min(o => o.Elapsed.TotalSeconds), g.Max(o => o.Elapsed.TotalSeconds),
            g.Average(o => o.Elapsed.TotalSeconds), g.Any(o => o.ContainsObservationGap))).ToArray();

    public static Task WriteAsync(string directory, HistoryState archive, bool dateFilenames = false) => Task.Run(async () =>
    {
        Directory.CreateDirectory(directory);
        const long limit = 256 * 1024 * 1024;
        var total = new DirectoryInfo(directory).EnumerateFiles("Synups-*").Sum(f => f.Length);
        foreach (var session in archive.Sessions.Where(s => s.EndedUtc.HasValue || s.Interrupted))
        {
            var rows = archive.Outages.Where(o => o.SessionId == session.Id).ToArray();
            var records = archive.Events.Where(e => e.SessionId == session.Id).OrderBy(e => e.Sequence).ToArray();
            var json = JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, Session = session, Outages = rows, Events = records, Statistics = Statistics(rows),
                DurationMeaning = "Elapsed since confirmation through last observation; marked gaps are uncertain. No Internet uptime percentage is inferred." });
            var name = Path.Combine(directory, $"Synups-{(dateFilenames ? session.StartedUtc.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture) + "-" : "")}{session.Id}.json");
            var csvName = Path.ChangeExtension(name, ".csv");
            if (File.Exists(name) && File.Exists(csvName) && (await File.ReadAllBytesAsync(name).ConfigureAwait(false)).AsSpan().SequenceEqual(json)) continue;
            var oldLength = File.Exists(name) ? new FileInfo(name).Length : 0;
            if (total - oldLength + json.Length > limit) throw new IOException("Automatic logs reached 256MB. Archive logs or select another directory; history remains in SQLite.");

            await using var csv = new MemoryStream(); await CsvExporter.WriteAsync(csv, rows).ConfigureAwait(false);
            var bytes = csv.ToArray();
            var oldCsv = File.Exists(csvName) ? new FileInfo(csvName).Length : 0;
            if (total - oldCsv + bytes.Length - oldLength + json.Length > limit) throw new IOException("Automatic log quota reached; durable SQLite history remains intact.");
            await AtomicAsync(csvName, bytes).ConfigureAwait(false);
            await AtomicAsync(name, json).ConfigureAwait(false); total += bytes.Length - oldCsv + json.Length - oldLength;
        }
    });
    private static async Task AtomicAsync(string path, byte[] bytes)
    {
        var temporary = path + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
