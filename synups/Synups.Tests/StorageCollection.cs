using Xunit;

namespace Synups.Tests;

// Real fsync/backup/capacity fixtures must not compete with the suite's other disk writers.
// Production concurrency and bounded shutdown remain exercised explicitly in EngineTests and HistoryTests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StorageCollection
{
    public const string Name = "Durable storage";
}
