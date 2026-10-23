using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Synups.Core.Diagnostics;

public enum DiagnosticKind { TcpConnect, Dns, PingQuality, NetworkConfiguration, TraceRoute, RawTcpSyn, UdpProbe }
public enum DiagnosticOutcome { Success, NoAnswer, Refused, Timeout, Unreachable, DnsFailure, Cancelled, PermissionDenied, Unsupported, NetworkError, MalformedResponse, Failed }
public enum DiagnosticCertainty { Observed, Interpretation, Unknown }
public enum DiagnosticState { Pending, Running, Completed, Partial, Cancelled, Failed, Interrupted }
public enum DnsRecordType { A = 1, AAAA = 28 }
public sealed record DiagnosticConsent(bool TargetApproved = false, bool AlternativeResolverApproved = false, bool ExternalServicesApproved = false);
public sealed record DiagnosticParameters
{
    public int Port { get; init; } = 443;
    public int SampleCount { get; init; } = 5;
    public int IntervalMilliseconds { get; init; } = 250;
    public int ProbeTimeoutMilliseconds { get; init; } = 1000;
    public int SessionTimeoutSeconds { get; init; } = 60;
    public int MaximumProbes { get; init; } = 100;
    public int MaximumHops { get; init; } = 16;
    public int ProbesPerHop { get; init; } = 2;
    public string? ResolverAddress { get; init; }
    public DnsRecordType RecordType { get; init; } = DnsRecordType.A;
    public int ReverseDnsTimeoutMilliseconds { get; init; } = 500;
    public bool ResolveHopNames { get; init; }
}
public sealed record DiagnosticRequest(Guid Id, string Target, IReadOnlyList<DiagnosticKind> Tests,
    DiagnosticParameters Parameters, DiagnosticConsent Consent, Guid? IncidentId = null, string ProfileId = "custom", int ProfileVersion = 1)
{
    public DiagnosticRequest ValidatedCopy()
    {
        if (Id == Guid.Empty || IncidentId == Guid.Empty || ProfileVersion < 1 || string.IsNullOrWhiteSpace(ProfileId) || ProfileId.Length > 80)
            throw new ArgumentException("Invalid diagnostic identity/profile.");
        if (!Consent.TargetApproved) throw new ArgumentException("Confirm the exact diagnostic target and tests before running.");
        var host = Target.Trim();
        if (host.Length is < 1 or > 253 || host.Any(char.IsControl) || host.Contains('/') || host.Contains('\\') || host.Contains(' ') ||
            (!IPAddress.TryParse(host, out _) && Uri.CheckHostName(host) != UriHostNameType.Dns)) throw new ArgumentException("Enter one valid hostname or unicast IP, without a scheme or port.");
        if (IPAddress.TryParse(host, out var ip) && (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.Broadcast) || ip.IsIPv6Multicast ||
            (ip.AddressFamily == AddressFamily.InterNetwork && ip.GetAddressBytes()[0] >= 224))) throw new ArgumentException("A unicast diagnostic endpoint is required.");
        if (Tests is null || Tests.Count is < 1 or > 7 || Tests.Distinct().Count() != Tests.Count || Tests.Any(t => !Enum.IsDefined(t))) throw new ArgumentException("Select distinct supported test kinds.");
        var p = Parameters;
        if (p.Port is < 1 or > 65535 || p.SampleCount is < 1 or > 100 || p.IntervalMilliseconds is < 0 or > 10000 ||
            p.ProbeTimeoutMilliseconds is < 10 or > 5000 || p.SessionTimeoutSeconds is < 1 or > 300 || p.MaximumProbes is < 1 or > 256 ||
            p.MaximumHops is < 1 or > 64 || p.ProbesPerHop is < 1 or > 3 || p.ReverseDnsTimeoutMilliseconds is < 10 or > 2000 || !Enum.IsDefined(p.RecordType)) throw new ArgumentException("Diagnostic parameters exceed finite safety bounds.");
        var cost = Tests.Sum(t => t switch { DiagnosticKind.PingQuality => p.SampleCount, DiagnosticKind.TraceRoute => p.MaximumHops * p.ProbesPerHop + (p.ResolveHopNames ? Math.Min(64, p.MaximumHops * p.ProbesPerHop) : 0), DiagnosticKind.NetworkConfiguration => 0, _ => 1 });
        if (cost > p.MaximumProbes) throw new ArgumentException($"Requested maximum {cost} probes exceeds the session cap {p.MaximumProbes}.");
        if (!string.IsNullOrWhiteSpace(p.ResolverAddress))
        {
            if (!IPAddress.TryParse(p.ResolverAddress, out var resolver) || resolver.Equals(IPAddress.Any) || resolver.Equals(IPAddress.IPv6Any) || resolver.IsIPv6Multicast || resolver.Equals(IPAddress.Broadcast) ||
                (resolver.AddressFamily == AddressFamily.InterNetwork && resolver.GetAddressBytes()[0] >= 224)) throw new ArgumentException("Choose a literal unicast DNS resolver IP.");
            if (!Consent.AlternativeResolverApproved) throw new ArgumentException("Explicit resolver disclosure requires approval.");
        }
        return this with { Target = host, Tests = Array.AsReadOnly(Tests.ToArray()) };
    }
}
public sealed record DiagnosticObservation(DateTimeOffset Utc, string Name, DiagnosticCertainty Certainty,
    double? Value = null, string? Unit = null, string? Text = null, int? Sample = null);
public sealed record PingStatistics(int Attempted, int Completed, int Successful, int Unanswered,
    double? MinimumMilliseconds, double? AverageMilliseconds, double? MaximumMilliseconds,
    double? StandardDeviationMilliseconds, double? JitterMilliseconds, double? LossPercent, bool Partial);
public sealed record DnsAnswer(string Name, DnsRecordType Type, string Address, uint TtlSeconds);
public sealed record DnsDiagnosticData(string Resolver, DnsRecordType QueryType, int? ResponseCode,
    IReadOnlyList<DnsAnswer> Answers, string Transport = "UDP", string CachePolicy = "Direct query; recursive resolver cache may apply; no local answer cache.");
public sealed record PathHop(int Hop, IReadOnlyList<string> Addresses, IReadOnlyList<double> RttMilliseconds, int Unanswered, string? Hostname = null);
public sealed record NetworkInterfaceSnapshot(string Id, string Name, string Type, string Status, int? IPv4Index, int? IPv6Index,
    IReadOnlyList<string> Addresses, IReadOnlyList<string> Gateways, IReadOnlyList<string> DnsServers);
public sealed record NetworkConfigurationSnapshot(DateTimeOffset Utc, string Provider, IReadOnlyList<NetworkInterfaceSnapshot> Interfaces,
    IReadOnlyList<string> Routes, IReadOnlyList<string> Limitations);
public sealed record DiagnosticTestResult(Guid Id, DiagnosticKind Kind, string Target, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc,
    DiagnosticOutcome Outcome, string Method, double ElapsedMilliseconds, IReadOnlyList<DiagnosticObservation> Observations, string? Error = null,
    PingStatistics? Statistics = null, DnsDiagnosticData? Dns = null, IReadOnlyList<PathHop>? Hops = null, NetworkConfigurationSnapshot? Network = null);
public sealed record DiagnosticSession(int Version, DiagnosticRequest Request, DateTimeOffset StartedUtc, DateTimeOffset? CompletedUtc,
    DiagnosticState State, IReadOnlyList<DiagnosticTestResult> Results, string Platform, string ApplicationVersion, string? Error = null, IReadOnlyList<DiagnosticObservation>? Analysis = null)
{
    public static DiagnosticSession Start(DiagnosticRequest request) => new(1, request, DateTimeOffset.UtcNow, null, DiagnosticState.Running, [],
        RuntimeInformation.OSDescription + "; " + RuntimeInformation.RuntimeIdentifier,
        typeof(DiagnosticSession).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
}
public sealed record DiagnosticProgress(Guid SessionId, DiagnosticKind? Kind, string Phase, int Completed, int Total, DiagnosticTestResult? SavedResult = null);
public interface IDiagnosticTest
{
    DiagnosticKind Kind { get; }
    Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation);
}
public interface IDiagnosticRunner : IAsyncDisposable
{
    Task<DiagnosticSession> RunAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress = null, CancellationToken cancellation = default);
}
public sealed record DiagnosticSessionSummary(Guid Id, string Target, Guid? IncidentId, DateTimeOffset StartedUtc, DiagnosticState State, string ProfileId)
{
    public override string ToString() => $"{StartedUtc:yyyy-MM-dd HH:mm:ss} UTC · {Target} · {State} · {ProfileId}";
}
public sealed record DiagnosticBaseline(Guid Id, Guid SessionId, string Target, string Label, DateTimeOffset SavedUtc);
public sealed record DiagnosticComparison(Guid Id, Guid BaselineId, Guid SessionId, DateTimeOffset Utc, IReadOnlyList<DiagnosticObservation> Differences);
public interface IDiagnosticRepository
{
    Task InitializeDiagnosticsAsync();
    Task SaveDiagnosticSessionAsync(DiagnosticSession session);
    Task<DiagnosticSession?> FindDiagnosticSessionAsync(Guid id);
    Task<IReadOnlyList<DiagnosticSessionSummary>> ReadDiagnosticSessionsAsync(int limit = 50, DateTimeOffset? before = null, Guid? incidentId = null, Guid? beforeId = null);
    Task<bool> DeleteDiagnosticSessionAsync(Guid id);
    Task<bool> DeleteDiagnosticBaselineAsync(Guid id);
    Task SaveDiagnosticBaselineAsync(DiagnosticBaseline baseline);
    Task<IReadOnlyList<DiagnosticBaseline>> ReadDiagnosticBaselinesAsync(int limit = 50);
    Task SaveDiagnosticComparisonAsync(DiagnosticComparison comparison);
}
public sealed record DiagnosticProfile(string Id, string Label, int Version, IReadOnlyList<DiagnosticKind> Tests, DiagnosticParameters Parameters)
{
    public override string ToString() => Label;
    public static IReadOnlyList<DiagnosticProfile> BuiltIn { get; } = [
        new("quick", "Quick Check", 1, [DiagnosticKind.TcpConnect, DiagnosticKind.PingQuality, DiagnosticKind.NetworkConfiguration], new()),
        new("dns", "DNS Focused", 1, [DiagnosticKind.Dns, DiagnosticKind.NetworkConfiguration], new()),
        new("deep", "Deep Dive", 1, [DiagnosticKind.Dns, DiagnosticKind.TcpConnect, DiagnosticKind.PingQuality, DiagnosticKind.TraceRoute, DiagnosticKind.NetworkConfiguration], new()),
        new("incident", "Incident Response", 1, [DiagnosticKind.Dns, DiagnosticKind.TcpConnect, DiagnosticKind.PingQuality, DiagnosticKind.NetworkConfiguration], new()),
        new("custom", "Custom", 1, [DiagnosticKind.TcpConnect], new())];
}
