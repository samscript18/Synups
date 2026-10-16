using System.Text.Json;

namespace Synups.Core.Persistence;

public sealed record WindowPlacement(double Width, double Height, int X, int Y, bool Maximized);
public sealed record AppPreferences
{
    public int Version { get; init; } = 1;
    public bool ConfirmExit { get; init; }
    public bool AlwaysOnTop { get; init; }
    public bool MinimizeToTray { get; init; }
    public bool LaunchMinimized { get; init; }
    public bool MonitorOnLaunch { get; init; }
    public bool NotifyOutages { get; init; } = true;
    public bool NotifyRecovery { get; init; } = true;
    public bool NotifyErrors { get; init; } = true;
    public bool NativeNotifications { get; init; }
    public bool AutomaticLogs { get; init; } = true;
    public string LogDirectory { get; init; } = Path.Combine(SynupsPaths.DataDirectory, "logs");
    public int RetentionDays { get; init; } // 0 keeps all evidence, bounded by the store's capacity.
    public WindowPlacement? Placement { get; init; }
    public AppPreferences Validate()
    {
        if (Version != 1) throw new ArgumentException("Unsupported preferences version.");
        if (RetentionDays is < 0 or > 3650) throw new ArgumentException("Retention: 0 (keep all) or 1–3650 days.");
        if (string.IsNullOrWhiteSpace(LogDirectory) || !Path.IsPathFullyQualified(LogDirectory)) throw new ArgumentException("Choose an absolute log directory.");
        if (Placement is { } p && (!double.IsFinite(p.Width) || !double.IsFinite(p.Height) || p.Width < 300 || p.Height < 200 || p.Width > 20000 || p.Height > 20000))
            throw new ArgumentException("Invalid saved window dimensions.");
        return this;
    }
}
public sealed record PreferencesLoad(AppPreferences Preferences, string? Error);
public sealed class PreferencesStore(string path, string? defaultLogDirectory = null)
{
    private readonly SemaphoreSlim writes = new(1, 1);
    public Task<PreferencesLoad> LoadAsync() => Task.Run<PreferencesLoad>(async () =>
    {
        try
        {
            if (!File.Exists(path)) return new(new() { LogDirectory = defaultLogDirectory ?? Path.Combine(SynupsPaths.DataDirectory, "logs") }, null);
            if (new FileInfo(path).Length > 64 * 1024) throw new IOException("Preferences exceed 64KB.");
            await using var file = File.OpenRead(path);
            var prefs = await JsonSerializer.DeserializeAsync<AppPreferences>(file).ConfigureAwait(false) ?? throw new JsonException("Empty preferences.");
            return new(prefs.Validate(), null);
        }
        catch (Exception ex) { return new(new() { LogDirectory = defaultLogDirectory ?? Path.Combine(SynupsPaths.DataDirectory, "logs") }, $"Preferences unreadable and preserved; safe defaults used: {ex.Message}"); }
    });
    public Task SaveAsync(AppPreferences preferences, bool recover = false) => Task.Run(async () =>
    {
        preferences.Validate();
        await writes.WaitAsync().ConfigureAwait(false);
        try
        {
            var prior = await LoadAsync().ConfigureAwait(false);
            if (prior.Error is not null && !recover) throw new IOException(prior.Error + " Explicitly recover preferences before overwriting.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (prior.Error is not null) File.Copy(path, path + $".{Guid.NewGuid():N}.bak");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                { await JsonSerializer.SerializeAsync(file, preferences).ConfigureAwait(false); await file.FlushAsync().ConfigureAwait(false); }
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { writes.Release(); }
    });
}
