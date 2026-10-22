using System.Net;
using System.Net.NetworkInformation;
using Synups.Core.Diagnostics;
using Synups.Core.Monitoring;
using Xunit;

namespace Synups.Tests;

public sealed class DiagnosticNetworkTests
{
    [Fact]
    public void QualityMathUsesPopulationDeviationAndSuccessAdjacency()
    {
        var stats = PingQualityTest.CalculateStatistics(6, [10, 20, null, 40, 50], true);
        Assert.Equal(6, stats.Attempted); Assert.Equal(5, stats.Completed); Assert.Equal(4, stats.Successful);
        Assert.Equal(10, stats.MinimumMilliseconds); Assert.Equal(30, stats.AverageMilliseconds); Assert.Equal(50, stats.MaximumMilliseconds);
        Assert.Equal(Math.Sqrt(250), stats.StandardDeviationMilliseconds!.Value, 10);
        Assert.Equal(10, stats.JitterMilliseconds); Assert.Equal(20, stats.LossPercent); Assert.True(stats.Partial);
    }

    [Fact]
    public void MissingAndSingleSamplesDoNotInventJitterOrLatency()
    {
        var absent = PingQualityTest.CalculateStatistics(3, [null, null, null], false);
        Assert.Equal(100, absent.LossPercent); Assert.Null(absent.AverageMilliseconds); Assert.Null(absent.StandardDeviationMilliseconds); Assert.Null(absent.JitterMilliseconds);
        var single = PingQualityTest.CalculateStatistics(1, [5000], false);
        Assert.Equal(0, single.StandardDeviationMilliseconds); Assert.Null(single.JitterMilliseconds);
        var empty = PingQualityTest.CalculateStatistics(0, [], true);
        Assert.Null(empty.LossPercent);
        Assert.Throws<ArgumentOutOfRangeException>(() => PingQualityTest.CalculateStatistics(1, [double.MaxValue], false));
        Assert.Throws<ArgumentOutOfRangeException>(() => PingQualityTest.CalculateStatistics(1, [double.NaN], false));
        Assert.Throws<ArgumentOutOfRangeException>(() => PingQualityTest.CalculateStatistics(0, [1], false));
    }

    [Fact]
    public async Task QualityCancellationRetainsCompletedAndIncompleteAttempts()
    {
        using var cancel = new CancellationTokenSource(); var calls = 0;
        var probe = new QualityProbe((_, _) =>
        {
            if (++calls == 1) return Task.FromResult(new ProbeResult(ProbeOutcome.Success, 12));
            cancel.Cancel(); throw new OperationCanceledException(cancel.Token);
        });
        var result = await new PingQualityTest(probe).ExecuteAsync(Request(DiagnosticKind.PingQuality), null, cancel.Token);
        Assert.Equal(DiagnosticOutcome.Cancelled, result.Outcome); Assert.Equal(2, result.Statistics!.Attempted);
        Assert.Equal(1, result.Statistics.Completed); Assert.Equal(12, result.Statistics.AverageMilliseconds); Assert.True(result.Statistics.Partial);
        Assert.True(result.ElapsedMilliseconds >= 0); Assert.Equal(TimeSpan.Zero, result.StartedUtc.Offset);
    }

    [Theory]
    [InlineData(ProbeError.PermissionDenied, DiagnosticOutcome.PermissionDenied)]
    [InlineData(ProbeError.Unsupported, DiagnosticOutcome.Unsupported)]
    public async Task MissingLocalIcmpCapabilityIsNotPacketLoss(ProbeError error, DiagnosticOutcome expected)
    {
        var result = await new PingQualityTest(new QualityProbe((_, _) => Task.FromResult(new ProbeResult(ProbeOutcome.Unavailable, Error: error))))
            .ExecuteAsync(Request(DiagnosticKind.PingQuality), null, TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Outcome); Assert.Equal(1, result.Statistics!.Attempted); Assert.Equal(0, result.Statistics.Completed);
        Assert.Null(result.Statistics.LossPercent); Assert.True(result.Statistics.Partial);
    }

    [Fact]
    public async Task TraceTtlBoundsAndUnansweredHopRemainObserved()
    {
        var probe = new TraceProbe((_, ttl, _) => Task.FromResult(ttl switch
        {
            1 => new TraceProbeReply(IPStatus.TtlExpired, IPAddress.Parse("192.0.2.1"), 2),
            2 => new TraceProbeReply(IPStatus.TimedOut),
            _ => new TraceProbeReply(IPStatus.Success, IPAddress.Loopback, 4),
        }));
        var result = await new TraceRouteTest(probe).ExecuteAsync(Request(DiagnosticKind.TraceRoute), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome); Assert.Equal(new[] { 1, 1, 2, 2, 3 }, probe.HopLimits);
        Assert.Equal(3, result.Hops!.Count); Assert.Equal(2, result.Hops[1].Unanswered);
        Assert.Contains(result.Observations, o => o.Name == "Possible ICMP response suppression" && o.Certainty == DiagnosticCertainty.Interpretation);
        Assert.DoesNotContain(result.Observations, o => o.Certainty == DiagnosticCertainty.Observed && o.Text?.Contains("forwarding loss", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task TraceStopsAtMaximumHopAndExposesPermissionFailure()
    {
        var never = new TraceProbe((_, _, _) => Task.FromResult(new TraceProbeReply(IPStatus.TimedOut)));
        var result = await new TraceRouteTest(never).ExecuteAsync(Request(DiagnosticKind.TraceRoute), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.NoAnswer, result.Outcome); Assert.Equal(6, never.HopLimits.Count); Assert.Equal(3, result.Hops!.Count);
        var denied = await new TraceRouteTest(new TraceProbe((_, _, _) => Task.FromResult(new TraceProbeReply(IPStatus.Unknown, Error: ProbeError.PermissionDenied))))
            .ExecuteAsync(Request(DiagnosticKind.TraceRoute), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.PermissionDenied, denied.Outcome); Assert.Equal(0, denied.Hops![0].Unanswered);
    }

    [Fact]
    public async Task TraceCancellationRetainsPartialHopAndSupportsIpv6Destination()
    {
        using var cancel = new CancellationTokenSource(); var calls = 0;
        var trace = new TraceProbe((_, _, _) =>
        {
            if (++calls == 1) return Task.FromResult(new TraceProbeReply(IPStatus.TtlExpired, IPAddress.Parse("2001:db8::1"), 2));
            cancel.Cancel(); throw new OperationCanceledException(cancel.Token);
        });
        var request = Request(DiagnosticKind.TraceRoute) with { Target = "::1" };
        var result = await new TraceRouteTest(trace).ExecuteAsync(request, null, cancel.Token);
        Assert.Equal(DiagnosticOutcome.Cancelled, result.Outcome); Assert.Single(result.Hops!); Assert.Single(result.Hops![0].RttMilliseconds);
        var success = await new TraceRouteTest(new TraceProbe((address, _, _) => Task.FromResult(new TraceProbeReply(IPStatus.Success, address, 0.1))))
            .ExecuteAsync(request, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, success.Outcome); Assert.Equal("::1", Assert.Single(success.Hops![0].Addresses));
    }

    [Fact]
    public void PathAnalysisDistinguishesChangedObservationSuppressionAndRepeatedResponder()
    {
        PathHop[] baseline = [new(1, ["192.0.2.1"], [1, 2], 0), new(2, [], [], 2)];
        PathHop[] current = [new(1, ["192.0.2.2"], [1], 1), new(2, ["192.0.2.2"], [2], 0)];
        var analyzed = PathAnalysis.Analyze(current, DateTimeOffset.UtcNow);
        Assert.Contains(analyzed, o => o.Name == "Repeated responder" && o.Certainty == DiagnosticCertainty.Interpretation);
        var changed = PathAnalysis.Compare(baseline, current, DateTimeOffset.UtcNow);
        Assert.Single(changed, o => o.Certainty == DiagnosticCertainty.Observed);
        Assert.Contains("Hop 1", changed[0].Text);
        Assert.Contains(PathAnalysis.AnalyzeEvidence([]), o => o.Name == "Rate-limit evidence" && o.Certainty == DiagnosticCertainty.Unknown);
    }

    [Fact]
    public void RateLimitEvidenceExaminesDestinationAndProtocolWithoutGuaranteeingCause()
    {
        var utc = DateTimeOffset.UtcNow;
        var echo = new DiagnosticTestResult(Guid.NewGuid(), DiagnosticKind.PingQuality, "127.0.0.1", utc, utc,
            DiagnosticOutcome.Timeout, "test fixture", 1, [], Statistics: PingQualityTest.CalculateStatistics(3, [null, null, 2], false));
        var tcp = echo with { Kind = DiagnosticKind.TcpConnect, Outcome = DiagnosticOutcome.Success, Statistics = null };
        var result = PathAnalysis.AnalyzeEvidence([echo, tcp]);
        Assert.Contains(result, o => o.Name == "Possible end-to-end echo loss" && o.Certainty == DiagnosticCertainty.Interpretation);
        Assert.Contains(result, o => o.Name == "Protocol comparison" && o.Certainty == DiagnosticCertainty.Interpretation);
    }

    [Fact]
    public async Task NetworkConfigurationPreservesPartialSnapshotAndTypedUnsupported()
    {
        using var cancel = new CancellationTokenSource();
        var data = new NetworkConfigurationSnapshot(DateTimeOffset.UtcNow, "fixture", [new("1", "fixture", "Ethernet", "Up", 1, null, ["192.0.2.2"], [], [])], [], ["Fixture limitation"]);
        var result = await new NetworkConfigurationTest(new SnapshotProvider(_ => { cancel.Cancel(); return Task.FromResult(data); }))
            .ExecuteAsync(Request(DiagnosticKind.NetworkConfiguration), null, cancel.Token);
        Assert.Equal(DiagnosticOutcome.Cancelled, result.Outcome); Assert.Equal(data, result.Network); Assert.Single(result.Network!.Interfaces);
        var unsupported = await new NetworkConfigurationTest(new SnapshotProvider(_ => throw new PlatformNotSupportedException("Fixture")))
            .ExecuteAsync(Request(DiagnosticKind.NetworkConfiguration), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Unsupported, unsupported.Outcome);
    }

    [Fact]
    public async Task OptionalHopNamesRetainRawAddressWhenConfiguredResolverUnavailable()
    {
        var request = Request(DiagnosticKind.TraceRoute);
        request = request with { Parameters = request.Parameters with { ResolveHopNames = true } };
        var trace = new TraceRouteTest(new TraceProbe((address, _, _) => Task.FromResult(new TraceProbeReply(IPStatus.Success, address, 1))),
            reverseCacheFactory: (_, token) => new ReverseDnsCache(sessionCancellation: token, configuredResolverProvider: () => null));
        var result = await trace.ExecuteAsync(request, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome); Assert.Null(result.Hops![0].Hostname);
        Assert.Equal("127.0.0.1", Assert.Single(result.Hops[0].Addresses));
        Assert.Contains(result.Observations, o => o.Name == "Reverse DNS policy");
    }

    [Fact]
    public async Task NativeLoopbackQualityAndTraceOnlyWithExplicitOptIn()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS") != "1") Assert.Skip("Opt-in native loopback ICMP quality and TTL tracing; no public target.");
        var quality = await new PingQualityTest().ExecuteAsync(Request(DiagnosticKind.PingQuality) with { Parameters = Request(DiagnosticKind.PingQuality).Parameters with { SampleCount = 2 } }, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, quality.Outcome); Assert.Equal(2, quality.Statistics!.Successful);
        var trace = await new TraceRouteTest().ExecuteAsync(Request(DiagnosticKind.TraceRoute), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, trace.Outcome); Assert.Single(trace.Hops!); Assert.Contains("127.0.0.1", trace.Hops![0].Addresses);
    }

    [Fact]
    public async Task NativeSnapshotOnlyWithExplicitOptIn()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS") != "1") Assert.Skip("Opt-in read-only native interface and default-route snapshot.");
        var result = await new NetworkConfigurationTest().ExecuteAsync(Request(DiagnosticKind.NetworkConfiguration), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome); Assert.NotEmpty(result.Network!.Interfaces); Assert.NotEmpty(result.Network.Limitations);
    }

    private static DiagnosticRequest Request(DiagnosticKind kind) => new(Guid.NewGuid(), "127.0.0.1", [kind],
        new() { SampleCount = 3, IntervalMilliseconds = 0, MaximumHops = 3, ProbesPerHop = 2 }, new(true));
    private sealed class QualityProbe(Func<IPAddress, CancellationToken, Task<ProbeResult>> run) : IConnectivityProbe
    {
        public Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) => run(address, cancellationToken);
    }
    private sealed class TraceProbe(Func<IPAddress, int, CancellationToken, Task<TraceProbeReply>> run) : ITraceRouteProbe
    {
        public List<int> HopLimits { get; } = [];
        public Task<TraceProbeReply> ProbeAsync(IPAddress address, int hopLimit, TimeSpan timeout, CancellationToken cancellation)
        { HopLimits.Add(hopLimit); return run(address, hopLimit, cancellation); }
    }
    private sealed class SnapshotProvider(Func<CancellationToken, Task<NetworkConfigurationSnapshot>> run) : INetworkConfigurationProvider
    { public Task<NetworkConfigurationSnapshot> CaptureAsync(CancellationToken cancellation) => run(cancellation); }
}
