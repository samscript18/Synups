using System.Collections.ObjectModel;
using System.Net;
using Synups.Core;
using Synups.Core.Monitoring;
using Synups.Core.Network;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<LegacyDiscoveryRule> LegacyRules { get; } = LegacyCandidateDiscovery.OriginalRules();
    private LegacyDiscoveryRule? selectedLegacyRule;
    private CandidateCheck? legacyCheck;
    public LegacyDiscoveryRule? SelectedLegacyRule
    { get => selectedLegacyRule; set { if(IsDiscovering)return; if (Set(ref selectedLegacyRule,value)) { legacyCheck=null; LegacyAttempts.Clear(); Changed(nameof(CanAcceptLegacy)); Changed(nameof(CanCheckLegacy)); } } }
    public ObservableCollection<string> LegacyAttempts { get; } = new();
    private string candidateMessage="Choose an original candidate list. Check sends only the displayed ICMP requests after confirmation; no range scan or device identity inference.";
    public string CandidateMessage { get=>candidateMessage; private set=>Set(ref candidateMessage,value); }
    public bool CanCheckLegacy => CanDiscover && SelectedLegacyRule is not null;
    public bool CanAcceptLegacy => CanCheckLegacy && legacyCheck?.Responding is not null;
    public Task CheckLegacyCandidatesAsync(bool confirmed, LegacyCandidateDiscovery? checker=null)
    {
        if(!confirmed || !CanCheckLegacy || SelectedLegacyRule is not { } rule) return Task.CompletedTask;
        discoveryCancellation?.Dispose();discoveryCancellation=new();IsDiscovering=true;legacyCheck=null;LegacyAttempts.Clear();
        CandidateMessage="Checking only the confirmed candidates…";Changed(nameof(CanAcceptLegacy));
        discoveryTask=CheckAsync();return discoveryTask;
        async Task CheckAsync()
        {
            try
            {
                legacyCheck=await (checker??new LegacyCandidateDiscovery()).CheckAsync(rule,discoveryCancellation.Token);
                foreach(var attempt in legacyCheck.Attempts)
                    LegacyAttempts.Add($"{attempt.Address} · {attempt.Result.Outcome} · {attempt.Result.Detail ?? attempt.Result.Error.ToString()}"+
                        (attempt.Result.LatencyMilliseconds is { } latency?$" · {latency}ms":""));
                CandidateMessage=legacyCheck.Summary;
            }
            catch(Exception ex){CandidateMessage=$"Candidate check unavailable: {ex.Message}. Existing configuration is unchanged.";}
            finally{IsDiscovering=false;Changed(nameof(CanAcceptLegacy));}
        }
    }
    public Task AcceptLegacyCandidateAsync(bool confirmed)=>ExecuteAsync(async()=>
    {
        EnsureEditing();
        if(!confirmed || SelectedLegacyRule is not { } rule || legacyCheck is not { Cancelled:false,Responding:{ } response })
            throw new InvalidOperationException("Confirm a responding candidate before adding it.");
        await AddToOriginalGroupAsync(rule.GroupName,rule.Name+" candidate",response.Address);
        CandidateMessage="Confirmed reachable address saved; original group enabled. Device identity remains unknown. Monitoring was not started.";
        legacyCheck=null;Changed(nameof(CanAcceptLegacy));
    });
    public Task AddGatewayToOriginalRouterAsync(bool confirmed)=>ExecuteAsync(async()=>
    {
        EnsureEditing();
        if(!confirmed || SelectedGateway is not { } gateway) throw new InvalidOperationException("Confirm a selected gateway first.");
        await AddToOriginalGroupAsync("Local Router","Default Gateway",gateway.Address);
        DiscoveryMessage="Confirmed gateway added to Local Router and that group enabled. Other groups/IDs unchanged; no monitoring started.";
    });
    private async Task AddToOriginalGroupAsync(string groupName,string targetName,string address)
    {
        if(recoveryRequired || IsDiscovering) throw new InvalidOperationException("Use a valid stopped profile and finish discovery first.");
        var parsed=IPAddress.Parse(address);
        if(configuration.Groups.SelectMany(g=>g.Targets).Any(t=>IPAddress.Parse(t.Address).Equals(parsed)))
            throw new InvalidOperationException("This address already exists. No duplicate or configuration change was made.");
        var matches=configuration.Groups.Where(g=>g.Name==groupName).ToArray();
        if(matches.Length>1) throw new InvalidOperationException("Multiple groups have this name. Choose manual group membership instead.");
        var original=DefaultProfileReader.LoadBundled().Single(g=>g.Name==groupName);
        var group=matches.SingleOrDefault()??new GroupConfiguration(Guid.NewGuid(),groupName,false,(int)original.PollInterval.TotalMilliseconds,
            (int)original.FailAfter.TotalMilliseconds,(int)original.Timeout.TotalMilliseconds,[]);
        var target=new TargetConfiguration(Guid.NewGuid(),targetName,parsed.ToString());
        group=group with{Enabled=true,Targets=group.Targets.Append(target).ToArray()};
        await SaveConfigurationAsync(new(1,configuration.Groups.Any(g=>g.Id==group.Id)?configuration.Groups.Select(g=>g.Id==group.Id?group:g).ToArray():configuration.Groups.Append(group).ToArray()),group.Id,target.Id);
    }
    public Task AddMissingDefaultsAsync(bool confirmed)=>ExecuteAsync(async()=>
    {
        EnsureEditing();if(!confirmed)throw new InvalidOperationException("Confirm adding original groups first.");
        var missing=MonitorConfiguration.OriginalDefaults().Groups.Where(g=>!configuration.Groups.Any(existing=>existing.Name==g.Name)).ToArray();
        if(missing.Length==0){Message="All original group names already exist; no targets, IDs or timing were replaced.";return;}
        await SaveConfigurationAsync(new(1,configuration.Groups.Concat(missing).ToArray()));
        Message="Missing original groups added without changing existing groups. Discovery-only groups stay disabled until a confirmed address is added. No probes or monitoring started.";
    });
}
