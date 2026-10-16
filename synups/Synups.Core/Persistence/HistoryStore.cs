using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public sealed record HistoryState(IReadOnlyList<OutageRecord> Outages, IReadOnlyList<MonitorEvent> Events,
    IReadOnlyList<SessionRecord> Sessions, long LastSequence);
public sealed record HistoryBatch(IReadOnlyList<OutageRecord> Outages, IReadOnlyList<MonitorEvent> Events,
    SessionRecord? Session);
public interface IHistoryStore : IAsyncDisposable
{
    Task<HistoryState> LoadAsync(bool includeAllEvents = false);
    Task CommitAsync(HistoryBatch batch);
    Task<int> RetainAsync(int days, DateTimeOffset utcNow);
}

/// <summary>Single owned connection, serialized off-thread I/O. SQLite async APIs are synchronous.</summary>
public sealed class SqliteHistoryStore(string fileName, long maxBytes = 64 * 1024 * 1024) : IHistoryStore
{
    private readonly SemaphoreSlim io = new(1, 1);
    private readonly string path = Path.GetFullPath(fileName);
    private SqliteConnection? connection;
    private FileStream? lease;
    private bool disposed, recoveredOpenSessions;
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Parse<T>(string value) => JsonSerializer.Deserialize<T>(value) ?? throw new InvalidDataException("Empty history record.");

    private Task<T> WorkAsync<T>(Func<T> work) => Task.Run(async () =>
    {
        await io.WaitAsync().ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(disposed, this); return work(); }
        finally { io.Release(); }
    });
    private void Open()
    {
        if (connection is not null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 2 }.ToString());
            connection.Open();
            var version = Convert.ToInt32(Scalar("PRAGMA user_version"), CultureInfo.InvariantCulture);
            if (version is not (0 or 1)) throw new InvalidDataException($"Unsupported history version {version}. No reset performed.");
            var integrity = Convert.ToString(Scalar("PRAGMA quick_check"), CultureInfo.InvariantCulture);
            if (integrity != "ok") throw new InvalidDataException($"History integrity check failed: {integrity}");
            Execute("PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA journal_size_limit=0;");
            Execute($"PRAGMA max_page_count={Math.Max(32, maxBytes / Convert.ToInt64(Scalar("PRAGMA page_size"), CultureInfo.InvariantCulture))};");
            Execute("""
                CREATE TABLE IF NOT EXISTS outages(id TEXT PRIMARY KEY, session_id TEXT NOT NULL, recovered INTEGER NOT NULL, observed_end INTEGER NOT NULL, last_observed TEXT, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS events(id TEXT PRIMARY KEY, sequence INTEGER UNIQUE NOT NULL, session_id TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS metadata(id INTEGER PRIMARY KEY CHECK(id=1), sequence INTEGER NOT NULL);
                INSERT OR IGNORE INTO metadata VALUES(1,0);
                PRAGMA user_version=1;
                """);
        }
        catch { connection?.Dispose(); connection = null; lease?.Dispose(); lease = null; throw; }
    }
    private object? Scalar(string sql)
    { using var command = connection!.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    private void Execute(string sql)
    { using var command = connection!.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }

    public Task<HistoryState> LoadAsync(bool includeAllEvents = false) => WorkAsync(() =>
    {
        Open();
        var sessions = Read<SessionRecord>("SELECT payload FROM sessions ORDER BY rowid");
        var outages = Read<OutageRecord>("SELECT payload FROM outages ORDER BY rowid");
        var events = Read<MonitorEvent>(includeAllEvents ? "SELECT payload FROM events ORDER BY sequence" : "SELECT payload FROM (SELECT payload,sequence FROM events ORDER BY sequence DESC LIMIT 500) ORDER BY sequence");
        var seq = Convert.ToInt64(Scalar("SELECT sequence FROM metadata WHERE id=1"), CultureInfo.InvariantCulture);
        var interruptedIds = sessions.Where(s => s.EndedUtc is null && !s.Interrupted).Select(s => s.Id).ToHashSet();
        if (!recoveredOpenSessions && interruptedIds.Count > 0)
        {
            var recoveredSessions = sessions.Select(s => interruptedIds.Contains(s.Id) ? s with { Interrupted = true, ContainsObservationGap = true } : s).ToArray();
            var recoveredOutages = outages.Select(o => interruptedIds.Contains(o.SessionId) && o.RecoveredUtc is null && o.ObservationEndedUtc is null
                ? o with { ObservationEndedUtc = sessions.Single(s => s.Id == o.SessionId).LastObservedUtc ?? o.StartedUtc, ContainsObservationGap = true } : o).ToArray();
            var notices = interruptedIds.Select(id => new MonitorEvent(++seq, id, null, DateTimeOffset.UtcNow, MonitorEventKind.Interrupted,
                "Previous process ended without a clean session end. Last observations retained; no recovery inferred.")).ToArray();
            Commit(new(recoveredOutages, notices, null), recoveredSessions);
            outages = recoveredOutages.ToList(); sessions = recoveredSessions.ToList(); events.AddRange(notices);
        }
        recoveredOpenSessions = true;
        return new HistoryState(outages, includeAllEvents ? events : events.TakeLast(500).ToArray(), sessions, seq);
    });
    private List<T> Read<T>(string sql)
    {
        using var command = connection!.CreateCommand(); command.CommandText = sql;
        using var reader = command.ExecuteReader(); var rows = new List<T>();
        while (reader.Read()) rows.Add(Parse<T>(reader.GetString(0)));
        return rows;
    }

    public Task CommitAsync(HistoryBatch batch) => WorkAsync(() => { Open(); Commit(batch); return true; });
    private void Commit(HistoryBatch batch, IEnumerable<SessionRecord>? extraSessions = null)
    {
        using var transaction = connection!.BeginTransaction();
        void Write(string sql, params (string Key, object? Value)[] parameters)
        {
            using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
            foreach (var p in parameters) command.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
        foreach (var session in (extraSessions ?? Array.Empty<SessionRecord>()).Concat(batch.Session is { } s ? [s] : []))
            Write("INSERT INTO sessions(id,payload) VALUES($id,$p) ON CONFLICT(id) DO UPDATE SET payload=$p", ("$id", session.Id.ToString()), ("$p", Json(session)));
        foreach (var outage in batch.Outages)
            Write("""
                INSERT INTO outages VALUES($id,$session,$recovered,$ended,$observed,$p)
                ON CONFLICT(id) DO UPDATE SET recovered=$recovered,observed_end=$ended,last_observed=$observed,payload=$p
                """, ("$id", outage.Id.ToString()), ("$session", outage.SessionId.ToString()), ("$recovered", outage.RecoveredUtc.HasValue ? 1 : 0),
                ("$ended", outage.ObservationEndedUtc.HasValue ? 1 : 0), ("$observed", batch.Session?.LastObservedUtc?.ToString("O")), ("$p", Json(outage)));
        foreach (var e in batch.Events)
        {
            Write("INSERT INTO events VALUES($id,$seq,$session,$p) ON CONFLICT(id) DO NOTHING", ("$id", e.Id.ToString()), ("$seq", e.Sequence), ("$session", e.SessionId.ToString()), ("$p", Json(e)));
            Write("UPDATE metadata SET sequence=MAX(sequence,$seq) WHERE id=1", ("$seq", e.Sequence));
        }
        transaction.Commit();

    }

    // Retention is opt-in. Only recovered incidents and fully ended sessions older than the cutoff are eligible.
    public Task<int> RetainAsync(int days, DateTimeOffset utcNow) => WorkAsync(() =>
    {
        Open();
        if (days is < 1 or > 3650) throw new ArgumentException("Retention must be 1–3650 days.");
        var cutoff = utcNow.AddDays(-days);
        var candidates = Read<SessionRecord>("SELECT payload FROM sessions").Where(s => s.EndedUtc < cutoff && !s.Interrupted).ToArray();
        var count = 0;
        using (var tx = connection!.BeginTransaction())
        {
            foreach (var s in candidates)
            {
                using var command = connection.CreateCommand(); command.Transaction = tx;
                command.CommandText = """
                    DELETE FROM events WHERE session_id=$id AND NOT EXISTS(SELECT 1 FROM outages WHERE session_id=$id AND recovered=0);
                    DELETE FROM outages WHERE session_id=$id AND recovered=1 AND NOT EXISTS(SELECT 1 FROM outages WHERE session_id=$id AND recovered=0);
                    DELETE FROM sessions WHERE id=$id AND NOT EXISTS(SELECT 1 FROM outages WHERE session_id=$id);
                    """;
                command.Parameters.AddWithValue("$id", s.Id.ToString()); count += command.ExecuteNonQuery();
            }
            tx.Commit();
        }
        Execute("VACUUM;");
        return count;
    });
    public async ValueTask DisposeAsync()
    {
        await io.WaitAsync().ConfigureAwait(false);
        try { if (disposed) return; disposed = true; connection?.Dispose(); connection = null; lease?.Dispose(); lease = null; }
        finally { io.Release(); }
    }
}
