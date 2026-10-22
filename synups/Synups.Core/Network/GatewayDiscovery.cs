using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Synups.Core.Network;

public enum RoutePlatform { Windows, MacOS, Linux, Unsupported }
public enum DiscoveryStatus { Found, NoGateway, Unavailable, Cancelled, TimedOut, Unsupported }
public sealed record GatewayCandidate(string Address, string InterfaceName, int? Metric = null)
{
    public override string ToString() => $"{Address} · {InterfaceName}" + (Metric is { } m ? $" · route metric {m}" : "");
}
public sealed record GatewayDiscoveryResult(DiscoveryStatus Status, IReadOnlyList<GatewayCandidate> Candidates, string Detail);
public sealed record RouteCommand(string FileName, IReadOnlyList<string> Arguments);
public sealed record RouteOutput(int ExitCode, string Text, string Error);
public interface IRouteCommandRunner { Task<RouteOutput> RunAsync(RouteCommand command, CancellationToken cancellation); }

/// <summary>Read-only native routing queries. No ping, scan, route or interface mutation.</summary>
public sealed class GatewayDiscovery(IRouteCommandRunner? runner = null, RoutePlatform? platform = null, TimeSpan? timeout = null)
{
    private readonly IRouteCommandRunner runner = runner ?? new RouteCommandRunner();
    private readonly RoutePlatform platform = platform ?? CurrentPlatform;
    private readonly TimeSpan timeout = timeout ?? TimeSpan.FromSeconds(8);
    public static RoutePlatform CurrentPlatform => OperatingSystem.IsWindows() ? RoutePlatform.Windows :
        OperatingSystem.IsMacOS() ? RoutePlatform.MacOS : OperatingSystem.IsLinux() ? RoutePlatform.Linux : RoutePlatform.Unsupported;
    public async Task<GatewayDiscoveryResult> DiscoverAsync(CancellationToken cancellation)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(15)) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (platform == RoutePlatform.Unsupported) return new(DiscoveryStatus.Unsupported, [], "Default-route discovery is unavailable on this OS. Configure a literal IP manually.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        try
        {
            var candidates = new List<GatewayCandidate>(); var errors = new List<string>();
            foreach (var command in Commands(platform))
            {
                deadline.Token.ThrowIfCancellationRequested();
                var result = await runner.RunAsync(command, deadline.Token).ConfigureAwait(false);
                if (result.ExitCode == 0) candidates.AddRange(Parse(platform, result.Text));
                else errors.Add(result.Error.Trim());
            }
            var unique = candidates.DistinctBy(c => (c.Address, c.InterfaceName)).Take(100).ToArray();
            return unique.Length > 0
                ? new(DiscoveryStatus.Found, unique, "Read-only OS default-route snapshot. Select and confirm an address; no target or probe was created. VPN/split routes may override particular destinations. A gateway response does not prove Internet availability." + (errors.Count > 0 ? " Some address-family queries were unavailable." : ""))
                : new(errors.Count == 0 ? DiscoveryStatus.NoGateway : DiscoveryStatus.Unavailable, [],
                    "No addressable default gateway was found. Point-to-point/VPN routes can have no gateway IP. Manual targets remain available." + (errors.Count > 0 ? " OS feedback: " + string.Join("; ", errors).Trim()[..Math.Min(500, string.Join("; ", errors).Trim().Length)] : ""));
        }
        catch (OperationCanceledException) { return new(cancellation.IsCancellationRequested ? DiscoveryStatus.Cancelled : DiscoveryStatus.TimedOut, [], "Discovery cancelled or timed out; no configuration or network setting changed."); }
        catch (Exception ex)
        { return new(DiscoveryStatus.Unavailable, [], $"Gateway discovery unavailable: {ex.Message}. Configure a literal IP manually."); }
    }
    public static IReadOnlyList<RouteCommand> Commands(RoutePlatform platform) => platform switch
    {
        RoutePlatform.MacOS => [new("/sbin/route", ["-n", "get", "default"]), new("/sbin/route", ["-n", "get", "-inet6", "default"])],
        RoutePlatform.Linux => [new(LinuxIp(), ["-j", "-4", "route", "show", "default"]), new(LinuxIp(), ["-j", "-6", "route", "show", "default"])],
        RoutePlatform.Windows => [new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference='Stop'; $up=@(Get-NetIPInterface | Where-Object ConnectionState -eq 'Connected' | Select-Object -ExpandProperty InterfaceIndex); Get-NetRoute -PolicyStore ActiveStore -DestinationPrefix '0.0.0.0/0','::/0' | Where-Object { $up -contains $_.InterfaceIndex -and $_.State -ne 'Dead' } | Select-Object -First 100 -Property NextHop,InterfaceAlias,RouteMetric | ConvertTo-Json -Compress"])],
        _ => [],
    };
    private static string LinuxIp() => new[] { "/usr/sbin/ip", "/sbin/ip", "/usr/bin/ip", "/bin/ip" }.FirstOrDefault(File.Exists) ?? "/usr/sbin/ip";
    public static IReadOnlyList<GatewayCandidate> Parse(RoutePlatform platform, string text)
    {
        var rows = new List<GatewayCandidate>();
        void Add(string? address, string? name, int? metric = null)
        {
            if (!IPAddress.TryParse(address, out var ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(ip)) return;
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal && ip.ScopeId == 0)
            {
                var adapter = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == name);
                if (adapter?.GetIPProperties().GetIPv6Properties() is not { } properties) return;
                ip.ScopeId = properties.Index;
            }
            rows.Add(new(ip.ToString(), string.IsNullOrWhiteSpace(name) ? "Interface not reported" : name, metric));
        }
        if (platform == RoutePlatform.MacOS)
        {
            string? gateway = null, iface = null;
            foreach (var line in text.Split('\n'))
            {
                var pair = line.Trim().Split(':', 2);
                if (pair.Length == 2 && pair[0] == "gateway") gateway = pair[1].Trim();
                if (pair.Length == 2 && pair[0] == "interface") iface = pair[1].Trim();
            }
            Add(gateway, iface);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            using var document = JsonDocument.Parse(text);
            var records = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().ToArray() : [document.RootElement];
            foreach (var row in records.Take(100))
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                var linux = platform == RoutePlatform.Linux;
                if (linux && row.TryGetProperty("flags", out var flags) && flags.EnumerateArray().Any(f => f.GetString() == "linkdown")) continue;
                var key = linux ? "gateway" : "NextHop"; var iface = linux ? "dev" : "InterfaceAlias"; var metric = linux ? "metric" : "RouteMetric";
                Add(row.TryGetProperty(key, out var g) ? g.GetString() : null, row.TryGetProperty(iface, out var i) ? i.GetString() : null,
                    row.TryGetProperty(metric, out var m) && m.TryGetInt32(out var value) ? value : null);
            }
        }
        return rows;
    }
}

public sealed class RouteCommandRunner : IRouteCommandRunner
{
    public async Task<RouteOutput> RunAsync(RouteCommand command, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(command.FileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        async Task<string> ReadAsync(StreamReader reader)
        {
            var output = new StringBuilder(); var buffer = new char[1024];
            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false);
                if (count == 0) return output.ToString();
                if (output.Length + count > 65536) throw new IOException("Native routing output exceeded 64 KiB; discovery stopped.");
                output.Append(buffer, 0, count);
            }
        }
        var stdout = ReadAsync(process.StandardOutput); var stderr = ReadAsync(process.StandardError);
        try
        {
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellation)).ConfigureAwait(false);
            return new(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(false); }
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        }
    }
}
