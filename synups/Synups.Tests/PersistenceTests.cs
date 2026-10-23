using System.Text;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task AtomicConfigurationRoundtripPreservesIdsAndInvalidFileRequiresExplicitRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "configuration.v1.json"); var store = new ConfigurationStore(path);
            Assert.Empty((await store.LoadAsync()).Configuration!.Groups);
            var config = Config(Group(2));
            await store.SaveAsync(config);
            var loaded = (await new ConfigurationStore(path).LoadAsync()).Configuration!;
            Assert.Equal(config.Groups[0].Id, loaded.Groups[0].Id);
            Assert.Equal(config.Groups[0].Targets[1], loaded.Groups[0].Targets[1]);
            await File.WriteAllTextAsync(path, "{broken", TestContext.Current.CancellationToken);
            var failure = await store.LoadAsync();
            Assert.True(failure.NeedsRecovery);
            Assert.Null(failure.Configuration);
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(config));
            Assert.Equal("{broken", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            await store.SaveAsync(config, recoverInvalid: true);
            Assert.Equal("{broken", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(directory, "*.bak")), TestContext.Current.CancellationToken));
            Assert.False((await store.LoadAsync()).NeedsRecovery);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(0, 1000, 200)] [InlineData(249, 1000, 200)] [InlineData(1000, 0, 200)]
    [InlineData(1000, 1000, 0)] [InlineData(1000, 1000, 5001)]
    public void ValidatesTimingBounds(int poll, int fail, int timeout)
    {
        var group = Group() with { PollMilliseconds = poll, FailMilliseconds = fail, TimeoutMilliseconds = timeout };
        Assert.Throws<ArgumentException>(() => Config(group).ValidatedCopy());
    }

    [Theory]
    [InlineData("example.com")] [InlineData("0.0.0.0")] [InlineData("::")] [InlineData("224.0.0.1")] [InlineData("255.255.255.255")]
    public void RejectsHostnamesUnspecifiedAndMulticastAddresses(string address)
    {
        var group = Group();
        Assert.Throws<ArgumentException>(() => Config(group with { Targets = [group.Targets[0] with { Address = address }] }).ValidatedCopy());
    }

    [Fact]
    public void ValidationPreservesDisabledEmptyGroupsButRejectsEnabledEmptyOrDuplicateIds()
    {
        var group = Group();
        Assert.Throws<ArgumentException>(() => Config(group with { Targets = [] }).ValidatedCopy());
        Assert.Single(Config(group with { Enabled = false, Targets = [] }).ValidatedCopy().Groups);
        Assert.Throws<ArgumentException>(() => Config(group, group).ValidatedCopy());
        Assert.Throws<ArgumentException>(() => new MonitorConfiguration(99, [group]).ValidatedCopy());
        Assert.Throws<ArgumentException>(() => Config(group with { Name = "\n" }).ValidatedCopy());
        var copy = Config(group).ValidatedCopy();
        Assert.NotSame(group.Targets, copy.Groups[0].Targets);
    }

    [Fact]
    public void DefaultRestoreDoesNotInventDiscoveredDevices()
    {
        var config = MonitorConfiguration.OriginalDefaults();
        Assert.Equal(15, config.Groups.Sum(g => g.Targets.Count));
        Assert.Single(config.Groups, g => g.Enabled);
        Assert.All(config.Groups.Where(g => !g.Enabled), g => Assert.Empty(g.Targets));
    }

    [Fact]
    public async Task CsvEscapesCommasQuotesNewlinesAndPreservesUnknownRecovery()
    {
        var utc = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        var outage = new OutageRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A, \"B\"", utc, utc,
            null, utc.AddSeconds(5), TimeSpan.FromSeconds(5.25), true, null, "line1\nline2, \"quoted\"");
        await using var stream = new MemoryStream();
        await CsvExporter.WriteAsync(stream, [outage]);
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"A, \"\"B\"\"\"", csv);
        Assert.Contains("\"line1\nline2, \"\"quoted\"\"\"", csv);
        Assert.Contains("5.25,true,,", csv);
        Assert.Contains("2026-10-16T00:00:00.0000000+00:00", csv);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public async Task UnknownVersionDoesNotDestroyConfigurationAndFileErrorsSurface()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-tests-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "config.json");
            await File.WriteAllTextAsync(path, "{\"version\":42,\"groups\":[]}", TestContext.Current.CancellationToken);
            Assert.True((await new ConfigurationStore(path).LoadAsync()).NeedsRecovery);
            var impossible = new ConfigurationStore(Path.Combine(path, "child.json"));
            await Assert.ThrowsAnyAsync<IOException>(() => impossible.SaveAsync(Config(Group())));
        }
        finally { Directory.Delete(directory, true); }
    }
}
