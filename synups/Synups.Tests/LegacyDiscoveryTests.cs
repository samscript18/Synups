using System.Net;
using Synups.Core.Monitoring;
using Synups.Core.Network;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class LegacyDiscoveryTests
{
    [Fact]
    public void CandidateListsAndTimeoutsAreExactlyFromOriginalResource()
    {
        var rules=LegacyCandidateDiscovery.OriginalRules();Assert.Equal(2,rules.Count);
        Assert.Equal(new[]{"192.168.100.1","10.0.0.1","10.1.0.1"},rules[0].Addresses.Select(a=>a.ToString()));
        Assert.Equal("192.168.1.254",Assert.Single(rules[1].Addresses).ToString());Assert.All(rules,r=>Assert.Equal(50,r.TimeoutMilliseconds));
    }
    [Fact]
    public async Task CandidateChecksAreSequentialStopOnSuccessAndDoNotInferIdentity()
    {
        var rule=LegacyCandidateDiscovery.OriginalRules()[0];var probe=new ScriptedProbe(Fail,Ok,Error);
        var result=await new LegacyCandidateDiscovery(probe).CheckAsync(rule,TestContext.Current.CancellationToken);
        Assert.Equal(2,result.Attempts.Count);Assert.Equal("10.0.0.1",result.Responding!.Address);Assert.Contains("identity is unknown",result.Summary);
        Assert.Equal(new[]{"192.168.100.1","10.0.0.1"},probe.Addresses);
        var denied=await new LegacyCandidateDiscovery(new ScriptedProbe(Error,Fail,Fail)).CheckAsync(rule,TestContext.Current.CancellationToken);
        Assert.Null(denied.Responding);Assert.Contains("unavailable",denied.Summary);
        await Assert.ThrowsAsync<ArgumentException>(()=>new LegacyCandidateDiscovery(probe).CheckAsync(rule with{TimeoutMilliseconds=0},TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task CandidateCancellationOwnsAndDrainsTheProbe()
    {
        var probe=new WaitingProbe();using var cancel=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var check=new LegacyCandidateDiscovery(probe).CheckAsync(LegacyCandidateDiscovery.OriginalRules()[0],cancel.Token);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);cancel.Cancel();var result=await check;
        Assert.True(result.Cancelled);Assert.True(probe.Drained);Assert.Empty(result.Attempts);
    }
    [Fact]
    public async Task GuidedDefaultsAndConfirmedCandidatePreserveIdsAndPersistWithoutMonitoring()
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-guided-defaults-"+Guid.NewGuid());var path=Path.Combine(root,"config.json");var original=Group() with{Name="Internet"};
        await new ConfigurationStore(path).SaveAsync(Config(original));var vm=new MainWindowViewModel(new MonitoringService(new ScriptedProbe()),new ConfigurationStore(path));
        try
        {
            await vm.InitializeAsync();await vm.AddMissingDefaultsAsync(true);Assert.Equal(4,vm.Groups.Count);
            var internet=vm.Groups.Single(g=>g.Name=="Internet");Assert.Equal(original.Id,internet.Id);Assert.Equal(original.Targets[0].Id,internet.Targets[0].Id);
            var modem=vm.Groups.Single(g=>g.Name=="Local Modem");var modemId=modem.Id;Assert.False(modem.Configuration.Enabled);
            vm.SelectedLegacyRule=vm.LegacyRules[0];var probe=new ScriptedProbe(Ok);
            await vm.CheckLegacyCandidatesAsync(false,new(probe));Assert.Empty(probe.Addresses);Assert.False(vm.CanAcceptLegacy);
            await vm.CheckLegacyCandidatesAsync(true,new(probe));Assert.True(vm.CanAcceptLegacy);Assert.Empty(modem.Targets);Assert.False(vm.IsRunning);
            await vm.AcceptLegacyCandidateAsync(false);Assert.Empty(vm.Groups.Single(g=>g.Name=="Local Modem").Targets);
            await vm.AcceptLegacyCandidateAsync(true);modem=vm.Groups.Single(g=>g.Name=="Local Modem");Assert.Equal(modemId,modem.Id);Assert.True(modem.Configuration.Enabled);
            Assert.Equal("192.168.100.1",Assert.Single(modem.Targets).Address);Assert.Equal(50,modem.Configuration.TimeoutMilliseconds);
            var saved=(await new ConfigurationStore(path).LoadAsync()).Configuration!;Assert.Equal(modemId,saved.Groups.Single(g=>g.Name=="Local Modem").Id);
            vm.SelectedGateway=new("192.0.2.1","Fixture interface",null);await vm.AddGatewayToOriginalRouterAsync(true);
            Assert.True(vm.Groups.Single(g=>g.Name=="Local Router").Configuration.Enabled);Assert.Equal("Not observed",vm.LastMeasurement);Assert.False(vm.IsRunning);
            var before=await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken);await vm.AddGatewayToOriginalRouterAsync(true);
            Assert.Equal(before,await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken));Assert.Contains("already exists",vm.Message);
        }
        finally{await vm.DisposeAsync();Directory.Delete(root,true);}
    }
    [Fact]
    public async Task RealLoopbackExplicitCandidateCheckUsesProductionProbe()
    {
        if(Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS")!="1")Assert.Skip("Explicit real loopback candidate check only.");
        var result=await new LegacyCandidateDiscovery().CheckAsync(new("Explicit loopback verification","This computer","Verification",1000,[IPAddress.Loopback]),TestContext.Current.CancellationToken);
        Assert.Equal("127.0.0.1",result.Responding!.Address);Assert.False(result.Cancelled);Assert.NotNull(result.Responding.Result.LatencyMilliseconds);
    }
    private sealed class WaitingProbe:IConnectivityProbe
    {
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);public bool Drained{get;private set;}
        public async Task<ProbeResult> ProbeAsync(IPAddress address,TimeSpan timeout,CancellationToken token)
        {Started.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);throw new InvalidOperationException();}finally{Drained=true;}}
    }
}
