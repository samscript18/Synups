using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Synups.Core.Monitoring;

namespace Synups.Core.Diagnostics;

/// <summary>Bounded ICMP echo quality measurements, independent of the continuous monitor.</summary>
public sealed class PingQualityTest(IConnectivityProbe? probe = null,
    Func<string, CancellationToken, Task<IPAddress>>? resolve = null) : IDiagnosticTest
{
    private readonly IConnectivityProbe probe = probe ?? new IcmpProbe();
    private readonly Func<string, CancellationToken, Task<IPAddress>> resolve = resolve ?? IcmpDiagnosticSupport.ResolveAsync;
    public DiagnosticKind Kind => DiagnosticKind.PingQuality;

    public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        request = request.ValidatedCopy();
        var started = DateTimeOffset.UtcNow; var watch = Stopwatch.StartNew();
        var observations = new List<DiagnosticObservation>(); var samples = new List<double?>();
        var attempted = 0; var outcome = DiagnosticOutcome.NoAnswer; string? error = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(request.Parameters.SessionTimeoutSeconds));
        try
        {
            var address = await resolve(request.Target, deadline.Token).ConfigureAwait(false);
            observations.Add(new(DateTimeOffset.UtcNow, "Resolved target address", DiagnosticCertainty.Observed, Text: address.ToString()));
            for (var i = 0; i < request.Parameters.SampleCount; i++)
            {
                deadline.Token.ThrowIfCancellationRequested(); attempted++;
                var reply = await probe.ProbeAsync(address, TimeSpan.FromMilliseconds(request.Parameters.ProbeTimeoutMilliseconds), deadline.Token).ConfigureAwait(false);
                observations.Add(new(DateTimeOffset.UtcNow, "ICMP echo outcome", DiagnosticCertainty.Observed,
                    reply.LatencyMilliseconds, "ms", $"{reply.Outcome}; {reply.Error}; {reply.Detail}", i + 1));
                // An unavailable local capability is not a completed network measurement or packet loss.
                if (reply.Outcome is ProbeOutcome.Unavailable or ProbeOutcome.NotChecked)
                { outcome = IcmpDiagnosticSupport.Map(reply.Error); error = reply.Detail; break; }
                samples.Add(reply.Outcome == ProbeOutcome.Success && reply.LatencyMilliseconds is >= 0 ? reply.LatencyMilliseconds.Value : null);
                outcome = samples.Any(s => s.HasValue) ? DiagnosticOutcome.Success : IcmpDiagnosticSupport.Map(reply.Error);
                progress?.Report(new(request.Id, Kind, "ICMP echo quality", samples.Count, request.Parameters.SampleCount));
                if (i + 1 < request.Parameters.SampleCount && request.Parameters.IntervalMilliseconds > 0)
                    await Task.Delay(request.Parameters.IntervalMilliseconds, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout; error = "Stopped; retained completed samples and incomplete attempt count."; }
        catch (SocketException ex) { outcome = DiagnosticOutcome.DnsFailure; error = ex.Message; }
        catch (Exception ex) { outcome = IcmpDiagnosticSupport.Map(IcmpProbe.MapException(ex).Error); error = ex.Message; }
        var statistics = CalculateStatistics(attempted, samples, samples.Count < request.Parameters.SampleCount);
        observations.Add(new(DateTimeOffset.UtcNow, "Statistics definition", DiagnosticCertainty.Observed, Text:
            "Population standard deviation over successful RTTs. Jitter is the mean absolute difference between adjacent completed successful probes; an unanswered probe breaks adjacency. Unanswered percentage uses completed network measurements only; local unavailable and incomplete attempts are excluded. Cancelled samples are partial."));
        observations.Add(new(DateTimeOffset.UtcNow, "ICMP interpretation", DiagnosticCertainty.Unknown, Text:
            "An unanswered echo can reflect filtering, suppression, rate limiting, or reachability failure; it does not establish application traffic loss."));
        return new(Guid.NewGuid(), Kind, request.Target, started, DateTimeOffset.UtcNow, outcome,
            $"ICMP echo; samples={request.Parameters.SampleCount}; interval={request.Parameters.IntervalMilliseconds}ms; timeout={request.Parameters.ProbeTimeoutMilliseconds}ms; payload=0 bytes",
            watch.Elapsed.TotalMilliseconds, observations.ToArray(), error, statistics);
    }

    public static PingStatistics CalculateStatistics(int attempted, IReadOnlyList<double?> completed, bool partial)
    {
        if (attempted < completed.Count || completed.Any(v => v is { } n && (!double.IsFinite(n) || n < 0 || n > 5000)))
            throw new ArgumentOutOfRangeException(nameof(completed), "RTTs must be finite, nonnegative, and within the maximum probe deadline.");
        var successes = completed.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var mean = successes.Length > 0 ? successes.Average() : (double?)null;
        var differences = completed.Zip(completed.Skip(1)).Where(p => p.First.HasValue && p.Second.HasValue)
            .Select(p => Math.Abs(p.Second!.Value - p.First!.Value)).ToArray();
        return new(attempted, completed.Count, successes.Length, completed.Count - successes.Length,
            successes.Length > 0 ? successes.Min() : null, mean, successes.Length > 0 ? successes.Max() : null,
            mean.HasValue ? Math.Sqrt(successes.Sum(v => (v - mean.Value) * (v - mean.Value)) / successes.Length) : null,
            differences.Length > 0 ? differences.Average() : null,
            completed.Count > 0 ? 100d * (completed.Count - successes.Length) / completed.Count : null, partial);
    }
}

public sealed record TraceProbeReply(IPStatus Status, IPAddress? Address = null, double? ReplyMilliseconds = null,
    ProbeError Error = ProbeError.None, string? Detail = null);
public interface ITraceRouteProbe
{
    Task<TraceProbeReply> ProbeAsync(IPAddress address, int hopLimit, TimeSpan timeout, CancellationToken cancellation);
}

/// <summary>Real OS ICMP echo with an explicit TTL/hop limit. No simulated or TCP/UDP fallback.</summary>
public sealed class IcmpTraceRouteProbe : ITraceRouteProbe
{
    public async Task<TraceProbeReply> ProbeAsync(IPAddress address, int hopLimit, TimeSpan timeout, CancellationToken cancellation)
    {
        if (hopLimit is < 1 or > 64 || timeout.TotalMilliseconds is < 10 or > 5000) throw new ArgumentOutOfRangeException(nameof(hopLimit));
        cancellation.ThrowIfCancellationRequested();
        try
        {
            using var ping = new Ping(); var watch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(address, timeout, Array.Empty<byte>(), new PingOptions(hopLimit, false), cancellation).ConfigureAwait(false);
            var elapsed = watch.Elapsed.TotalMilliseconds;
            return new(reply.Status, reply.Address, reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded ? elapsed : null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception ex) { var mapped = IcmpProbe.MapException(ex); return new(IPStatus.Unknown, Error: mapped.Error, Detail: mapped.Detail); }
    }
}

public sealed class TraceRouteTest(ITraceRouteProbe? probe = null,
    Func<string, CancellationToken, Task<IPAddress>>? resolve = null,
    Func<DiagnosticParameters, CancellationToken, ReverseDnsCache>? reverseCacheFactory = null) : IDiagnosticTest
{
    private readonly ITraceRouteProbe probe = probe ?? new IcmpTraceRouteProbe();
    private readonly Func<string, CancellationToken, Task<IPAddress>> resolve = resolve ?? IcmpDiagnosticSupport.ResolveAsync;
    public DiagnosticKind Kind => DiagnosticKind.TraceRoute;
    public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        request = request.ValidatedCopy(); var started = DateTimeOffset.UtcNow; var watch = Stopwatch.StartNew();
        var observations = new List<DiagnosticObservation>(); var hops = new List<PathHop>();
        var outcome = DiagnosticOutcome.NoAnswer; string? error = null; var attempted = 0; var reached = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(request.Parameters.SessionTimeoutSeconds));
        try
        {
            var address = await resolve(request.Target, deadline.Token).ConfigureAwait(false);
            observations.Add(new(DateTimeOffset.UtcNow, "Resolved target address", DiagnosticCertainty.Observed, Text: address.ToString()));
            var finished = false;
            for (var ttl = 1; ttl <= request.Parameters.MaximumHops && !finished; ttl++)
            {
                var addresses = new List<string>(); var times = new List<double>(); var unanswered = 0;
                // Add even a partially completed hop when cancellation interrupts the probe sequence.
                try
                {
                    for (var sample = 0; sample < request.Parameters.ProbesPerHop; sample++)
                    {
                        deadline.Token.ThrowIfCancellationRequested(); attempted++;
                        var reply = await probe.ProbeAsync(address, ttl, TimeSpan.FromMilliseconds(request.Parameters.ProbeTimeoutMilliseconds), deadline.Token).ConfigureAwait(false);
                        observations.Add(new(DateTimeOffset.UtcNow, $"Hop {ttl} ICMP status", DiagnosticCertainty.Observed,
                            reply.ReplyMilliseconds, "ms", $"{reply.Status}; {reply.Address}; {reply.Error}; {reply.Detail}", sample + 1));
                        if (reply.Error != ProbeError.None)
                        { outcome = IcmpDiagnosticSupport.Map(reply.Error); error = reply.Detail; finished = true; break; }
                        if (reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded)
                        {
                            if (reply.Address is { } responder && !responder.Equals(IPAddress.Any) && !responder.Equals(IPAddress.IPv6Any)) addresses.Add(responder.ToString());
                            if (reply.ReplyMilliseconds is { } elapsed && double.IsFinite(elapsed) && elapsed >= 0) times.Add(elapsed);
                        }
                        else unanswered++;
                        if (reply.Status == IPStatus.Success && reply.Address?.Equals(address) == true)
                        { outcome = DiagnosticOutcome.Success; finished = true; reached = true; }
                        else if (reply.Status is not (IPStatus.Success or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded or IPStatus.TimedOut))
                        { outcome = DiagnosticOutcome.Unreachable; finished = true; }
                        progress?.Report(new(request.Id, Kind, $"ICMP TTL {ttl}", attempted, request.Parameters.MaximumHops * request.Parameters.ProbesPerHop));
                        if (finished) break;
                        if (request.Parameters.IntervalMilliseconds > 0 && (ttl < request.Parameters.MaximumHops || sample + 1 < request.Parameters.ProbesPerHop))
                            await Task.Delay(request.Parameters.IntervalMilliseconds, deadline.Token).ConfigureAwait(false);
                    }
                }
                finally { hops.Add(new(ttl, addresses.Distinct().ToArray(), times.ToArray(), unanswered)); }
            }
            if (request.Parameters.ResolveHopNames && !deadline.IsCancellationRequested)
            {
                try
                {
                    await using var reverse = reverseCacheFactory?.Invoke(request.Parameters, deadline.Token) ?? new ReverseDnsCache(
                        capacity: 32, maximumConcurrent: 1, timeoutMilliseconds: request.Parameters.ReverseDnsTimeoutMilliseconds,
                        sessionCancellation: deadline.Token,
                        resolver: request.Parameters.ResolverAddress is { Length: > 0 } resolver ? new IPEndPoint(IPAddress.Parse(resolver), 53) : null);
                    var names = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var index = 0; index < hops.Count; index++)
                    {
                        deadline.Token.ThrowIfCancellationRequested();
                        // One representative per hop; observed ECMP addresses remain intact. Max 64 distinct queries.
                        var raw = hops[index].Addresses.FirstOrDefault();
                        if (raw is null) continue;
                        if (!names.TryGetValue(raw, out var name))
                        {
                            if (names.Count >= 64) continue;
                            name = await reverse.ResolveAsync(IPAddress.Parse(raw), deadline.Token).ConfigureAwait(false);
                            names.Add(raw, name);
                        }
                        if (!string.Equals(name, raw, StringComparison.Ordinal)) hops[index] = hops[index] with { Hostname = name };
                    }
                    deadline.Token.ThrowIfCancellationRequested();
                    observations.Add(new(DateTimeOffset.UtcNow, "Reverse DNS policy", DiagnosticCertainty.Observed, Text:
                        $"Optional PTR lookups for one representative address per hop; {names.Count} distinct candidates; maximum64; session cache32; concurrency1; timeout={request.Parameters.ReverseDnsTimeoutMilliseconds}ms. Failures retain literal addresses. A PTR name is not identity verification."));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                { observations.Add(new(DateTimeOffset.UtcNow, "Reverse DNS unavailable", DiagnosticCertainty.Unknown, Text: $"{ex.Message}; observed literal hop addresses retained.")); }
            }
        }
        catch (OperationCanceledException) { outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout; error = "Trace stopped; completed responses and partial hop retained."; }
        catch (SocketException ex) { outcome = DiagnosticOutcome.DnsFailure; error = ex.Message; }
        catch (Exception ex) { outcome = IcmpDiagnosticSupport.Map(IcmpProbe.MapException(ex).Error); error = ex.Message; }
        observations.Add(new(DateTimeOffset.UtcNow, "Probes attempted", DiagnosticCertainty.Observed, attempted, "probes"));
        observations.Add(new(DateTimeOffset.UtcNow, "Destination echo observed", DiagnosticCertainty.Observed, Text: reached.ToString()));
        observations.AddRange(PathAnalysis.Analyze(hops, DateTimeOffset.UtcNow));
        return new(Guid.NewGuid(), Kind, request.Target, started, DateTimeOffset.UtcNow, outcome,
            $"ICMP echo TTL/hop-limit=1..{request.Parameters.MaximumHops}; probes/hop<={request.Parameters.ProbesPerHop}; timeout={request.Parameters.ProbeTimeoutMilliseconds}ms; RTT=monotonic send-to-reply elapsed (includes local scheduling); payload=0 bytes",
            watch.Elapsed.TotalMilliseconds, observations.ToArray(), error, Hops: hops.ToArray());
    }
}

internal static class IcmpDiagnosticSupport
{
    internal static async Task<IPAddress> ResolveAsync(string target, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (IPAddress.TryParse(target, out var literal)) return literal;
        var addresses = await BoundedHostnameResolver.Shared.ResolveAsync(target, cancellation).ConfigureAwait(false);
        return addresses.FirstOrDefault(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            ?? throw new SocketException((int)SocketError.HostNotFound);
    }
    internal static DiagnosticOutcome Map(ProbeError error) => error switch
    {
        ProbeError.Timeout => DiagnosticOutcome.Timeout, ProbeError.Unreachable => DiagnosticOutcome.Unreachable,
        ProbeError.PermissionDenied => DiagnosticOutcome.PermissionDenied, ProbeError.Unsupported => DiagnosticOutcome.Unsupported,
        _ => DiagnosticOutcome.NetworkError,
    };
}
