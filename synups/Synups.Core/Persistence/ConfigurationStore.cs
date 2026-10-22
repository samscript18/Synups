using System.Text.Json;
using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public sealed record ConfigurationLoad(MonitorConfiguration? Configuration, string? Error, bool NeedsRecovery);

public static class SynupsPaths
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Synups");
    public static string HistoryFile => Path.Combine(DataDirectory, "history.v1.db");
    public static string PreferencesFile => Path.Combine(DataDirectory, "preferences.v1.json");
    public static string ConfigurationFile => Path.Combine(DataDirectory, "configuration.v1.json");
}

public sealed class ConfigurationStore(string fileName)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim writes = new(1, 1);
    public string FileName { get; } = Path.GetFullPath(fileName);

    public async Task<ConfigurationLoad> LoadAsync()
    {
        try
        {
            if (!File.Exists(FileName)) return new(MonitorConfiguration.Empty, null, false);
            var info = new FileInfo(FileName);
            if (info.Length > 2_000_000) throw new IOException("Configuration exceeds 2MB.");
            await using var stream = File.OpenRead(FileName);
            var config = await JsonSerializer.DeserializeAsync<MonitorConfiguration>(stream, Options).ConfigureAwait(false)
                ?? throw new JsonException("Configuration is empty.");
            return new(config.ValidatedCopy(), null, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        { return new(null, $"Cannot load {FileName}: {ex.Message}. The original file was left untouched. Retry after fixing it, or explicitly recover with defaults.", true); }
    }

    public Task SaveAsync(MonitorConfiguration configuration, bool recoverInvalid = false) => SaveCoreAsync(configuration, recoverInvalid, false);
    public Task<string?> SaveWithBackupAsync(MonitorConfiguration configuration) => SaveCoreAsync(configuration, false, true);
    private async Task<string?> SaveCoreAsync(MonitorConfiguration configuration, bool recoverInvalid, bool backupExisting)
    {
        var valid = configuration.ValidatedCopy();
        await writes.WaitAsync().ConfigureAwait(false);
        try
        {
            var existing = await LoadAsync().ConfigureAwait(false);
            if (existing.NeedsRecovery && !recoverInvalid) throw new IOException(existing.Error);
            var directory = Path.GetDirectoryName(FileName)!;
            Directory.CreateDirectory(directory);
            string? backup = null;
            if (existing.NeedsRecovery || (backupExisting && File.Exists(FileName)))
            {
                backup = FileName + $".{(existing.NeedsRecovery ? "recovered" : "before-import")}-{Guid.NewGuid():N}.bak";
                await Task.Run(() => File.Copy(FileName, backup, overwrite: false)).ConfigureAwait(false);
            }
            var temporary = Path.Combine(directory, $".{Path.GetFileName(FileName)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, valid, Options).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
                // Same-directory rename: readers see either complete version; never truncate the live file.
                File.Move(temporary, FileName, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return backup;
        }
        finally { writes.Release(); }
    }
}
