namespace Synups.Core.Persistence;

public static class HistoryCapacity
{
    public const long MiB = 1024L * 1024;
    public const long MinimumBytes = 256 * MiB;
    public const long DefaultBytes = 1024 * MiB;
    public const long MaximumBytes = 10 * 1024 * MiB;

    public static void Validate(long bytes)
    {
        if (bytes < MinimumBytes || bytes > MaximumBytes || bytes % MiB != 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "History capacity must be a whole number of MiB from 256 to 10240 (10 GiB).");
    }
}

public sealed record StorageUsage(long ConfiguredBytes, long EffectiveLimitBytes, long PageSize, long PageCount, long FreePages,
    long DatabaseBytes, long WalBytes, long ShmBytes, long JournalBytes, long BackupBytes, long DerivedLogBytes,
    long? VolumeAvailableBytes, string BackupDirectory)
{
    public long AllocatedBytes => checked(PageCount * PageSize);
    public long ReusableBytes => checked(FreePages * PageSize);
    public long UsedBytes => AllocatedBytes - ReusableBytes;
    public long AvailableDatabaseBytes => Math.Max(0, Math.Min(ConfiguredBytes, EffectiveLimitBytes) - UsedBytes);
    public long GrowthHeadroomBytes => Math.Max(0, Math.Min(ConfiguredBytes, EffectiveLimitBytes) - AllocatedBytes);
    public long TotalDiskBytes => checked(DatabaseBytes + WalBytes + ShmBytes + JournalBytes + BackupBytes + DerivedLogBytes);
    public long WarningBytes => ConfiguredBytes * 4 / 5;
    public long CriticalBytes => ConfiguredBytes * 19 / 20;
    public int WarningLevel => AllocatedBytes >= CriticalBytes ? 95 : AllocatedBytes >= WarningBytes ? 80 : 0;
}

/// <summary>One warning and one escalation per capacity per process; size oscillations cannot cause storms.</summary>
public sealed class StorageWarningPolicy
{
    private long capacity;
    private int highestLevel;
    public int Select(StorageUsage usage)
    {
        if (capacity != usage.ConfiguredBytes) { capacity = usage.ConfiguredBytes; highestLevel = 0; }
        var level = usage.WarningLevel;
        if (level <= highestLevel) return 0;
        highestLevel = level;
        return level;
    }
}

public interface IHistoryStorageManagement
{
    Task SetCapacityAsync(long bytes);
    Task<StorageUsage> GetStorageUsageAsync(string logDirectory);
    Task<string> BackupAsync();
}
