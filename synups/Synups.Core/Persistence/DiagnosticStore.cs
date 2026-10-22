using System.Globalization;
using System.Text.Json;
using Synups.Core.Diagnostics;

namespace Synups.Core.Persistence;

public sealed partial class SqliteHistoryStore : IDiagnosticRepository
{
    private bool diagnosticsRecovered;
    private void BackupBeforeDiagnosticsMigration()
    {
        Directory.CreateDirectory(BackupDirectory);
        var final = Path.Combine(BackupDirectory, $"history-before-diagnostics-v2-{Guid.NewGuid():N}.db");
        var temporary = final + ".tmp";
        try
        {
            using (var backup = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ToString()))
            {
                backup.Open(); connection!.BackupDatabase(backup);
                using var check = backup.CreateCommand(); check.CommandText = "PRAGMA quick_check";
                if (check.ExecuteScalar() as string != "ok") throw new IOException("Pre-migration backup verification failed. Original schema was not changed.");
            }
            File.Move(temporary, final);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private const string DiagnosticSchema = """
        CREATE TABLE IF NOT EXISTS diagnostic_sessions(
          id TEXT PRIMARY KEY, incident_id TEXT REFERENCES outages(id) ON DELETE RESTRICT,
          target TEXT NOT NULL, started_utc TEXT NOT NULL, state INTEGER NOT NULL, profile TEXT NOT NULL, payload TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS diagnostic_incident ON diagnostic_sessions(incident_id,started_utc);
        CREATE INDEX IF NOT EXISTS diagnostic_started ON diagnostic_sessions(started_utc,id);
        CREATE INDEX IF NOT EXISTS diagnostic_target ON diagnostic_sessions(target,started_utc);
        CREATE TABLE IF NOT EXISTS diagnostic_results(
          id TEXT PRIMARY KEY, session_id TEXT NOT NULL REFERENCES diagnostic_sessions(id) ON DELETE CASCADE,
          kind INTEGER NOT NULL, outcome INTEGER NOT NULL, started_utc TEXT NOT NULL, payload TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS diagnostic_result_kind ON diagnostic_results(session_id,kind);
        CREATE TABLE IF NOT EXISTS diagnostic_observations(
          result_id TEXT NOT NULL REFERENCES diagnostic_results(id) ON DELETE CASCADE, ordinal INTEGER NOT NULL,
          utc TEXT NOT NULL, name TEXT NOT NULL, certainty INTEGER NOT NULL, value REAL, unit TEXT, text TEXT,
          PRIMARY KEY(result_id,ordinal));
        CREATE TABLE IF NOT EXISTS diagnostic_baselines(
          id TEXT PRIMARY KEY, session_id TEXT NOT NULL REFERENCES diagnostic_sessions(id) ON DELETE RESTRICT,
          target TEXT NOT NULL, saved_utc TEXT NOT NULL, payload TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS diagnostic_baseline_target ON diagnostic_baselines(target,saved_utc);
        CREATE TABLE IF NOT EXISTS diagnostic_comparisons(
          id TEXT PRIMARY KEY, baseline_id TEXT NOT NULL REFERENCES diagnostic_baselines(id) ON DELETE RESTRICT,
          session_id TEXT NOT NULL REFERENCES diagnostic_sessions(id) ON DELETE RESTRICT, utc TEXT NOT NULL, payload TEXT NOT NULL);
        """;
    public Task InitializeDiagnosticsAsync() => WorkAsync(() =>
    {
        Open();
        if (diagnosticsRecovered) return true;
        using var command = connection!.CreateCommand();
        // Each checkpoint already contains immutable partial results. Preserve the original times; do not invent completion.
        command.CommandText = """
            UPDATE diagnostic_sessions SET state=$interrupted,
              payload=json_set(payload,'$.State',$interrupted,'$.Error','Previous process ended before diagnostic completion. Retained checkpoint; no recovery inferred.')
            WHERE state IN ($pending,$running);
            """;
        command.Parameters.AddWithValue("$interrupted", (int)DiagnosticState.Interrupted);
        command.Parameters.AddWithValue("$pending", (int)DiagnosticState.Pending);
        command.Parameters.AddWithValue("$running", (int)DiagnosticState.Running);
        command.ExecuteNonQuery(); diagnosticsRecovered = true; return true;
    });
    private static void CheckSession(DiagnosticSession session)
    {
        if (session.Version != 1 || !Enum.IsDefined(session.State) || session.Results.Count > 7 || session.Results.Select(r => r.Id).Distinct().Count() != session.Results.Count || session.Results.Select(r => r.Kind).Distinct().Count() != session.Results.Count || session.Request.Id == Guid.Empty)
            throw new InvalidDataException("Unsupported or invalid diagnostic evidence.");
        session.Request.ValidatedCopy();
        if (session.State == DiagnosticState.Completed && (session.CompletedUtc is null || session.Results.Count != session.Request.Tests.Count || session.Results.Any(r => r.Outcome != DiagnosticOutcome.Success)))
            throw new InvalidDataException("Completed sessions require every requested test to have successful retained evidence.");
        if (session.StartedUtc.Offset != TimeSpan.Zero || (session.Analysis?.Count ?? 0) > 100)
            throw new InvalidDataException("Invalid diagnostic times or analysis bounds.");
        if (session.Results.Any(r => r.Target != session.Request.Target || !Enum.IsDefined(r.Outcome) || r.Observations.Any(o => !Enum.IsDefined(o.Certainty) || o.Value is { } v && !double.IsFinite(v)) || !session.Request.Tests.Contains(r.Kind) || r.Id == Guid.Empty || r.Observations.Count > 2048 ||
            r.ElapsedMilliseconds < 0 || !double.IsFinite(r.ElapsedMilliseconds))) throw new InvalidDataException("Invalid diagnostic result bounds.");
    }
    public Task SaveDiagnosticSessionAsync(DiagnosticSession session) => WorkAsync(() =>
    {
        CheckSession(session); Open();
        var payload = Json(session);
        if (payload.Length > 2_000_000) throw new InvalidDataException("Diagnostic session exceeds 2 MB.");
        using var tx = connection!.BeginTransaction();
        void Write(string sql, params (string, object?)[] values)
        {
            using var command = connection.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
            foreach (var (key, value) in values) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
        // Session identity/link/target are immutable. A terminal record cannot be rewritten as a fresh running session.
        using (var existing = connection.CreateCommand())
        {
            existing.Transaction = tx; existing.CommandText = "SELECT payload FROM diagnostic_sessions WHERE id=$id";
            existing.Parameters.AddWithValue("$id", session.Request.Id.ToString());
            if (existing.ExecuteScalar() is string prior)
            {
                var old = Parse<DiagnosticSession>(prior);
                if (Json(old.Request) != Json(session.Request) || old.StartedUtc != session.StartedUtc ||
                    (old.State is not (DiagnosticState.Pending or DiagnosticState.Running) && prior != payload))
                    throw new InvalidOperationException("Saved diagnostic identity and completed evidence are immutable.");
                if (session.Results.Count < old.Results.Count || old.Results.Where((r, i) => Json(r) != Json(session.Results[i])).Any())
                    throw new InvalidOperationException("Previously checkpointed diagnostic results are append-only and immutable.");
            }
        }
        Write("""
            INSERT INTO diagnostic_sessions VALUES($id,$incident,$target,$utc,$state,$profile,$p)
            ON CONFLICT(id) DO UPDATE SET state=$state,payload=$p;
            """, ("$id", session.Request.Id.ToString()), ("$incident", session.Request.IncidentId?.ToString()), ("$target", session.Request.Target),
            ("$utc", session.StartedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)), ("$state", (int)session.State), ("$profile", session.Request.ProfileId), ("$p", payload));
        foreach (var result in session.Results)
        {
            using (var owner = connection.CreateCommand())
            {
                owner.Transaction = tx; owner.CommandText = "SELECT session_id,payload FROM diagnostic_results WHERE id=$id";
                owner.Parameters.AddWithValue("$id", result.Id.ToString());
                using var reader = owner.ExecuteReader();
                if (reader.Read() && (reader.GetString(0) != session.Request.Id.ToString() || reader.GetString(1) != Json(result)))
                    throw new InvalidOperationException("A diagnostic result cannot be reassigned or rewritten.");
            }
            Write("""
                INSERT INTO diagnostic_results VALUES($id,$session,$kind,$outcome,$utc,$p)
                ON CONFLICT(id) DO NOTHING;
                """, ("$id", result.Id.ToString()), ("$session", session.Request.Id.ToString()), ("$kind", (int)result.Kind),
                ("$outcome", (int)result.Outcome), ("$utc", result.StartedUtc.ToString("O")), ("$p", Json(result)));
            Write("DELETE FROM diagnostic_observations WHERE result_id=$id", ("$id", result.Id.ToString()));
            for (var i = 0; i < result.Observations.Count; i++)
            {
                var o = result.Observations[i];
                Write("INSERT INTO diagnostic_observations VALUES($id,$i,$utc,$name,$certainty,$value,$unit,$text)",
                    ("$id", result.Id.ToString()), ("$i", i), ("$utc", o.Utc.ToString("O")), ("$name", o.Name), ("$certainty", (int)o.Certainty),
                    ("$value", o.Value), ("$unit", o.Unit), ("$text", o.Text));
            }
        }
        tx.Commit(); return true;
    });
    public Task<DiagnosticSession?> FindDiagnosticSessionAsync(Guid id) => WorkAsync(() =>
    {
        Open(); using var command = connection!.CreateCommand(); command.CommandText = "SELECT payload FROM diagnostic_sessions WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        if (command.ExecuteScalar() is not string payload) return null;
        if (payload.Length > 2_000_000) throw new InvalidDataException("Diagnostic row exceeds 2 MB.");
        try { var result = Parse<DiagnosticSession>(payload); CheckSession(result); return result; }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException) { throw new InvalidDataException("Corrupt diagnostic row; original evidence preserved.", ex); }
    });
    public Task<IReadOnlyList<DiagnosticSessionSummary>> ReadDiagnosticSessionsAsync(int limit = 50, DateTimeOffset? before = null, Guid? incidentId = null) => WorkAsync<IReadOnlyList<DiagnosticSessionSummary>>(() =>
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        Open(); using var command = connection!.CreateCommand();
        command.CommandText = "SELECT id,target,incident_id,started_utc,state,profile FROM diagnostic_sessions WHERE ($before IS NULL OR started_utc<$before) AND ($incident IS NULL OR incident_id=$incident) ORDER BY started_utc DESC,id DESC LIMIT $limit";
        command.Parameters.AddWithValue("$before", (object?)before?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$incident", (object?)incidentId?.ToString() ?? DBNull.Value); command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader(); var rows = new List<DiagnosticSessionSummary>();
        while (reader.Read()) rows.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture), (DiagnosticState)reader.GetInt32(4), reader.GetString(5)));
        return rows;
    });
    public Task SaveDiagnosticBaselineAsync(DiagnosticBaseline baseline) => WorkAsync(() =>
    {
        if (baseline.Id == Guid.Empty || string.IsNullOrWhiteSpace(baseline.Label) || baseline.Label.Length > 120) throw new ArgumentException("Baseline requires a label of 1–120 characters.");
        Open(); using var get = connection!.CreateCommand(); get.CommandText = "SELECT payload FROM diagnostic_sessions WHERE id=$id";
        get.Parameters.AddWithValue("$id", baseline.SessionId.ToString());
        var session = get.ExecuteScalar() is string p ? Parse<DiagnosticSession>(p) : throw new InvalidOperationException("Baseline session was not saved.");
        if (session.Request.Target != baseline.Target || session.State != DiagnosticState.Completed || session.Results.Count == 0 || session.Results.Any(r => r.Outcome != DiagnosticOutcome.Success))
            throw new InvalidOperationException("Only a fully successful saved session can be a baseline.");
        using var insert = connection.CreateCommand(); insert.CommandText = "INSERT INTO diagnostic_baselines VALUES($id,$session,$target,$utc,$p)";
        insert.Parameters.AddWithValue("$id", baseline.Id.ToString()); insert.Parameters.AddWithValue("$session", baseline.SessionId.ToString());
        insert.Parameters.AddWithValue("$target", baseline.Target); insert.Parameters.AddWithValue("$utc", baseline.SavedUtc.ToString("O")); insert.Parameters.AddWithValue("$p", Json(baseline));
        insert.ExecuteNonQuery(); return true;
    });
    public Task<IReadOnlyList<DiagnosticBaseline>> ReadDiagnosticBaselinesAsync(int limit = 50) => WorkAsync<IReadOnlyList<DiagnosticBaseline>>(() =>
    { if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit)); Open(); return Read<DiagnosticBaseline>($"SELECT payload FROM diagnostic_baselines ORDER BY saved_utc DESC LIMIT {limit}"); });
    public Task SaveDiagnosticComparisonAsync(DiagnosticComparison comparison) => WorkAsync(() =>
    {
        if (comparison.Differences.Count > 100) throw new ArgumentException("Comparison exceeds bounds.");
        Open(); using var command = connection!.CreateCommand(); command.CommandText = "INSERT INTO diagnostic_comparisons VALUES($id,$baseline,$session,$utc,$p)";
        command.Parameters.AddWithValue("$id", comparison.Id.ToString()); command.Parameters.AddWithValue("$baseline", comparison.BaselineId.ToString());
        command.Parameters.AddWithValue("$session", comparison.SessionId.ToString()); command.Parameters.AddWithValue("$utc", comparison.Utc.ToString("O"));
        command.Parameters.AddWithValue("$p", Json(comparison)); command.ExecuteNonQuery(); return true;
    });
}
