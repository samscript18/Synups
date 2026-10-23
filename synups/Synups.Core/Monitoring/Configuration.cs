using System.Net;

namespace Synups.Core.Monitoring;

public sealed record TargetConfiguration(Guid Id, string Name, string Address);
public sealed record GroupConfiguration(Guid Id, string Name, bool Enabled, int PollMilliseconds,
    int FailMilliseconds, int TimeoutMilliseconds, IReadOnlyList<TargetConfiguration> Targets);
public sealed record MonitorConfiguration(int Version, IReadOnlyList<GroupConfiguration> Groups)
{
    public const int CurrentVersion = 1;
    public static MonitorConfiguration Empty => new(CurrentVersion, Array.Empty<GroupConfiguration>());

    public MonitorConfiguration ValidatedCopy()
    {
        if (Version != CurrentVersion) throw new ArgumentException($"Unsupported configuration version {Version}.");
        if (Groups is null || Groups.Count > 64) throw new ArgumentException("Configuration supports at most 64 groups.");
        var ids = new HashSet<Guid>();
        var targetCount = 0;
        foreach (var group in Groups)
        {
            if (group is null || group.Id == Guid.Empty || !ids.Add(group.Id)) throw new ArgumentException("Group IDs must be present and unique.");
            ValidateName(group.Name);
            if (group.PollMilliseconds is < 250 or > 3_600_000) throw new ArgumentException("Poll interval must be 0.25–3600 seconds.");
            if (group.FailMilliseconds is < 1 or > 36_000_000) throw new ArgumentException("Fail interval must be greater than zero and at most 36000 seconds.");
            if (group.TimeoutMilliseconds is < 1 or > 5_000) throw new ArgumentException("Probe timeout must be 1–5000 milliseconds.");
            if (group.Targets is null || group.Targets.Count > 128 || (group.Enabled && group.Targets.Count == 0))
                throw new ArgumentException("Each enabled group needs 1–128 targets. Disable an empty group before saving it.");
            foreach (var target in group.Targets)
            {
                if (target is null || target.Id == Guid.Empty || !ids.Add(target.Id)) throw new ArgumentException("Target IDs must be present and unique across the profile.");
                ValidateName(target.Name);
                if (target.Address is null || !IPAddress.TryParse(target.Address, out var address) ||
                    address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast) ||
                    address.IsIPv6Multicast || (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && address.GetAddressBytes()[0] >= 224))
                    throw new ArgumentException("Enter a literal unicast IPv4 or IPv6 address (hostnames are not supported).");
                targetCount++;
            }
        }
        if (targetCount > 512) throw new ArgumentException("Configuration supports at most 512 targets.");
        return new(Version, Array.AsReadOnly(Groups.Select(g => g with
        {
            Targets = Array.AsReadOnly(g.Targets.Select(t => t with { Address = IPAddress.Parse(t.Address).ToString() }).ToArray()),
        }).ToArray()));
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl))
            throw new ArgumentException("Names must be 1–120 characters without control characters.");
    }

    public static MonitorConfiguration OriginalDefaults() => new MonitorConfiguration(CurrentVersion,
        DefaultProfileReader.LoadBundled().Select(g => new GroupConfiguration(Guid.NewGuid(), g.Name,
            g.Targets.Count > 0, (int)g.PollInterval.TotalMilliseconds, (int)g.FailAfter.TotalMilliseconds,
            (int)g.Timeout.TotalMilliseconds, g.Targets.Select(t => new TargetConfiguration(Guid.NewGuid(), t.Name, t.Address.ToString())).ToArray())).ToArray()).ValidatedCopy();
}
