using System.Diagnostics;
using System.Net.NetworkInformation;
using Synups.Core.Network;

namespace Synups.Core.Diagnostics;

public interface INetworkConfigurationProvider
{
    Task<NetworkConfigurationSnapshot> CaptureAsync(CancellationToken cancellation);
}

/// <summary>Read-only local interface data and bounded native default-route discovery.</summary>
public sealed class NativeNetworkConfigurationProvider(GatewayDiscovery? gateways = null) : INetworkConfigurationProvider
{
    private readonly GatewayDiscovery gateways = gateways ?? new(timeout: TimeSpan.FromSeconds(3));
    public async Task<NetworkConfigurationSnapshot> CaptureAsync(CancellationToken cancellation)
    {
        // .NET interface enumeration is synchronous. Own and drain one worker; do not abandon OS calls.
        var snapshot = await Task.Run(() => CaptureInterfaces(cancellation), CancellationToken.None).ConfigureAwait(false);
        if (cancellation.IsCancellationRequested) return snapshot with { Limitations = snapshot.Limitations.Append("Cancelled; default-route discovery was not started.").ToArray() };
        var routes = await gateways.DiscoverAsync(cancellation).ConfigureAwait(false);
        return snapshot with
        {
            Routes = routes.Candidates.Select(c => $"default via {c.Address}; interface={c.InterfaceName}; metric={c.Metric?.ToString() ?? "not reported"}").ToArray(),
            Limitations = snapshot.Limitations.Append($"Default routes: {routes.Status}. {routes.Detail}").ToArray()
        };
    }

    private static NetworkConfigurationSnapshot CaptureInterfaces(CancellationToken cancellation)
    {
        var interfaces = new List<NetworkInterfaceSnapshot>();
        var limitations = new List<string>
        {
            "Read-only snapshot; no settings were changed. Routes contain default-gateway candidates only, not a full route table or destination route selection. VPN and split routes may override defaults.",
            "DNS server lists are OS interface configuration, not proof of which resolver a particular application uses. Per-link DNS policies, DNS-over-HTTPS, search domains and proxy settings are not captured.",
            "Interface enumeration uses synchronous .NET OS APIs and is drained before completion; cancellation is checked between adapters and may wait for an in-flight OS call."
        };
        NetworkInterface[] adapters;
        try { adapters = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (Exception ex) { limitations.Add($"Interface enumeration unavailable: {ex.Message}"); return Snapshot(); }
        if (adapters.Length > 256) limitations.Add("Interface snapshot capped at 256 adapters.");
        foreach (var adapter in adapters.Take(256))
        {
            if (cancellation.IsCancellationRequested) { limitations.Add("Interface snapshot cancelled; retained adapters are partial."); break; }
            try
            {
                var properties = adapter.GetIPProperties();
                int? v4 = null, v6 = null;
                try { v4 = properties.GetIPv4Properties()?.Index; } catch (Exception ex) { limitations.Add($"{adapter.Name} IPv4 index unavailable: {ex.Message}"); }
                try { v6 = properties.GetIPv6Properties()?.Index; } catch (Exception ex) { limitations.Add($"{adapter.Name} IPv6 index unavailable: {ex.Message}"); }
                IReadOnlyList<string> ReadAddresses(Func<IEnumerable<string>> read, string label)
                {
                    try { return read().Take(256).ToArray(); }
                    catch (Exception ex) { limitations.Add($"{adapter.Name} {label} unavailable: {ex.Message}"); return []; }
                }
                string WithPrefix(UnicastIPAddressInformation address)
                {
                    try { return $"{address.Address}/{address.PrefixLength}"; }
                    catch (Exception ex) { limitations.Add($"{adapter.Name} prefix length unavailable: {ex.Message}"); return address.Address.ToString(); }
                }
                interfaces.Add(new(adapter.Id, adapter.Name, adapter.NetworkInterfaceType.ToString(), adapter.OperationalStatus.ToString(), v4, v6,
                    ReadAddresses(() => properties.UnicastAddresses.Select(WithPrefix), "addresses/prefix lengths"),
                    ReadAddresses(() => properties.GatewayAddresses.Select(a => a.Address.ToString()), "gateways"),
                    ReadAddresses(() => properties.DnsAddresses.Select(a => a.ToString()), "DNS servers")));
            }
            catch (Exception ex) { limitations.Add($"An adapter could not be read: {ex.Message}"); }
        }
        return Snapshot();
        NetworkConfigurationSnapshot Snapshot() => new(DateTimeOffset.UtcNow,
            $".NET NetworkInterface; {GatewayDiscovery.CurrentPlatform} native read-only default-route provider", interfaces.ToArray(), [], limitations.ToArray());
    }
}

public sealed class NetworkConfigurationTest(INetworkConfigurationProvider? provider = null) : IDiagnosticTest
{
    private readonly INetworkConfigurationProvider provider = provider ?? new NativeNetworkConfigurationProvider();
    public DiagnosticKind Kind => DiagnosticKind.NetworkConfiguration;
    public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        request = request.ValidatedCopy(); var start = DateTimeOffset.UtcNow; var watch = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(request.Parameters.SessionTimeoutSeconds));
        NetworkConfigurationSnapshot? snapshot = null; var outcome = DiagnosticOutcome.Success; string? error = null;
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            progress?.Report(new(request.Id, Kind, "Read-only local network snapshot", 0, 1));
            snapshot = await provider.CaptureAsync(deadline.Token).ConfigureAwait(false);
            if (snapshot.Interfaces.Count == 0 && snapshot.Routes.Count == 0) { outcome = DiagnosticOutcome.NoAnswer; error = "No interface or default route data was available; inspect provider limitations."; }
            if (deadline.IsCancellationRequested) { outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout; error = "Snapshot stopped; retained provider data may be partial."; }
            progress?.Report(new(request.Id, Kind, "Network snapshot retained", 1, 1));
        }
        catch (OperationCanceledException) { outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout; error = "Local snapshot cancelled or timed out."; }
        catch (PlatformNotSupportedException ex) { outcome = DiagnosticOutcome.Unsupported; error = ex.Message; }
        catch (UnauthorizedAccessException ex) { outcome = DiagnosticOutcome.PermissionDenied; error = ex.Message; }
        catch (Exception ex) { outcome = DiagnosticOutcome.NetworkError; error = ex.Message; }
        var observations = snapshot?.Limitations.Select(l => new DiagnosticObservation(snapshot.Utc, "Snapshot limitation", DiagnosticCertainty.Unknown, Text: l)).ToArray() ?? [];
        return new(Guid.NewGuid(), Kind, request.Target, start, DateTimeOffset.UtcNow, outcome,
            "Read-only .NET interface enumeration and native OS default-route discovery; no probes or configuration changes", watch.Elapsed.TotalMilliseconds,
            observations, error, Network: snapshot);
    }
}
