using System.Net;
using System.Xml;
using System.Xml.Linq;
using Synups.Core.Monitoring;

namespace Synups.Core.Network;

public sealed record LegacyDiscoveryRule(string GroupName, string Name, string Kind, int TimeoutMilliseconds, IReadOnlyList<IPAddress> Addresses)
{
    public override string ToString() => $"{Name} · {GroupName} · {string.Join(", ", Addresses)}";
}
public sealed record CandidateAttempt(string Address, ProbeResult Result);
public sealed record CandidateCheck(IReadOnlyList<CandidateAttempt> Attempts, bool Cancelled)
{
    public CandidateAttempt? Responding => Attempts.FirstOrDefault(a => a.Result.Outcome == ProbeOutcome.Success);
    public string Summary => Cancelled ? "Cancelled. No target was created." : Responding is { } found
        ? $"{found.Address} responded to ICMP. Device identity is unknown. Confirm before adding; no monitoring was started."
        : Attempts.Any(a => a.Result.Outcome == ProbeOutcome.Unavailable) ? "No responding candidate; some probes were unavailable. Check permissions and routing, or configure a manual IP."
        : "No candidate responded within its timeout. This does not prove a modem/transceiver is absent; configure a manual IP if known.";
}

/// <summary>Explicit finite checks of original XML candidates, sequential and stop-on-success. Never scans a range.</summary>
public sealed class LegacyCandidateDiscovery(IConnectivityProbe? probe = null)
{
    private readonly IConnectivityProbe probe = probe ?? new IcmpProbe();
    public static IReadOnlyList<LegacyDiscoveryRule> OriginalRules()
    {
        using var stream = typeof(DefaultProfileReader).Assembly.GetManifestResourceStream("Synups.DefaultProfile.xml")
            ?? throw new InvalidOperationException("Original profile resource missing.");
        using var reader = XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1_000_000 });
        var root = XElement.Load(reader);
        return root.Elements("PeerGroup").SelectMany(g=>g.Elements("Find")
            .Where(f=>(string?)f.Attribute("Type") is "CableModem" or "Transceiver")
            .Select(f=>new LegacyDiscoveryRule((string)g.Attribute("Name")!, (string)f.Attribute("Name")!, (string)f.Attribute("Type")!,
                (int?)g.Attribute("WaitMilliseconds") ?? 200, f.Elements("Address").Select(a=>IPAddress.Parse(a.Value.Trim())).ToArray()))).ToArray();
    }
    public async Task<CandidateCheck> CheckAsync(LegacyDiscoveryRule rule, CancellationToken cancellationToken)
    {
        if (rule.Addresses.Count is < 1 or > 8 || rule.TimeoutMilliseconds is < 1 or > 5000)
            throw new ArgumentException("Candidate checks require 1–8 explicit addresses and a 1–5000ms finite timeout.");
        var attempts = new List<CandidateAttempt>();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            foreach(var address in rule.Addresses)
            {
                deadline.Token.ThrowIfCancellationRequested();ProbeResult result;
                try { result=await probe.ProbeAsync(address,TimeSpan.FromMilliseconds(rule.TimeoutMilliseconds),deadline.Token).ConfigureAwait(false); }
                catch(OperationCanceledException) when(deadline.IsCancellationRequested) { throw; }
                catch(Exception ex) { result=IcmpProbe.MapException(ex); }
                deadline.Token.ThrowIfCancellationRequested();
                attempts.Add(new(address.ToString(),result));
                if(result.Outcome==ProbeOutcome.Success) break;
            }
            return new(attempts.AsReadOnly(),false);
        }
        catch(OperationCanceledException) when(deadline.IsCancellationRequested) { return new(attempts.AsReadOnly(),true); }
    }
}
