namespace Synups.Core.Diagnostics;

/// <summary>Interpretations of retained ICMP control-plane responses, never assertions of forwarding loss.</summary>
public static class PathAnalysis
{
    public static IReadOnlyList<DiagnosticObservation> AnalyzeEvidence(IReadOnlyList<DiagnosticTestResult> evidence)
    {
        var result = new List<DiagnosticObservation>();
        var traces = evidence.Where(e => e.Kind == DiagnosticKind.TraceRoute && e.Hops is not null).OrderBy(e => e.StartedUtc).ToArray();
        var utc = evidence.Count > 0 ? evidence.Max(e => e.CompletedUtc) : DateTimeOffset.UtcNow;
        foreach (var trace in traces) result.AddRange(Analyze(trace.Hops!, trace.CompletedUtc));
        var quality = evidence.Where(e => e.Kind == DiagnosticKind.PingQuality && e.Statistics is not null).ToArray();
        foreach (var sample in quality.Where(e => e.Statistics is { Completed: >= 2, Unanswered: > 0 }))
            result.Add(new(sample.CompletedUtc, "Possible end-to-end echo loss", DiagnosticCertainty.Interpretation, Text:
                $"{sample.Statistics!.Unanswered}/{sample.Statistics.Completed} completed destination echo probes were unanswered. End-to-end echo loss is possible; ICMP filtering or suppression remains an alternative explanation."));
        if (quality.Any(e => e.Statistics is { Completed: >= 2, Unanswered: > 0 } &&
            evidence.Any(tcp => tcp.Kind == DiagnosticKind.TcpConnect && tcp.Outcome == DiagnosticOutcome.Success &&
                string.Equals(e.Target, tcp.Target, StringComparison.OrdinalIgnoreCase) && Math.Abs((e.StartedUtc - tcp.StartedUtc).TotalMinutes) <= 5)))
            result.Add(new(utc, "Protocol comparison", DiagnosticCertainty.Interpretation, Text:
                "Destination echo probes were unanswered while a TCP connection succeeded within the supplied evidence window. This is consistent with protocol-specific filtering or suppression; tests occurred at different instants and do not establish rate limiting."));
        for (var i = 1; i < traces.Length; i++)
        {
            var previous = traces[i - 1]; var current = traces[i];
            if (!string.Equals(previous.Target, current.Target, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var hop in current.Hops!)
            {
                var old = previous.Hops!.FirstOrDefault(h => h.Hop == hop.Hop);
                if (old is null) continue;
                var oldTotal = old.RttMilliseconds.Count + old.Unanswered; var total = hop.RttMilliseconds.Count + hop.Unanswered;
                if (oldTotal < 2 || total < 2 || (double)old.Unanswered / oldTotal == (double)hop.Unanswered / total) continue;
                result.Add(new(current.CompletedUtc, "Changed ICMP response fraction", DiagnosticCertainty.Observed, Text:
                    $"Hop {hop.Hop}: unanswered {old.Unanswered}/{oldTotal} at {previous.StartedUtc:O}, then {hop.Unanswered}/{total} at {current.StartedUtc:O}. Different time windows, responders or load may affect the samples."));
            }
        }
        if (!result.Any(o => o.Certainty == DiagnosticCertainty.Interpretation))
            result.Add(new(utc, "Rate-limit evidence", DiagnosticCertainty.Unknown, Text:
                "Insufficient evidence to infer ICMP rate limiting. Multiple samples, responding later hops, destination results, and comparable time windows are needed; none can guarantee the cause."));
        return result;
    }

    public static IReadOnlyList<DiagnosticObservation> Analyze(IReadOnlyList<PathHop> hops, DateTimeOffset utc)
    {
        var result = new List<DiagnosticObservation>
        {
            new(utc, "Path visibility", DiagnosticCertainty.Unknown, Text:
                "Intermediate nonresponse does not establish forwarding loss. ICMP filtering, suppression, rate limiting, ECMP, and asymmetric return paths can affect these observations.")
        };
        foreach (var hop in hops.Where(h => h.Unanswered > 0 && hops.Any(later => later.Hop > h.Hop && later.Addresses.Count > 0)))
            result.Add(new(utc, "Possible ICMP response suppression", DiagnosticCertainty.Interpretation, Text:
                $"Hop {hop.Hop} has {hop.Unanswered} unanswered probes while a later hop responded. This is consistent with ICMP response suppression or rate limiting, but the cause is unconfirmed; forwarding loss is not established."));
        var repeats = hops.SelectMany(h => h.Addresses.Select(a => (h.Hop, Address: a))).GroupBy(x => x.Address)
            .Where(g => g.Select(x => x.Hop).Distinct().Count() > 1);
        foreach (var repeat in repeats)
            result.Add(new(utc, "Repeated responder", DiagnosticCertainty.Interpretation, Text:
                $"{repeat.Key} responded at multiple hop limits ({string.Join(", ", repeat.Select(x => x.Hop).Distinct())}). A loop is possible; repeated responses alone do not prove a routing loop."));
        return result;
    }

    public static IReadOnlyList<DiagnosticObservation> Compare(IReadOnlyList<PathHop> baseline, IReadOnlyList<PathHop> current, DateTimeOffset utc)
    {
        var observations = new List<DiagnosticObservation>();
        foreach (var hop in current)
        {
            var previous = baseline.FirstOrDefault(h => h.Hop == hop.Hop);
            if (previous is null || previous.Addresses.Count == 0 || hop.Addresses.Count == 0) continue;
            if (!previous.Addresses.ToHashSet(StringComparer.Ordinal).SetEquals(hop.Addresses))
                observations.Add(new(utc, "Changed observed hop responders", DiagnosticCertainty.Observed, Text:
                    $"Hop {hop.Hop}: [{string.Join(", ", previous.Addresses)}] -> [{string.Join(", ", hop.Addresses)}]."));
        }
        if (observations.Count > 0)
            observations.Add(new(utc, "Path comparison limits", DiagnosticCertainty.Unknown, Text:
                "Different ICMP responder addresses can reflect load balancing or changed visibility; they do not by themselves prove a changed forwarding route. Missing responses are not compared as route changes."));
        return observations;
    }
}
