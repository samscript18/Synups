using System.Collections.ObjectModel;
using System.Net;
using Synups.Core.Network;
using Synups.Core.Monitoring;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? discoveryCancellation;
    private Task discoveryTask = Task.CompletedTask;
    private bool isDiscovering;
    private string discoveryMessage = "Discovery reads default routes only when requested. It never scans or probes. Select a result and confirm before adding it.";
    private GatewayCandidate? selectedGateway;
    public ObservableCollection<GatewayCandidate> GatewayCandidates { get; } = new();
    private DiscoveryStatus? gatewayStatus;
    public DiscoveryStatus? GatewayStatus { get => gatewayStatus; private set => Set(ref gatewayStatus, value); }
    public bool IsDiscovering { get => isDiscovering; private set { if (Set(ref isDiscovering, value)) NotifyControls(); } }
    public bool CanDiscover => CanEdit && !recoveryRequired;
    public bool CanAcceptGateway => CanDiscover && SelectedGateway is not null;
    public string DiscoveryMessage { get => discoveryMessage; private set => Set(ref discoveryMessage, value); }
    public GatewayCandidate? SelectedGateway { get => selectedGateway; set { if (Set(ref selectedGateway, value)) Changed(nameof(CanAcceptGateway)); } }
    public Task DiscoverGatewaysAsync(GatewayDiscovery? discovery = null)
    {
        if (!CanDiscover) return Task.CompletedTask;
        discoveryCancellation?.Dispose(); discoveryCancellation = new();
        IsDiscovering = true; GatewayCandidates.Clear(); SelectedGateway = null; GatewayStatus = null;
        DiscoveryMessage = "Reading native default routes… Cancel is safe; existing targets are unchanged.";
        discoveryTask = ReadAsync(); return discoveryTask;
        async Task ReadAsync()
        {
            try
            {
                var result = await (discovery ?? new GatewayDiscovery()).DiscoverAsync(discoveryCancellation.Token);
                GatewayStatus = result.Status;
                foreach (var candidate in result.Candidates) GatewayCandidates.Add(candidate);
                SelectedGateway = GatewayCandidates.FirstOrDefault(); DiscoveryMessage = result.Detail;
            }
            catch (Exception ex) { DiscoveryMessage = $"Discovery unavailable: {ex.Message}. Configure a manual IP instead."; }
            finally { IsDiscovering = false; }
        }
    }
    public void CancelDiscovery() => discoveryCancellation?.Cancel();
    public Task AcceptGatewayAsync(bool confirmed) => ExecuteAsync(async () =>
    {
        EnsureEditing();
        if (!confirmed) throw new InvalidOperationException("Confirm the discovered address before creating a target.");
        if (IsDiscovering || recoveryRequired || SelectedGateway is not { } candidate) throw new InvalidOperationException("Discover and select an address first, with a valid stopped profile.");
        var address = IPAddress.Parse(candidate.Address);
        if (configuration.Groups.SelectMany(g => g.Targets).Any(t => IPAddress.Parse(t.Address).Equals(address)))
            throw new InvalidOperationException("This address already exists in your profile. No duplicate was created.");
        var target = new TargetConfiguration(Guid.NewGuid(), "Gateway · " + candidate.InterfaceName, address.ToString());
        var group = SelectedGroup?.Configuration;
        if (group is null) group = new(Guid.NewGuid(), "Default gateway", true, 5000, 10000, 1000, [target]);
        else group = group with { Targets = group.Targets.Append(target).ToArray() };
        var groups = configuration.Groups.Any(g => g.Id == group.Id)
            ? configuration.Groups.Select(g => g.Id == group.Id ? group : g).ToArray() : configuration.Groups.Append(group).ToArray();
        await SaveConfigurationAsync(new(1, groups), group.Id, target.Id);
        DiscoveryMessage = "Gateway target saved with existing IDs preserved. Monitoring was not started. A reachable gateway does not establish Internet availability.";
    });
}
