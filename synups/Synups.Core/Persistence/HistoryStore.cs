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
public sealed class SqliteHistoryStore(string fileName, long maxBytes = HistoryCapacity.DefaultBytes) : IHistoryStore, IHistoryStorageManagement, IIncidentHistoryReader
{
    private readonly SemaphoreSlim io = new(1, 1);
    private readonly string path = Path.GetFullPath(fileName);
    private SqliteConnection? connection;
    private FileStream? lease;
    private readonly long initialBytes = maxBytes;
    private long configuredBytes = maxBytes;
    private string BackupDirectory => path + ".backups";
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
            var hasPolicy = Number("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='storage_policy'") == 1;
            configuredBytes = hasPolicy ? Number("SELECT max_bytes FROM storage_policy WHERE id=1") : initialBytes;
            // The optional constructor override also supports bounded host/test stores. UI changes use the stricter public range.
            if (configuredBytes < 128 * 1024 || configuredBytes > HistoryCapacity.MaximumBytes)
                throw new InvalidDataException("Invalid stored history capacity. No reset performed.");
            // An already oversized legacy database may need a few pages for additive policy metadata.
            // Permit only this migration overhead, then cap growth at its actual allocation; never truncate old pages.
            var allocation = checked(Number("PRAGMA page_count") * Number("PRAGMA page_size"));
            var migrationLimit = !hasPolicy && allocation >= configuredBytes
                ? checked(allocation + 16 * Number("PRAGMA page_size")) : configuredBytes;
            ApplyPageLimit(migrationLimit, allowExistingLarger: true);
            using var migration = connection.BeginTransaction();
            using var schema = connection.CreateCommand(); schema.Transaction = migration;
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS storage_policy(id INTEGER PRIMARY KEY CHECK(id=1), max_bytes INTEGER NOT NULL);
                INSERT OR IGNORE INTO storage_policy VALUES(1,$bytes);
                CREATE TABLE IF NOT EXISTS outages(id TEXT PRIMARY KEY, session_id TEXT NOT NULL, recovered INTEGER NOT NULL, observed_end INTEGER NOT NULL, last_observed TEXT, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS events(id TEXT PRIMARY KEY, sequence INTEGER UNIQUE NOT NULL, session_id TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS metadata(id INTEGER PRIMARY KEY CHECK(id=1), sequence INTEGER NOT NULL);
                INSERT OR IGNORE INTO metadata VALUES(1,0);
                PRAGMA user_version=1;
                """;
            schema.Parameters.AddWithValue("$bytes", configuredBytes);
            schema.ExecuteNonQuery();
            migration.Commit();
            ApplyPageLimit(configuredBytes, allowExistingLarger: true);
        }
        catch { connection?.Dispose(); connection = null; lease?.Dispose(); lease = null; throw; }
    }
    private object? Scalar(string sql)
    { using var command = connection!.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    private void Execute(string sql)
    { using var command = connection!.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private long Number(string sql) => Convert.ToInt64(Scalar(sql), CultureInfo.InvariantCulture);
    private long ApplyPageLimit(long bytes, bool allowExistingLarger)
    {
        var pageSize = Number("PRAGMA page_size");
        var requested = bytes / pageSize;
        var existing = Number("PRAGMA page_count");
        if (!allowExistingLarger && requested < existing)
            throw new InvalidOperationException($"The database already allocates {checked(existing * pageSize)} bytes. Choose at least that capacity. No data was deleted and the previous setting is unchanged.");
        var expected = Math.Max(requested, existing);
        var effective = Number(FormattableString.Invariant($"PRAGMA max_page_count={expected}"));
        if (effective != expected)
            throw new IOException($"SQLite accepted {effective} pages instead of {expected}; capacity change refused.");
        return checked(effective * pageSize);
    }

    public Task SetCapacityAsync(long bytes) => WorkAsync(() =>
    {
        HistoryCapacity.Validate(bytes); Open();
        var previous = configuredBytes;
        try
        {
            ApplyPageLimit(bytes, allowExistingLarger: false);
            using var tx = connection!.BeginTransaction();
            using var command = connection.CreateCommand(); command.Transaction = tx;
            command.CommandText = "UPDATE storage_policy SET max_bytes=$bytes WHERE id=1";
            command.Parameters.AddWithValue("$bytes", bytes);
            if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("History capacity policy is missing.");
            tx.Commit();
            configuredBytes = bytes; // The durable transaction is authoritative, never the draft UI/preferences file.
        }
        catch
        {
            // PRAGMA is connection state, not a transactional setting. Reopen from the last committed policy on any failure.
            connection!.Dispose(); connection = null; lease!.Dispose(); lease = null; configuredBytes = previous;
            throw;
        }
        return true;
    });

    public Task<StorageUsage> GetStorageUsageAsync(string logDirectory) => WorkAsync(() =>
    {
        Open();
        static long Size(string name)
        {
            try { return new FileInfo(name).Length; }
            catch (FileNotFoundException) { return 0; }
            catch (DirectoryNotFoundException) { return 0; }
        }
        static long FolderSize(string directory, string pattern)
        {
            try { return new DirectoryInfo(directory).EnumerateFiles(pattern).Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0).Sum(f => f.Length); }
            catch (DirectoryNotFoundException) { return 0; }
        }
        long? available = null;
        // Use the containing mounted volume, including Unix mount points, rather than assuming the root volume.
        var volume = DriveInfo.GetDrives().Where(d => path.StartsWith(d.RootDirectory.FullName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            .OrderByDescending(d => d.RootDirectory.FullName.Length).FirstOrDefault();
        if (volume is { IsReady: true }) available = volume.AvailableFreeSpace;
        return new StorageUsage(configuredBytes, checked(Number("PRAGMA max_page_count") * Number("PRAGMA page_size")),
            Number("PRAGMA page_size"), Number("PRAGMA page_count"), Number("PRAGMA freelist_count"), Size(path), Size(path + "-wal"),
            Size(path + "-shm"), Size(path + "-journal"), FolderSize(BackupDirectory, "*"), FolderSize(logDirectory, "Synups-*"), available, BackupDirectory);
    });

    public Task<string> BackupAsync() => WorkAsync(() =>
    {
        Open();
        Directory.CreateDirectory(BackupDirectory);
        var destination = Path.Combine(BackupDirectory, $"history-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.db");
        var temporary = destination + ".tmp";
        try
        {
            using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ToString()))
            {
                backup.Open();
                connection!.BackupDatabase(backup); // SQLite online backup includes committed WAL/journal state.
                using var journal = backup.CreateCommand(); journal.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;"; journal.ExecuteNonQuery();
                using var check = backup.CreateCommand(); check.CommandText = "PRAGMA quick_check";
                if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal)) throw new IOException("Backup integrity verification failed.");
            }
            File.Move(temporary, destination); // Unique closed snapshot; never overwrite any live database.
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    });

    public Task<HistoryState> LoadAsync(bool includeAllEvents = false) => WorkAsync(() =>
    {
        Open();
        RecoverOpenSessions();
        // Normal startup/cache reads are bounded. Explicit full archive/logging remains a separate operation.
        var sessions = Read<SessionRecord>(includeAllEvents ? "SELECT payload FROM sessions ORDER BY rowid" :
            "SELECT payload FROM sessions WHERE rowid IN(SELECT rowid FROM sessions ORDER BY rowid DESC LIMIT 200) OR id IN(SELECT session_id FROM outages WHERE recovered=0 AND observed_end=0) ORDER BY rowid");
        var outages = Read<OutageRecord>(includeAllEvents ? "SELECT payload FROM outages ORDER BY rowid" :
            "SELECT payload FROM outages WHERE rowid IN(SELECT rowid FROM outages ORDER BY rowid DESC LIMIT 200) OR (recovered=0 AND observed_end=0) ORDER BY rowid");
        var events = Read<MonitorEvent>(includeAllEvents ? "SELECT payload FROM events ORDER BY sequence" : "SELECT payload FROM (SELECT payload,sequence FROM events ORDER BY sequence DESC LIMIT 500) ORDER BY sequence");
        var seq = Convert.ToInt64(Scalar("SELECT sequence FROM metadata WHERE id=1"), CultureInfo.InvariantCulture);
        return new HistoryState(outages, events, sessions, seq);
    });

    private void RecoverOpenSessions()
    {
        if (recoveredOpenSessions) return;
        while (true)
        {
            var open = Read<SessionRecord>("SELECT payload FROM sessions WHERE json_extract(payload,'$.EndedUtc') IS NULL AND json_extract(payload,'$.Interrupted')=0 LIMIT 100");
            if (open.Count == 0) break;
            foreach (var session in open)
            {
                var notice = new MonitorEvent(Number("SELECT sequence FROM metadata WHERE id=1") + 1, session.Id, null, DateTimeOffset.UtcNow,
                    MonitorEventKind.Interrupted, "Previous process ended without a clean session end. Last observations retained; no recovery inferred.");
                using var tx = connection!.BeginTransaction();
                using var command = connection.CreateCommand(); command.Transaction = tx;
                // SQL updates unresolved rows without materializing every historic outage. One session's recovery is atomic.
                command.CommandText = """
                    UPDATE outages SET observed_end=1,
                      payload=json_set(payload,'$.ObservationEndedUtc',COALESCE($last,json_extract(payload,'$.StartedUtc')),'$.ContainsObservationGap',json('true'))
                      WHERE session_id=$session AND recovered=0 AND observed_end=0;
                    UPDATE sessions SET payload=$payload WHERE id=$session;
                    INSERT INTO events(id,sequence,session_id,payload) VALUES($id,$seq,$session,$event);
                    UPDATE metadata SET sequence=$seq WHERE id=1;
                    """;
                command.Parameters.AddWithValue("$last", (object?)session.LastObservedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
                command.Parameters.AddWithValue("$session", session.Id.ToString());
                command.Parameters.AddWithValue("$payload", Json(session with { Interrupted = true, ContainsObservationGap = true }));
                command.Parameters.AddWithValue("$id", notice.Id.ToString()); command.Parameters.AddWithValue("$seq", notice.Sequence);
                command.Parameters.AddWithValue("$event", Json(notice)); command.ExecuteNonQuery(); tx.Commit();
            }
        }
        recoveredOpenSessions = true;
    }

    public Task<IncidentPage> ReadIncidentPageAsync(IncidentQuery query) => WorkAsync(() =>
    {
        if (query.Limit is < 1 or > 100 || query.Cursor is <= 0 || query.UpperBound is < 0) throw new ArgumentException("Incident page: 1–100 rows and valid positive cursor.");
        Open(); RecoverOpenSessions();
        var upper = query.UpperBound ?? Number("SELECT COALESCE(MAX(rowid),0) FROM outages");
        using var command = connection!.CreateCommand();
        var direction = query.OldestFirst ? "ASC" : "DESC"; var comparison = query.OldestFirst ? ">" : "<";
        var cursorClause = query.Cursor.HasValue ? $"AND o.rowid {comparison} $cursor" : "";
        command.CommandText = $"""
            SELECT o.rowid,o.payload,COALESCE(json_extract(s.payload,'$.Interrupted'),0)
            FROM outages o LEFT JOIN sessions s ON s.id=o.session_id
            WHERE o.rowid <= $upper {cursorClause}
            ORDER BY o.rowid {direction} LIMIT $limit
            """;
        command.Parameters.AddWithValue("$upper", upper); command.Parameters.AddWithValue("$cursor", (object?)query.Cursor ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", query.Limit + 1);
        using var reader = command.ExecuteReader(); var rows = new List<(long Id, IncidentItem Item)>();
        while (reader.Read()) rows.Add((reader.GetInt64(0), new(Parse<OutageRecord>(reader.GetString(1)), reader.GetInt64(2) != 0)));
        var more = rows.Count > query.Limit; if (more) rows.RemoveAt(rows.Count - 1);
        return new IncidentPage(rows.Select(r => r.Item).ToArray(), more ? rows[^1].Id : null, upper);
    });

    public Task<OutageRecord?> FindIncidentAsync(Guid id) => WorkAsync(() =>
    {
        Open(); RecoverOpenSessions(); using var command = connection!.CreateCommand();
        command.CommandText = "SELECT payload FROM outages WHERE id=$id"; command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteScalar() is string payload ? Parse<OutageRecord>(payload) : null;
    });
    private List<T> Read<T>(string sql)
    {
        using var command = connection!.CreateCommand(); command.CommandText = sql;
        using var reader = command.ExecuteReader(); var rows = new List<T>();
        while (reader.Read()) rows.Add(Parse<T>(reader.GetString(0)));
        return rows;
    }

    public Task CommitAsync(HistoryBatch batch) => WorkAsync(() => { Open(); RecoverOpenSessions(); Commit(batch); return true; });
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
        // Freed pages are reused within the limit. Avoid a second, potentially disk-full VACUUM after a successful deletion.
        return count;
    });
    public async ValueTask DisposeAsync()
    {
        await io.WaitAsync().ConfigureAwait(false);
        try { if (disposed) return; disposed = true; connection?.Dispose(); connection = null; lease?.Dispose(); lease = null; }
        finally { io.Release(); }
    }
}
