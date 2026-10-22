using Synups.Core.Network;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Synups.Tests;

public class GatewayTests
{
    [Theory]
    [InlineData(RoutePlatform.MacOS, "   route to: default\n gateway: 192.0.2.1\n interface: en0\n", "192.0.2.1", "en0")]
    [InlineData(RoutePlatform.Linux, "[{\"dst\":\"default\",\"gateway\":\"192.0.2.1\",\"dev\":\"eth0\",\"metric\":100}]", "192.0.2.1", "eth0")]
    [InlineData(RoutePlatform.Windows, "{\"NextHop\":\"192.0.2.1\",\"InterfaceAlias\":\"Ethernet\",\"RouteMetric\":25}", "192.0.2.1", "Ethernet")]
    public void ParsesNativePlatformFormats(RoutePlatform platform, string text, string address, string iface)
    {
        var result = Assert.Single(GatewayDiscovery.Parse(platform, text));
        Assert.Equal(address, result.Address); Assert.Equal(iface, result.InterfaceName);
    }

    [Fact]
    public void MultipleVpnRoutesAndMissingGatewaysRemainExplicit()
    {
        var candidates = GatewayDiscovery.Parse(RoutePlatform.Linux, """
            [{"gateway":"192.0.2.1","dev":"eth0","metric":100},
             {"gateway":"198.51.100.1","dev":"tun0","metric":10},
             {"dev":"tun1"},{"gateway":"0.0.0.0","dev":"eth1"},
             {"gateway":"203.0.113.1","dev":"down0","flags":["linkdown"]}]
            """);
        Assert.Equal(2, candidates.Count); Assert.Equal("tun0", candidates[1].InterfaceName);
        Assert.Empty(GatewayDiscovery.Parse(RoutePlatform.MacOS, "gateway: link#20\ninterface: utun0"));
        Assert.Empty(GatewayDiscovery.Parse(RoutePlatform.Windows, ""));
    }

    [Fact]
    public async Task TimeoutCancellationPermissionsAndMalformedOutputAreRecoverable()
    {
        var timeout = await new GatewayDiscovery(new WaitingRunner(), RoutePlatform.Linux, TimeSpan.FromMilliseconds(20)).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DiscoveryStatus.TimedOut, timeout.Status);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(DiscoveryStatus.Cancelled, (await new GatewayDiscovery(new WaitingRunner(), RoutePlatform.Linux).DiscoverAsync(cancel.Token)).Status);
        Assert.Equal(DiscoveryStatus.Unavailable, (await new GatewayDiscovery(new StubRunner(new(1,"","Permission denied")), RoutePlatform.Windows).DiscoverAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(DiscoveryStatus.Unavailable, (await new GatewayDiscovery(new StubRunner(new(0,"{broken","")), RoutePlatform.Windows).DiscoverAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(DiscoveryStatus.Unsupported, (await new GatewayDiscovery(platform: RoutePlatform.Unsupported).DiscoverAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(DiscoveryStatus.NoGateway, (await new GatewayDiscovery(new StubRunner(new(0,"[]","")), RoutePlatform.Linux).DiscoverAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task NativeRunnerPreCancellationDoesNotLaunchAProcess()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RouteCommandRunner().RunAsync(new("does-not-exist", []), cancel.Token));
    }

    [Fact]
    public async Task NativeRunnerCancelsAndDrainsItsOwnedProcess()
    {
        var command = OperatingSystem.IsWindows()
            ? new RouteCommand(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"])
            : new RouteCommand("/bin/sleep", ["30"]);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(100));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RouteCommandRunner().RunAsync(command, cancel.Token));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Owned native process did not drain promptly after cancellation.");
    }

    [AvaloniaFact]
    public async Task PreviewRequiresConfirmationPreservesIdsAndRefusesDuplicateAddresses()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-gateway-"+Guid.NewGuid()); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "configuration.json"); var group = TestConfig.Group(); var config = TestConfig.Config(group);
        var store = new ConfigurationStore(path); await store.SaveAsync(config);
        var probe = new ScriptedProbe(); var vm = new MainWindowViewModel(new MonitoringService(probe), store);
        try
        {
            await vm.InitializeAsync();
            await vm.DiscoverGatewaysAsync(new(new StubRunner(new(0,"{\"NextHop\":\"192.0.2.1\",\"InterfaceAlias\":\"Ethernet\"}","")), RoutePlatform.Windows));
            Assert.Single(vm.GatewayCandidates); Assert.Single(vm.Groups[0].Targets); Assert.Empty(probe.Addresses);
            await vm.AcceptGatewayAsync(false); Assert.Contains("Confirm", vm.Message); Assert.Single(vm.Groups[0].Targets);
            await vm.AcceptGatewayAsync(true);
            var saved = (await store.LoadAsync()).Configuration!;
            Assert.Equal(group.Id, saved.Groups[0].Id); Assert.Equal(group.Targets[0].Id, saved.Groups[0].Targets[0].Id);
            Assert.Equal(2, saved.Groups[0].Targets.Count); Assert.False(vm.IsRunning); Assert.Empty(probe.Addresses);
            await vm.AcceptGatewayAsync(true); Assert.Contains("already exists", vm.Message); Assert.Equal(2, vm.Groups[0].Targets.Count);
        }
        finally { await vm.DisposeAsync(); Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task CloseCancelsAndOwnsDiscoveryWithoutWritingConfiguration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-discovery-cancel-"+Guid.NewGuid());
        var path = Path.Combine(directory, "configuration.json");
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe()), new ConfigurationStore(path));
        await vm.InitializeAsync();
        var pending = vm.DiscoverGatewaysAsync(new(new WaitingRunner(), RoutePlatform.Linux));
        Assert.True(vm.IsDiscovering); Assert.False(vm.CanStart); Assert.False(vm.CanDiscover);
        await vm.DisposeAsync(); await pending; Assert.False(vm.IsDiscovering); Assert.False(File.Exists(path));
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Fact]
    public async Task RealDefaultGatewayReadOnlyNativeRouteQuery()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS") != "1") Assert.Skip("Opt-in read-only OS route query; no network probe.");
        var result = await new GatewayDiscovery().DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.True(result.Status is DiscoveryStatus.Found or DiscoveryStatus.NoGateway, result.Detail);
        foreach (var c in result.Candidates) { Assert.True(System.Net.IPAddress.TryParse(c.Address, out _)); Assert.False(string.IsNullOrWhiteSpace(c.InterfaceName)); }
        Console.WriteLine($"REAL_DEFAULT_GATEWAY={GatewayDiscovery.CurrentPlatform}; {result.Status}; candidates={result.Candidates.Count}; no probe or configuration write");
    }

    private sealed class StubRunner(RouteOutput output) : IRouteCommandRunner
    { public Task<RouteOutput> RunAsync(RouteCommand command, CancellationToken cancellation) => Task.FromResult(output); }
    private sealed class WaitingRunner : IRouteCommandRunner
    { public async Task<RouteOutput> RunAsync(RouteCommand command, CancellationToken cancellation) { await Task.Delay(Timeout.Infinite, cancellation); return new(0,"",""); } }
}
