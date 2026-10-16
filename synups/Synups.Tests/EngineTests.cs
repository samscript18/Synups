using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Synups.Core.Monitoring;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class EngineTests
{
    [Theory]
    [InlineData(IPStatus.Success, ProbeOutcome.Success)]
    [InlineData(IPStatus.TimedOut, ProbeOutcome.Failure)]
    [InlineData(IPStatus.DestinationHostUnreachable, ProbeOutcome.Failure)]
    public void MapsRealApiStatusesAndDoesNotInventFailureLatency(IPStatus status, ProbeOutcome expected)
    {
        var result = IcmpProbe.MapStatus(status, 7);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == ProbeOutcome.Success ? 7L : null, result.LatencyMilliseconds);
    }

    [Fact]
    public void ErrorsHaveUsefulClassification()
    {
        Assert.Equal(ProbeError.PermissionDenied, IcmpProbe.MapException(new PingException("ping", new SocketException((int)SocketError.AccessDenied))).Error);
        Assert.Equal(ProbeError.Unsupported, IcmpProbe.MapException(new PlatformNotSupportedException()).Error);
        Assert.Equal(ProbeError.NetworkUnavailable, IcmpProbe.MapException(new SocketException((int)SocketError.NetworkDown)).Error);
        Assert.Equal(ProbeError.Timeout, IcmpProbe.MapException(new TimeoutException()).Error);
        Assert.Equal(ProbeOutcome.Unavailable, IcmpProbe.MapException(new IOException()).Outcome);
    }

    [Fact]
    public async Task ProbePreCancellationDoesNotStartNetworkRequest()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new IcmpProbe().ProbeAsync(IPAddress.Loopback, TimeSpan.FromMilliseconds(200), source.Token));
    }

    [Theory]
    [InlineData(0)] [InlineData(5001)]
    public async Task ProbeRejectsUnboundedTimeoutWithoutNetwork(int timeout)
    {
        var result = await new IcmpProbe().ProbeAsync(IPAddress.Loopback, TimeSpan.FromMilliseconds(timeout), TestContext.Current.CancellationToken);
        Assert.Equal(ProbeOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task ThresholdBoundaryAndRecoveryAreMonotonicAndUnique()
    {
        var clock = new ManualClock();
        var group = new GroupMonitor(Group(failMs: 2000), Guid.NewGuid(), new ScriptedProbe(Fail, Fail, Fail, Fail, Ok, Ok), clock);
        Assert.False((await group.PollAsync(TestContext.Current.CancellationToken)).OutageStarted);
        clock.Advance(1000); clock.JumpUtc(TimeSpan.FromDays(-3));
        Assert.False((await group.PollAsync(TestContext.Current.CancellationToken)).OutageStarted);
        clock.Advance(1000);
        var outage = await group.PollAsync(TestContext.Current.CancellationToken);
        Assert.True(outage.OutageStarted);
        Assert.Equal(TimeSpan.Zero, outage.ChangedOutage!.Elapsed);
        clock.Advance(1000);
        Assert.False((await group.PollAsync(TestContext.Current.CancellationToken)).OutageStarted);
        clock.Advance(1000);
        var recovery = await group.PollAsync(TestContext.Current.CancellationToken);
        Assert.True(recovery.Recovered);
        Assert.Equal(TimeSpan.FromSeconds(2), recovery.ChangedOutage!.Elapsed);
        clock.Advance(1000);
        Assert.False((await group.PollAsync(TestContext.Current.CancellationToken)).Recovered);
    }

    [Fact]
    public async Task EarlyRecoveryAndErrorResetUnconfirmedFailureStreak()
    {
        var clock = new ManualClock();
        var g = new GroupMonitor(Group(failMs: 2000), Guid.NewGuid(), new ScriptedProbe(Fail, Ok, Fail, Error, Fail, Fail), clock);
        for (var i = 0; i < 6; i++)
        {
            Assert.False((await g.PollAsync(TestContext.Current.CancellationToken)).OutageStarted);
            clock.Advance(1000);
        }
    }

    [Fact]
    public async Task ErrorDuringOutageNeverCreatesRecoveryAndRepeatedErrorsAreDeduplicated()
    {
        var clock = new ManualClock();
        var g = new GroupMonitor(Group(), Guid.NewGuid(), new ScriptedProbe(Fail, Fail, Error, Error, Ok), clock);
        await g.PollAsync(TestContext.Current.CancellationToken); clock.Advance(1000);
        Assert.True((await g.PollAsync(TestContext.Current.CancellationToken)).OutageStarted); clock.Advance(1000);
        var error = await g.PollAsync(TestContext.Current.CancellationToken);
        Assert.False(error.Recovered);
        Assert.True(error.Snapshot.ConfirmedOutage);
        Assert.True(error.ChangedOutage!.ContainsObservationGap);
        Assert.Single(error.Errors); clock.Advance(1000);
        Assert.Empty((await g.PollAsync(TestContext.Current.CancellationToken)).Errors); clock.Advance(1000);
        Assert.True((await g.PollAsync(TestContext.Current.CancellationToken)).Recovered);
    }

    [Fact]
    public async Task RotatesByNameStopsAtSuccessAndPreservesStaleAndLastSuccessSamples()
    {
        var c = Group(3); var clock = new ManualClock(); var probe = new ScriptedProbe(Ok, Ok, Fail, Fail, Ok);
        var g = new GroupMonitor(c, Guid.NewGuid(), probe, clock);
        var first = (await g.PollAsync(TestContext.Current.CancellationToken)).Snapshot;
        Assert.Equal(ProbeOutcome.NotChecked, first.Targets[1].Result.Outcome);
        Assert.True(first.Targets[0].CheckedInLatestCycle);
        clock.Advance(1000); var second = (await g.PollAsync(TestContext.Current.CancellationToken)).Snapshot;
        Assert.False(second.Targets[0].CheckedInLatestCycle);
        Assert.NotNull(second.Targets[0].LastSuccessUtc);
        clock.Advance(1000); var third = (await g.PollAsync(TestContext.Current.CancellationToken)).Snapshot;
        Assert.Equal(GroupConnectivity.Connected, third.Connectivity);
        Assert.Equal(new[] { "127.0.0.1", "127.0.0.2", "127.0.0.3", "127.0.0.1", "127.0.0.2" }, probe.Addresses.ToArray());
        Assert.All(third.Targets, t => Assert.True(t.CheckedInLatestCycle));
        Assert.Null(third.Targets[0].Result.LatencyMilliseconds);
    }

    [Fact]
    public async Task MixedErrorsWithoutSuccessAreIndeterminateInEveryRotation()
    {
        var clock = new ManualClock();
        var g = new GroupMonitor(Group(2), Guid.NewGuid(), new ScriptedProbe(Error, Fail, Fail, Error), clock);
        Assert.Equal(GroupConnectivity.Unavailable, (await g.PollAsync(TestContext.Current.CancellationToken)).Snapshot.Connectivity);
        clock.Advance(1000);
        Assert.Equal(GroupConnectivity.Unavailable, (await g.PollAsync(TestContext.Current.CancellationToken)).Snapshot.Connectivity);
    }

    [Fact]
    public async Task SkippedPollDoesNotRotateAndLargeObservationGapDoesNotConfirmOutage()
    {
        var clock = new ManualClock(); var probe = new ScriptedProbe(Fail, Fail);
        var g = new GroupMonitor(Group(), Guid.NewGuid(), probe, clock);
        await g.PollAsync(TestContext.Current.CancellationToken);
        clock.Advance(980); await g.PollAsync(TestContext.Current.CancellationToken);
        Assert.Single(probe.Addresses);
        clock.Advance(20000);
        var result = await g.PollAsync(TestContext.Current.CancellationToken);
        Assert.True(result.ObservationGap);
        Assert.False(result.OutageStarted);
    }

    [Fact]
    public async Task StartStopRestartRetainsHistoryCommentsAndDoesNotRecoverOnStop()
    {
        var clock = new ManualClock(); var group = Group();
        await using var service = new MonitoringService(new ScriptedProbe(Fail, Fail, Fail, Ok), clock);
        await service.StartAsync(Config(group));
        await UntilAsync(() => clock.PendingDelays == 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(Config(group)));
        clock.Advance(1000);
        await UntilAsync(() => service.Capture().Outages.Count == 1);
        var outage = Assert.Single(service.Capture().Outages);
        await service.UpdateCommentAsync(outage.Id, "ISP called, ticket \"42\"\nFollow up");
        await UntilAsync(() => clock.PendingDelays == 1); clock.Advance(1000);
        await UntilAsync(() => service.Capture().Outages[0].Elapsed > TimeSpan.Zero);
        Assert.Contains("ticket", service.Capture().Outages[0].Comment);
        await service.StopAsync();
        Assert.Equal(SessionState.Stopped, service.Capture().State);
        Assert.Null(service.Capture().Outages[0].RecoveredUtc);
        Assert.NotNull(service.Capture().Outages[0].ObservationEndedUtc);
        Assert.Single(service.Capture().Events, e => e.Kind == MonitorEventKind.OutageStarted);
        Assert.DoesNotContain(service.Capture().Events, e => e.Kind == MonitorEventKind.Recovery);
        var firstSession = service.Capture().SessionId;
        await service.StartAsync(Config(group));
        await UntilAsync(() => service.Capture().Groups[0].Connectivity == GroupConnectivity.Connected);
        await service.StopAsync();
        Assert.NotEqual(firstSession, service.Capture().SessionId);
        Assert.Single(service.Capture().Outages);
    }

    [Fact]
    public async Task ConcurrentGroupsAreBoundedAndShutdownCancelsActiveAndWaitingWorkers()
    {
        var entered = 0; var concurrent = 0; var max = 0;
        var probe = new DelegateProbe(async (_, _, token) =>
        {
            Interlocked.Increment(ref entered);
            var count = Interlocked.Increment(ref concurrent);
            Interlocked.Exchange(ref max, Math.Max(max, count));
            try { await Task.Delay(Timeout.Infinite, token); return Ok; }
            finally { Interlocked.Decrement(ref concurrent); }
        });
        await using var service = new MonitoringService(probe, maxConcurrency: 2);
        await service.StartAsync(Config(Enumerable.Range(0, 4).Select(_ => Group()).ToArray()));
        await UntilAsync(() => Volatile.Read(ref entered) == 2);
        await service.StopAsync();
        Assert.Equal(2, max);
        Assert.Equal(0, concurrent);
        Assert.Equal(2, entered);
        Assert.Empty(service.Capture().Outages);
        Assert.All(service.Capture().Groups, g => Assert.Equal(GroupConnectivity.NotChecked, g.Connectivity));
    }

    [Fact]
    public async Task ThrowingTargetDoesNotTerminateGroupAndLaterPeerCanSucceed()
    {
        var calls = 0;
        var probe = new DelegateProbe((_, _, _) => Interlocked.Increment(ref calls) == 1
            ? throw new UnauthorizedAccessException("denied") : Task.FromResult(Ok));
        var g = new GroupMonitor(Group(2), Guid.NewGuid(), probe, new ManualClock());
        var result = await g.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GroupConnectivity.Connected, result.Snapshot.Connectivity);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task BoundedStopRetainsOwnershipAndBlocksRestartUntilNativeLikeProbeDrains()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new DelegateProbe((_, _, _) => { entered.TrySetResult(); return release.Task; });
        var config = Config(Group());
        await using var service = new MonitoringService(probe, shutdownTimeout: TimeSpan.FromMilliseconds(30));
        try
        {
            await service.StartAsync(config);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<TimeoutException>(() => service.StopAsync());
            Assert.Equal(SessionState.Stopping, service.Capture().State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(config));
        }
        finally
        {
            release.TrySetResult(Ok);
            await UntilAsync(() => service.Capture().State == SessionState.Stopped);
            await service.StopAsync();
        }
        Assert.Empty(service.Capture().Outages);
        Assert.Null(service.Capture().Error);
    }

    [Fact]
    public async Task SameGroupCannotOverlapEvenWhenClockAdvancesWhileProbeIsPending()
    {
        var clock = new ManualClock(); var entered = 0;
        var release = new TaskCompletionSource<ProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new DelegateProbe((_, timeout, _) =>
        {
            Assert.Equal(TimeSpan.FromMilliseconds(200), timeout);
            Interlocked.Increment(ref entered); return release.Task;
        });
        await using var service = new MonitoringService(probe, clock);
        await service.StartAsync(Config(Group()));
        await UntilAsync(() => Volatile.Read(ref entered) == 1);
        clock.Advance(10000);
        Assert.Equal(1, Volatile.Read(ref entered));
        var stopping = service.StopAsync();
        release.TrySetResult(Ok);
        await stopping;
        Assert.Equal(1, entered);
    }

    [Fact]
    public async Task RecoveryEventIsNotRepeatedAndSincePreviousIsMeasuredFromObservedRecovery()
    {
        var clock = new ManualClock();
        var g = new GroupMonitor(Group(), Guid.NewGuid(), new ScriptedProbe(Fail, Fail, Ok, Ok, Fail, Fail), clock);
        await g.PollAsync(TestContext.Current.CancellationToken); clock.Advance(1000);
        Assert.True((await g.PollAsync(TestContext.Current.CancellationToken)).OutageStarted); clock.Advance(1000);
        Assert.True((await g.PollAsync(TestContext.Current.CancellationToken)).Recovered); clock.Advance(1000);
        Assert.False((await g.PollAsync(TestContext.Current.CancellationToken)).Recovered); clock.Advance(1000);
        await g.PollAsync(TestContext.Current.CancellationToken); clock.Advance(1000);
        var next = await g.PollAsync(TestContext.Current.CancellationToken);
        Assert.True(next.OutageStarted);
        Assert.Equal(TimeSpan.FromSeconds(3), next.ChangedOutage!.SincePrevious);
    }

    [Fact]
    public async Task DisposeSerializesAgainstAQueuedRestart()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new MonitoringService(new DelegateProbe((_, _, _) => { entered.TrySetResult(); return release.Task; }));
        var config = Config(Group());
        await service.StartAsync(config);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var disposing = service.DisposeAsync().AsTask();
        var restarting = service.StartAsync(config);
        release.TrySetResult(Ok);
        await disposing;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => restarting);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task RealLoopbackProbeAndEngineIntegrationOnlyWhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS") != "1") Assert.Skip("Opt-in real ICMP: set SYNUPS_REAL_NETWORK_TESTS=1. No public IP dependency.");
        var probe = await new IcmpProbe().ProbeAsync(IPAddress.Loopback, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(ProbeOutcome.Success, probe.Outcome);
        Assert.NotNull(probe.LatencyMilliseconds);
        await using var service = new MonitoringService();
        await service.StartAsync(Config(Group()));
        await UntilAsync(() => service.Capture().LastMeasurementUtc.HasValue);
        Assert.Equal(GroupConnectivity.Connected, Assert.Single(service.Capture().Groups).Connectivity);
        await service.StopAsync();
        Console.WriteLine($"Real ICMP 127.0.0.1: {probe.Outcome}, RTT {probe.LatencyMilliseconds}ms; engine start/measurement/stop succeeded.");
    }
}

internal sealed class DelegateProbe(Func<IPAddress, TimeSpan, CancellationToken, Task<ProbeResult>> send) : IConnectivityProbe
{
    public Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token) => send(address, timeout, token);
}
