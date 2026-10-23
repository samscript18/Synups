using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Synups.Core.Monitoring;

namespace Synups.Core.Diagnostics;

public sealed record EvidenceDocument(int Version, DateTimeOffset CreatedUtc, bool Redacted, string RedactionDeclaration,
    DiagnosticSession Session, OutageRecord? Incident, DiagnosticComparison? Comparison, IReadOnlyList<string> Limitations);
public sealed record EvidenceExportResult(string Directory, IReadOnlyList<string> Files, string ManifestPath);

public static class DiagnosticEvidence
{
    public static DiagnosticComparison Compare(DiagnosticSession baseline, DiagnosticSession current, Guid baselineId)
    {
        var differences = new List<DiagnosticObservation>(); var utc = DateTimeOffset.UtcNow;
        void Note(string name, DiagnosticCertainty certainty, string text, double? number = null, string? unit = null) => differences.Add(new(utc, name, certainty, number, unit, text));
        if (!string.Equals(baseline.Request.Target, current.Request.Target, StringComparison.OrdinalIgnoreCase))
        { Note("Compatibility", DiagnosticCertainty.Unknown, "Targets differ; measurements cannot be compared."); return new(Guid.NewGuid(), baselineId, current.Request.Id, utc, differences); }
        foreach (var old in baseline.Results)
        {
            var next = current.Results.SingleOrDefault(r => r.Kind == old.Kind);
            if (next is null) { Note(old.Kind.ToString(), DiagnosticCertainty.Unknown, "Current session has no comparable test."); continue; }
            var a = baseline.Request.Parameters; var b = current.Request.Parameters;
            var compatible = old.Kind switch
            {
                DiagnosticKind.PingQuality => a.SampleCount == b.SampleCount && a.IntervalMilliseconds == b.IntervalMilliseconds && a.ProbeTimeoutMilliseconds == b.ProbeTimeoutMilliseconds && old.Statistics?.Partial == false && next.Statistics?.Partial == false,
                DiagnosticKind.TcpConnect => a.Port == b.Port && a.ProbeTimeoutMilliseconds == b.ProbeTimeoutMilliseconds,
                DiagnosticKind.Dns => old.Dns is { } ad && next.Dns is { } bd && ad.Resolver == bd.Resolver && ad.QueryType == bd.QueryType && ad.Transport == bd.Transport && ad.CachePolicy == bd.CachePolicy,
                DiagnosticKind.TraceRoute => a.MaximumHops == b.MaximumHops && a.ProbesPerHop == b.ProbesPerHop && a.ProbeTimeoutMilliseconds == b.ProbeTimeoutMilliseconds,
                _ => false
            };
            if (!compatible || old.Method != next.Method) { Note(old.Kind.ToString(), DiagnosticCertainty.Unknown, "Different parameters, methods, partial windows or unsupported comparison. No equivalent delta calculated."); continue; }
            if (old.Statistics is { } x && next.Statistics is { } y)
            {
                if (x.AverageMilliseconds is null || y.AverageMilliseconds is null) Note("RTT difference", DiagnosticCertainty.Unknown, "A window has no successful RTT; no mean RTT delta is available.");
                if (x.AverageMilliseconds is { } avg && y.AverageMilliseconds is { } nextAvg) Note("RTT difference", DiagnosticCertainty.Observed, "Current minus baseline mean RTT; reporting threshold 10 ms, not a fault diagnosis.", nextAvg - avg, "ms");
                if (x.LossPercent is { } loss && y.LossPercent is { } nextLoss) Note("Observed loss difference", DiagnosticCertainty.Observed, "Current minus baseline; reporting threshold 5 percentage points. ICMP nonresponse is not proven forwarding loss.", nextLoss - loss, "percentage points");
            }
            if (old.Dns is { } dns && next.Dns is { } ndns)
            {
                if (dns.ResponseCode is null || ndns.ResponseCode is null || old.Outcome is DiagnosticOutcome.Timeout or DiagnosticOutcome.MalformedResponse or DiagnosticOutcome.Cancelled || next.Outcome is DiagnosticOutcome.Timeout or DiagnosticOutcome.MalformedResponse or DiagnosticOutcome.Cancelled)
                { Note("DNS comparison", DiagnosticCertainty.Unknown, "A DNS response is missing or invalid; no observed answer change can be established."); continue; }
                var first = dns.Answers.Select(d => $"{d.Type}:{d.Address}").Order().ToArray(); var second = ndns.Answers.Select(d => $"{d.Type}:{d.Address}").Order().ToArray();
                Note("DNS answer comparison", DiagnosticCertainty.Observed, dns.ResponseCode == ndns.ResponseCode && first.SequenceEqual(second) ? "Same returned addresses and response code; TTL can change." : "Returned DNS addresses or response code changed. Rotation, caching and legitimate updates remain possible.");
            }
            if (old.Kind == DiagnosticKind.TcpConnect)
            {
                Note("TCP outcomes", DiagnosticCertainty.Observed, $"Baseline {old.Outcome}; current {next.Outcome}. Connect outcomes do not identify firewall policy or application health.");
                var left = old.Observations.FirstOrDefault(o => o.Name == "TCP handshake")?.Value;
                var right = next.Observations.FirstOrDefault(o => o.Name == "TCP handshake")?.Value;
                if (left is { } l && right is { } r) Note("TCP handshake difference", DiagnosticCertainty.Observed, "Current minus baseline measured handshake time.", r-l, "ms");
                else Note("TCP handshake difference", DiagnosticCertainty.Unknown, "A successful handshake time is missing; no latency delta is available.");
            }
            if (old.Hops is { } oh && next.Hops is { } nh)
            {
                var changed = oh.Select(h => h.Hop).Union(nh.Select(h => h.Hop)).Where(i => !(oh.FirstOrDefault(h => h.Hop == i)?.Addresses.Order().SequenceEqual(nh.FirstOrDefault(h => h.Hop == i)?.Addresses.Order().ToArray() ?? Array.Empty<string>()) ?? false)).ToArray();
                Note("Route observations", DiagnosticCertainty.Observed, changed.Length == 0 ? "Same responding-hop addresses." : $"Different/missing hop observations at: {string.Join(", ", changed)}. Nonresponse and multipath do not prove a route change.");
            }
        }
        differences.AddRange(PathAnalysis.AnalyzeEvidence(baseline.Results.Concat(current.Results).ToArray()));
        return new(Guid.NewGuid(), baselineId, current.Request.Id, utc, differences.Take(100).ToArray());
    }
    public static EvidenceDocument Preview(DiagnosticSession session, OutageRecord? incident = null, bool redact = true, DiagnosticComparison? comparison = null)
    {
        // Round-trip detaches caller collections; every renderer receives this exact immutable evidence snapshot.
        session = JsonSerializer.Deserialize<DiagnosticSession>(JsonSerializer.Serialize(session))!;
        if (comparison is not null) comparison = JsonSerializer.Deserialize<DiagnosticComparison>(JsonSerializer.Serialize(comparison));
        if (redact)
        {
            var p = session.Request.Parameters with { ResolverAddress = session.Request.Parameters.ResolverAddress is null ? null : "[redacted resolver]" };
            session = session with { Analysis = session.Analysis?.Select(o => o with { Text = o.Text is null ? null : "[redacted analysis text]" }).ToArray(), Request = session.Request with { Target = "[redacted target]", Parameters = p }, Error = session.Error is null ? null : "[redacted error text]",
                Results = session.Results.Select(r => r with {
                    Target = "[redacted target]", Error = r.Error is null ? null : "[redacted error text]",
                    Observations = r.Observations.Select(o => o with { Text = o.Text is null ? null : "[redacted observation text]" }).ToArray(),
                    Dns = r.Dns is null ? null : r.Dns with { Resolver = "[redacted resolver]", Answers = r.Dns.Answers.Select(a => a with { Name = "[redacted name]", Address = "[redacted address]" }).ToArray() },
                    Hops = r.Hops?.Select(h => h with { Addresses = h.Addresses.Select(_ => "[redacted address]").ToArray(), Hostname = h.Hostname is null ? null : "[redacted name]" }).ToArray(),
                    Network = r.Network is null ? null : r.Network with { Interfaces = r.Network.Interfaces.Select(n => n with { Id = "[redacted interface]", Name = "[redacted interface]", Addresses = n.Addresses.Select(_ => "[redacted address]").ToArray(), Gateways = n.Gateways.Select(_ => "[redacted gateway]").ToArray(), DnsServers = n.DnsServers.Select(_ => "[redacted resolver]").ToArray() }).ToArray(), Routes = r.Network.Routes.Select(_ => "[redacted route]").ToArray(), Limitations = r.Network.Limitations.Select(_ => "[redacted provider detail]").ToArray() }
                }).ToArray() };
            if (incident is not null) incident = incident with { GroupName = "[redacted group]", Comment = "[redacted comment]" };
            if (comparison is not null) comparison = comparison with { Differences = comparison.Differences.Select(o => o with { Text = o.Text is null ? null : "[redacted comparison text]" }).ToArray() };
        }
        return new(1, DateTimeOffset.UtcNow, redact, redact ? "Target, resolver, DNS answers, hop addresses/names, interface identifiers/addresses/routes, incident group/comment, error and observation text redacted before rendering. UTC, units, numerical metrics and IDs retained." : "User explicitly selected unredacted local export. Network addresses, interface configuration and incident comments may be sensitive.", session, incident, comparison,
            ["Observations describe only the configured test window. Diagnostic results never alter monitoring incident recovery.",
             "No raw packet capture, enrichment, remote probing, payloads or service credentials are collected by this candidate.",
             "RTT uses monotonic timing; evidence timestamps use UTC. Missing measurements remain unknown.",
             "SHA-256 detects changed bytes; it does not prove origin or stop an attacker from regenerating a manifest.",
             "PDF renders non-ASCII characters using visible Unicode escape sequences to preserve portable text without platform fonts."]);
    }
    public static string ToMarkdown(EvidenceDocument evidence)
    {
        var s = evidence.Session; var text = new StringBuilder();
        void Line(string value = "") => text.AppendLine(value);
        Line("# Synups diagnostic evidence"); Line();
        Line($"Evidence schema: {evidence.Version} | Exported UTC: {evidence.CreatedUtc:O}");
        Line($"Session: {s.Request.Id} | State: {s.State}"); Line($"Target: {s.Request.Target}");
        Line($"Started UTC: {s.StartedUtc:O} | Completed UTC: {s.CompletedUtc:O}");
        Line($"Application: {s.ApplicationVersion} | Platform: {s.Platform}"); Line($"Profile: {s.Request.ProfileId} version {s.Request.ProfileVersion}");
        Line($"Effective parameters: {JsonSerializer.Serialize(s.Request.Parameters)}");
        Line($"Consent snapshot: {JsonSerializer.Serialize(s.Request.Consent)}"); Line(); Line(evidence.RedactionDeclaration); Line();
        if (evidence.Incident is { } incident)
        { Line("## Linked incident timeline"); Line($"ID: {incident.Id} | Group: {incident.GroupName}"); Line($"First missed UTC: {incident.FirstFailureUtc:O}"); Line($"Confirmed UTC: {incident.StartedUtc:O}"); Line($"Observed recovery UTC: {incident.RecoveredUtc:O}"); Line($"Observation end UTC: {incident.ObservationEndedUtc:O}"); Line($"Elapsed seconds: {incident.Elapsed.TotalSeconds.ToString("R", CultureInfo.InvariantCulture)} | Observation gap: {incident.ContainsObservationGap}"); Line($"Comment: {incident.Comment}"); }
        foreach (var r in s.Results)
        {
            Line(); Line($"## {r.Kind}: {r.Outcome}"); Line($"Method: {r.Method}"); Line($"UTC: {r.StartedUtc:O} through {r.CompletedUtc:O}");
            Line($"Elapsed: {r.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms"); if (r.Error is not null) Line($"Error: {r.Error}");
            foreach (var o in r.Observations) Line($"- [{o.Certainty}] {o.Utc:O} {o.Name}: {o.Value?.ToString("R", CultureInfo.InvariantCulture)} {o.Unit} {o.Text}");
            if (r.Statistics is { } stats) { Line($"Statistics: {JsonSerializer.Serialize(stats)}"); Line("Jitter: mean absolute difference of adjacent successful RTT samples; nonresponses break adjacency. Standard deviation: population over successful RTTs. Missing RTTs are not imputed."); }
            if (r.Dns is { } dns) { Line($"Resolver actually queried: {dns.Resolver}; query: {dns.QueryType}; RCODE: {dns.ResponseCode}; transport: {dns.Transport}; cache: {dns.CachePolicy}"); foreach (var a in dns.Answers) Line($"DNS {a.Type}: {a.Name} -> {a.Address}; TTL {a.TtlSeconds} s"); }
            if (r.Hops is { } hops) foreach (var h in hops) Line($"Hop {h.Hop}: {string.Join(", ", h.Addresses)}; RTT ms [{string.Join(", ", h.RttMilliseconds.Select(n => n.ToString("R", CultureInfo.InvariantCulture)))}]; unanswered {h.Unanswered}; name {h.Hostname ?? "unresolved"}");
            if (r.Network is { } network) { Line($"Network snapshot {network.Utc:O}; provider {network.Provider}"); foreach (var n in network.Interfaces) { Line($"Interface {n.Id} {n.Name} ({n.Type}, {n.Status}); IPv4 index {n.IPv4Index}; IPv6 index {n.IPv6Index}"); Line($"Addresses: {string.Join(", ", n.Addresses)}; gateways: {string.Join(", ", n.Gateways)}; DNS: {string.Join(", ", n.DnsServers)}"); } foreach (var route in network.Routes) Line($"Route: {route}"); foreach (var limitation in network.Limitations) Line($"Unknown: {limitation}"); }
        }
        if (s.Analysis is { } analysis) { Line(); Line("## Cautious cross-test analysis"); foreach (var o in analysis) Line($"[{o.Certainty}] {o.Name}: {o.Text}"); }
        if (evidence.Comparison is { } c) { Line(); Line("## Baseline comparison"); Line($"Baseline: {c.BaselineId}; current session: {c.SessionId}; UTC: {c.Utc:O}"); foreach (var o in c.Differences) Line($"[{o.Certainty}] {o.Name}: {o.Value?.ToString("R", CultureInfo.InvariantCulture)} {o.Unit} {o.Text}"); }
        Line(); Line("## Limitations"); foreach (var l in evidence.Limitations) Line("- " + l);
        return text.ToString();
    }
    // Canonical object member order, no insignificant whitespace; arrays retain observation order; UTF-8 JSON.
    public static byte[] CanonicalJson<T>(T value)
    {
        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value)); using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) Write(writer, json.RootElement);
        return bytes.ToArray();
        static void Write(Utf8JsonWriter writer, JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var property in node.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Write(writer, property.Value); } writer.WriteEndObject(); }
            else if (node.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in node.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
            else node.WriteTo(writer);
        }
    }
    public static Task<EvidenceExportResult> ExportAsync(string directory, EvidenceDocument evidence, CancellationToken cancellation = default) => Task.Run(async () =>
    {
        if (!Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory)) throw new ArgumentException("Select an existing absolute export folder.");
        var name = $"Synups-evidence-{evidence.Session.Request.Id:N}-{Guid.NewGuid():N}";
        var temporary = Path.Combine(directory, "." + name + ".partial"); var destination = Path.Combine(directory, name);
        System.IO.Directory.CreateDirectory(temporary);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var json = CanonicalJson(evidence);
            if (json.Length > 16_000_000) throw new InvalidDataException("Evidence artifact exceeds 16 MB.");
            var markdown = ToMarkdown(evidence);
            if (Encoding.UTF8.GetByteCount(markdown) > 16_000_000) throw new InvalidDataException("Evidence artifact exceeds 16 MB.");
            cancellation.ThrowIfCancellationRequested();
            var payloads = new Dictionary<string, byte[]> { ["evidence.json"] = json, ["evidence.md"] = Encoding.UTF8.GetBytes(markdown), ["evidence.pdf"] = PortableEvidencePdf.Render(markdown, cancellation) };
            var manifest = new List<object>();
            foreach (var (file, bytes) in payloads)
            {
                cancellation.ThrowIfCancellationRequested(); if (bytes.Length > 16_000_000) throw new InvalidDataException("Evidence artifact exceeds 16 MB.");
                await File.WriteAllBytesAsync(Path.Combine(temporary, file), bytes, cancellation).ConfigureAwait(false);
                manifest.Add(new { File = file, Bytes = bytes.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
            }
            await File.WriteAllBytesAsync(Path.Combine(temporary, "manifest.json"), CanonicalJson(new { Version = 1, Algorithm = "SHA-256", Scope = "Exact exported bytes; integrity, not origin authentication", Files = manifest }), cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); System.IO.Directory.Move(temporary, destination);
            return new EvidenceExportResult(destination, payloads.Keys.Select(f => Path.Combine(destination, f)).ToArray(), Path.Combine(destination, "manifest.json"));
        }
        finally { if (System.IO.Directory.Exists(temporary)) System.IO.Directory.Delete(temporary, true); }
    }, cancellation);
}

/// <summary>Small deterministic PDF 1.4 text renderer, fixed-width wrapping and true multipage output. No OS/font dependency.</summary>
internal static class PortableEvidencePdf
{
    public static byte[] Render(string markdown, CancellationToken cancellation = default)
    {
        static string Portable(string value) { var b = new StringBuilder(); foreach (var c in value) { if (c is >= ' ' and <= '~') b.Append(c); else if (c == '\t') b.Append("    "); else if (c != '\r') b.Append($"\\u{(int)c:X4}"); } return b.ToString(); }
        var lines = new List<string>();
        foreach (var raw in markdown.Split('\n'))
        {
            cancellation.ThrowIfCancellationRequested();
            var text = Portable(raw); if (text.Length == 0) lines.Add("");
            while (text.Length > 0) { var count = Math.Min(98, text.Length); lines.Add(text[..count]); text = text[count..]; }
        }
        var pages = lines.Chunk(64).ToArray(); var objects = new List<byte[]>();
        static byte[] B(string s) => Encoding.ASCII.GetBytes(s);
        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        objects.Add(B("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(B($"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(" ", Enumerable.Range(0, pages.Length).Select(i => $"{4 + i * 2} 0 R"))}] >>"));
        objects.Add(B("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));
        for (var i = 0; i < pages.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var pageId = 4 + i * 2; var content = new StringBuilder("BT /F1 8 Tf 10 TL 42 780 Td\n");
            content.Append($"({Escape("SYNUPS EVIDENCE - UTC timestamps")}) Tj T* T*\n");
            foreach (var line in pages[i]) content.Append($"({Escape(line)}) Tj T*\n");
            content.Append($"ET\nBT /F1 8 Tf 42 40 Td (Page {i + 1} of {pages.Length}) Tj ET\n");
            var data = B(content.ToString());
            objects.Add(B($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {pageId + 1} 0 R >>"));
            objects.Add(B($"<< /Length {data.Length} >>\nstream\n").Concat(data).Concat(B("endstream")).ToArray());
        }
        using var output = new MemoryStream(); output.Write(B("%PDF-1.4\n")); var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(output.Position); output.Write(B($"{i + 1} 0 obj\n")); output.Write(objects[i]); output.Write(B("\nendobj\n")); }
        var xref = output.Position; output.Write(B($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1)) output.Write(B($"{offset:0000000000} 00000 n \n"));
        output.Write(B($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n")); return output.ToArray();
    }
}
