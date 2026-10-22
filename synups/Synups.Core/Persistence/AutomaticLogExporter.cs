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

    public static Task WriteAsync(string directory, HistoryState archive, bool dateFilenames = false) =>
        WriteAsync(directory, archive, dateFilenames ? LogFilenameStyle.UtcSessionStart : LogFilenameStyle.StableSessionId);
    public static Task WriteAsync(string directory, HistoryState archive, LogFilenameStyle naming) => Task.Run(async () =>
    {
        if (!Enum.IsDefined(naming)) throw new ArgumentException("Unsupported log filename style.");
        Directory.CreateDirectory(directory);
        const long limit = 256 * 1024 * 1024;
        var total = new DirectoryInfo(directory).EnumerateFiles("Synups-*").Sum(f => f.Length);
        foreach (var session in archive.Sessions.Where(s => s.EndedUtc.HasValue || s.Interrupted))
        {
            var rows = archive.Outages.Where(o => o.SessionId == session.Id).ToArray();
            var records = archive.Events.Where(e => e.SessionId == session.Id).OrderBy(e => e.Sequence).ToArray();
            var json = JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, Session = session, Outages = rows, Events = records, Statistics = Statistics(rows),
                DurationMeaning = "Elapsed since confirmation through last observation; marked gaps are uncertain. No Internet uptime percentage is inferred." });
            var prefix = naming switch
            {
                LogFilenameStyle.UtcSessionStart => session.StartedUtc.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture) + "-",
                LogFilenameStyle.LocalSessionStart => session.StartedUtc.ToLocalTime().ToString("yyyyMMddTHHmmsszzz", CultureInfo.InvariantCulture).Replace(":", "") + "-",
                _ => "",
            };
            var name = Path.Combine(directory, $"Synups-{prefix}{session.Id}.json");
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
        if (naming == LogFilenameStyle.FixedArchiveSnapshot) await FixedSnapshotAsync(directory, archive, total, limit).ConfigureAwait(false);
    });
    private static async Task FixedSnapshotAsync(string directory, HistoryState archive, long total, long quota)
    {
        var json = Path.Combine(directory, "Synups-fixed.json"); var csv = Path.Combine(directory, "Synups-fixed.csv");
        var temporaryJson = Path.Combine(directory, ".Synups-fixed-" + Guid.NewGuid().ToString("N") + ".json.tmp");
        var temporaryCsv = temporaryJson + ".csv";
        var oldBytes = (File.Exists(json) ? new FileInfo(json).Length : 0) + (File.Exists(csv) ? new FileInfo(csv).Length : 0);
        var remaining = Math.Min(16L * 1024 * 1024, quota - total + oldBytes);
        if (remaining < 1) throw new IOException("Fixed log snapshot has no quota headroom. Per-session copies and SQLite history are preserved.");
        try
        {
            await using (var file = new FileStream(temporaryJson, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using var bounded = new LimitedOutput(file, remaining);
                await JsonSerializer.SerializeAsync(bounded, new { Version = 1, Scope = "Current archive snapshot; canonical per-session copies are retained separately.", archive.Sessions, archive.Events, archive.Outages, Statistics = Statistics(archive.Outages) }).ConfigureAwait(false);
                await file.FlushAsync().ConfigureAwait(false);
            }
            var used = new FileInfo(temporaryJson).Length;
            await using (var file = new FileStream(temporaryCsv, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using var bounded = new LimitedOutput(file, remaining - used);
                await CsvExporter.WriteAsync(bounded, archive.Outages).ConfigureAwait(false); await file.FlushAsync().ConfigureAwait(false);
            }
            // Both complete bounded files exist before replacement. SQLite and per-session copies remain the evidence source.
            File.Move(temporaryCsv, csv, true); File.Move(temporaryJson, json, true);
        }
        finally { if (File.Exists(temporaryJson)) File.Delete(temporaryJson); if (File.Exists(temporaryCsv)) File.Delete(temporaryCsv); }
    }
    private sealed class LimitedOutput(Stream inner, long maximum) : Stream
    {
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => inner.Position;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        private void Check(int count) { if (count > maximum - inner.Position) throw new IOException("Fixed combined snapshot exceeds its 16MiB/quota bound; choose per-session naming. Existing fixed files, per-session copies and SQLite are preserved."); }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); inner.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); inner.Write(buffer); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) { Check(buffer.Length); await inner.WriteAsync(buffer, token).ConfigureAwait(false); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) => WriteAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override void Flush() => inner.Flush(); public override Task FlushAsync(CancellationToken token) => inner.FlushAsync(token);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        // The outer await-using owns the file. Disposing this wrapper must not close it before FlushAsync.
    }
    private static async Task AtomicAsync(string path, byte[] bytes)
    {
        var temporary = path + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
