using Synups.Core.Persistence;
using Synups.Desktop;
using Synups.Desktop.Diagnostics;
using Synups.Desktop.Integrations;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

[Collection(StorageCollection.Name)]
public sealed class ReleaseHardeningTests
{
    [Fact]
    public void OrdinaryCommandLineRemainsPassiveAndUsesOriginalDataLocation()
    {
        var options = DesktopOptions.Parse([]);
        Assert.Equal(Path.GetFullPath(SynupsPaths.DataDirectory), options.DataDirectory);
        Assert.Null(options.VerificationDirectory);
    }
    [Theory]
    [InlineData("--unknown", "x")]
    [InlineData("--data-directory", "relative")]
    [InlineData("--verification-stage", "restart")]
    [InlineData("--observe-seconds", "0")]
    [InlineData("--verify-installation", "relative")]
    public void InvalidCommandLinesCannotSilentlyChangeStartup(string option, string value)
        => Assert.Throws<ArgumentException>(() => DesktopOptions.Parse([option, value]));
    [Fact]
    public async Task SingleInstanceLeaseForwardsActivationAndDrainsBeforeReopening()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-instance-" + Guid.NewGuid());
        var first = InstanceCoordinator.TryAcquire(directory)!;
        try
        {
            Assert.Null(InstanceCoordinator.TryAcquire(directory));
            var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.Listen(() => activated.TrySetResult());
            Assert.True(await InstanceCoordinator.ActivateExistingAsync(directory, TestContext.Current.CancellationToken));
            await activated.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, first.ActivationCount);
        }
        finally { await first.DisposeAsync(); }
        await using (var reopened = InstanceCoordinator.TryAcquire(directory)) Assert.NotNull(reopened);
        Directory.Delete(directory, true);
    }
    [Fact]
    public async Task InstallationCheckRefusesRealUserDataBeforeWriting()
    {
        var report = Path.Combine(Path.GetTempPath(), "Synups-refuse-" + Guid.NewGuid());
        var check = new InstallationCheck(new(SynupsPaths.DataDirectory, report));
        await Assert.ThrowsAsync<ArgumentException>(() => check.PrepareAsync());
        Assert.False(Directory.Exists(report));
    }
    [Fact]
    public async Task InstallationCheckNeverOverwritesPriorEvidenceOrExistingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-check-" + Guid.NewGuid());
        var data = Path.Combine(directory, "data"); var reports = Path.Combine(directory, "reports");
        Directory.CreateDirectory(reports);
        var evidence = Path.Combine(reports, "exercise.json");
        await File.WriteAllTextAsync(evidence, "preserve this evidence", TestContext.Current.CancellationToken);
        var check = new InstallationCheck(new(data, reports));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => check.PrepareAsync());
            await check.RecordStartupFailureAsync(new IOException("failure"));
            Assert.Equal("preserve this evidence", await File.ReadAllTextAsync(evidence, TestContext.Current.CancellationToken));
            var unrelated = Path.Combine(data, "existing.txt");
            await File.WriteAllTextAsync(unrelated, "preserve data", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<IOException>(() => new InstallationCheck(new(data, Path.Combine(directory, "other"))).PrepareAsync());
            Assert.Equal("preserve data", await File.ReadAllTextAsync(unrelated, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task InstallationRestartRefusesNonLoopbackProfileWithoutProbing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-guard-" + Guid.NewGuid());
        var data = Path.Combine(directory, "data"); var reports = Path.Combine(directory, "reports");
        try
        {
            await new InstallationCheck(new(data, reports)).PrepareAsync();
            var group = Group(); group = group with { Targets = [group.Targets[0] with { Address = "192.0.2.1" }] };
            await new ConfigurationStore(Path.Combine(data, "configuration.v1.json")).SaveAsync(Config(group));
            await Assert.ThrowsAsync<IOException>(() => new InstallationCheck(new(data, reports, "restart")).PrepareAsync());
        }
        finally { Directory.Delete(directory, true); }
    }
}
