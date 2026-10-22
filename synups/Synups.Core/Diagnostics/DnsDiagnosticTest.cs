using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Synups.Core.Diagnostics;

/// <summary>One explicit, uncached DNS wire query to a configured or approved resolver.</summary>
public sealed class DnsDiagnosticTest : IDiagnosticTest
{
    private readonly IPEndPoint? _resolver;
    private readonly Func<IPEndPoint?> _configuredResolverProvider;
    public DnsDiagnosticTest(Func<IPEndPoint?>? configuredResolverProvider = null) =>
        _configuredResolverProvider = configuredResolverProvider ?? DnsWire.FindConfiguredResolver;
    /// <summary>Override for an application-controlled resolver (including local test fixtures).</summary>
    public DnsDiagnosticTest(IPEndPoint resolver)
    {
        _resolver = resolver;
        _configuredResolverProvider = DnsWire.FindConfiguredResolver;
    }
    public DiagnosticKind Kind => DiagnosticKind.Dns;

    public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        request = request.ValidatedCopy();
        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var observations = new List<DiagnosticObservation>();
        var answers = new List<DnsAnswer>();
        int? rcode = null;
        var resolver = !string.IsNullOrWhiteSpace(request.Parameters.ResolverAddress)
            ? new IPEndPoint(IPAddress.Parse(request.Parameters.ResolverAddress), 53)
            : _resolver ?? _configuredResolverProvider();
        var outcome = DiagnosticOutcome.Unsupported;
        string? error = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (IPAddress.TryParse(request.Target, out _))
                error = "Forward DNS requires a hostname; an IP literal has no A/AAAA hostname question.";
            else if (resolver is null)
                error = "No active interface DNS resolver is available. Approve an explicit resolver to query one.";
            else
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(request.Parameters.ProbeTimeoutMilliseconds);
                progress?.Report(new(request.Id, Kind, "Querying resolver", 0, 1));
                var response = await DnsWire.QueryAsync(resolver, request.Target, (ushort)request.Parameters.RecordType, timeout.Token).ConfigureAwait(false);
                rcode = response.ResponseCode;
                if (response.Truncated)
                {
                    outcome = DiagnosticOutcome.Unsupported;
                    error = "Resolver set the truncated flag. UDP answer is incomplete; TCP retry is not implemented.";
                }
                else if (rcode != 0)
                {
                    outcome = rcode == 5 ? DiagnosticOutcome.Refused : DiagnosticOutcome.DnsFailure;
                    error = $"Resolver returned DNS RCODE {rcode} ({DnsWire.ResponseCodeName(rcode.Value)}).";
                }
                else
                {
                    foreach (var record in response.Answers.Where(r => r.Address is not null))
                        answers.Add(new(record.Name, (DnsRecordType)record.Type, record.Address!, record.Ttl));
                    outcome = answers.Count > 0 ? DiagnosticOutcome.Success : DiagnosticOutcome.NoAnswer;
                }
            }
        }
        catch (OperationCanceledException)
        {
            outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout;
            error = outcome == DiagnosticOutcome.Cancelled ? "DNS query cancelled." : "DNS resolver did not answer within the probe deadline.";
        }
        catch (InvalidDataException ex) { outcome = DiagnosticOutcome.MalformedResponse; error = ex.Message; }
        catch (ArgumentException ex) { outcome = DiagnosticOutcome.MalformedResponse; error = ex.Message; }
        catch (SocketException ex)
        {
            outcome = ex.SocketErrorCode is SocketError.AccessDenied ? DiagnosticOutcome.PermissionDenied
                : ex.SocketErrorCode is SocketError.HostUnreachable or SocketError.NetworkUnreachable ? DiagnosticOutcome.Unreachable : DiagnosticOutcome.NetworkError;
            error = ex.Message;
        }
        if (rcode.HasValue) observations.Add(new(DateTimeOffset.UtcNow, "DNS response code", DiagnosticCertainty.Observed, rcode, Text: DnsWire.ResponseCodeName(rcode.Value)));
        observations.Add(new(DateTimeOffset.UtcNow, "DNS resolver selection", DiagnosticCertainty.Observed, Text:
            request.Parameters.ResolverAddress is not null ? "User approved explicit resolver" : _resolver is not null ? "Application supplied resolver" : "First DNS resolver on an active interface; split DNS routing is not inferred"));
        progress?.Report(new(request.Id, Kind, "Finished", 1, 1));
        return new(Guid.NewGuid(), Kind, request.Target, started, DateTimeOffset.UtcNow, outcome, "Explicit DNS A/AAAA query over UDP", watch.Elapsed.TotalMilliseconds,
            observations, error, Dns: new(resolver?.ToString() ?? "Unavailable", request.Parameters.RecordType, rcode, answers));
    }
}

internal sealed record DnsWireRecord(string Name, ushort Type, uint Ttl, string? Address = null, string? Domain = null);
internal sealed record DnsWireResponse(int ResponseCode, bool Truncated, IReadOnlyList<DnsWireRecord> Answers, uint? CacheTtl = null);

internal static class DnsWire
{
    internal static IPEndPoint? FindConfiguredResolver()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            foreach (var address in adapter.GetIPProperties().DnsAddresses)
                if (!address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any)) return new(address, 53);
        }
        catch (NetworkInformationException) { }
        catch (PlatformNotSupportedException) { }
        return null;
    }

    internal static string ResponseCodeName(int code) => code switch { 0 => "NOERROR", 1 => "FORMERR", 2 => "SERVFAIL", 3 => "NXDOMAIN", 4 => "NOTIMP", 5 => "REFUSED", _ => "Other" };

    internal static async Task<DnsWireResponse> QueryAsync(IPEndPoint resolver, string domain, ushort type, CancellationToken cancellation)
    {
        var canonical = new System.Globalization.IdnMapping().GetAscii(domain.TrimEnd('.')).ToLowerInvariant();
        var id = (ushort)RandomNumberGenerator.GetInt32(65536);
        var query = BuildQuery(canonical, type, id);
        using var socket = new Socket(resolver.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        await socket.ConnectAsync(resolver, cancellation).ConfigureAwait(false);
        await socket.SendAsync(query, SocketFlags.None, cancellation).ConfigureAwait(false);
        // One extra byte detects a datagram larger than the parser bound even if a valid prefix fits exactly.
        var buffer = new byte[4097];
        int size;
        try { size = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellation).ConfigureAwait(false); }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize)
        { throw new InvalidDataException("DNS datagram exceeds the 4096-byte response bound.", ex); }
        if (size > 4096) throw new InvalidDataException("DNS datagram exceeds the 4096-byte response bound.");
        return Parse(buffer.AsSpan(0, size), canonical, type, id);
    }

    private static byte[] BuildQuery(string name, ushort type, ushort id)
    {
        using var stream = new MemoryStream();
        var header = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header, id);
        header[2] = 1; header[5] = 1; // Recursion desired, one question.
        stream.Write(header);
        foreach (var label in name.Split('.'))
        {
            if (label.Length is < 1 or > 63) throw new ArgumentException("DNS label is outside the 1–63 byte bounds.");
            stream.WriteByte((byte)label.Length); stream.Write(Encoding.ASCII.GetBytes(label));
        }
        stream.WriteByte(0);
        if (stream.Length > 267) throw new ArgumentException("DNS name exceeds its wire length bound.");
        stream.WriteByte((byte)(type >> 8)); stream.WriteByte((byte)type); stream.WriteByte(0); stream.WriteByte(1);
        return stream.ToArray();
    }

    private static DnsWireResponse Parse(ReadOnlySpan<byte> packet, string question, ushort type, ushort id)
    {
        if (packet.Length < 12) throw new InvalidDataException("DNS response header is incomplete.");
        return ParseBody(packet, question, type, id);
    }

    private static DnsWireResponse ParseBody(ReadOnlySpan<byte> packet, string question, ushort type, ushort id)
    {
        var responseId = BinaryPrimitives.ReadUInt16BigEndian(packet);
        var flags = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
        var questions = BinaryPrimitives.ReadUInt16BigEndian(packet[4..]);
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(packet[6..]);
        var authorityCount = BinaryPrimitives.ReadUInt16BigEndian(packet[8..]);
        var additionalCount = BinaryPrimitives.ReadUInt16BigEndian(packet[10..]);
        if (responseId != id || (flags & 0x8000) == 0 || (flags & 0x7800) != 0 || questions != 1)
            throw new InvalidDataException("DNS response transaction, opcode, or question count does not match.");
        if (answerCount + authorityCount + additionalCount > 256) throw new InvalidDataException("DNS response record count exceeds the bounded parser limit.");
        var offset = 12;
        var questionName = ReadName(packet, ref offset);
        Require(packet, offset, 4);
        if (!questionName.Equals(question, StringComparison.OrdinalIgnoreCase) || BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]) != type || BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 2)..]) != 1)
            throw new InvalidDataException("DNS response question does not match the requested hostname, class, and type.");
        offset += 4;
        if ((flags & 0x0200) != 0) return new(flags & 15, true, []);
        var records = new List<DnsWireRecord>();
        for (var i = 0; i < answerCount + authorityCount + additionalCount; i++)
        {
            var name = ReadName(packet, ref offset);
            Require(packet, offset, 10);
            var recordType = BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]);
            var recordClass = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 2)..]);
            var ttl = BinaryPrimitives.ReadUInt32BigEndian(packet[(offset + 4)..]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(packet[(offset + 8)..]);
            offset += 10; Require(packet, offset, length);
            string? address = null, target = null;
            if (recordClass == 1 && (recordType == 1 || recordType == 28))
            {
                if (length != (recordType == 1 ? 4 : 16)) throw new InvalidDataException("DNS address record has an invalid data length.");
                address = new IPAddress(packet.Slice(offset, length)).ToString();
            }
            else if (recordClass == 1 && (recordType == 5 || recordType == 12))
            {
                var nameOffset = offset;
                target = ReadName(packet, ref nameOffset);
                if (nameOffset != offset + length) throw new InvalidDataException("DNS domain record has an invalid data length.");
            }
            if (i < answerCount && recordClass == 1) records.Add(new(name, recordType, ttl, address, target));
            offset += length;
        }
        if (offset != packet.Length) throw new InvalidDataException("DNS response contains trailing or unparsed bytes.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { question };
        for (var i = 0; i < 16; i++)
        {
            var added = false;
            foreach (var record in records.Where(r => r.Type == 5 && r.Domain is not null))
                if (names.Contains(record.Name)) added |= names.Add(record.Domain!);
            if (!added) break;
        }
        var relevant = records.Where(r => (r.Type == type || r.Type == 5) && names.Contains(r.Name)).ToArray();
        return new(flags & 15, false, relevant.Where(r => r.Type == type).ToArray(), relevant.Length > 0 ? relevant.Min(r => r.Ttl) : null);
    }

    private static void Require(ReadOnlySpan<byte> packet, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > packet.Length - length) throw new InvalidDataException("DNS response is incomplete or has an out-of-bounds field.");
    }

    private static string ReadName(ReadOnlySpan<byte> packet, ref int offset)
    {
        var labels = new List<string>();
        var position = offset;
        var jumped = false;
        var visited = new HashSet<int>();
        var nameLength = 0;
        for (var steps = 0; steps < 128; steps++)
        {
            Require(packet, position, 1);
            if (!visited.Add(position)) throw new InvalidDataException("DNS compression pointer loop.");
            var size = packet[position++];
            if ((size & 0xc0) == 0xc0)
            {
                Require(packet, position, 1);
                var pointer = ((size & 0x3f) << 8) | packet[position++];
                if (!jumped) offset = position;
                jumped = true; position = pointer;
                continue;
            }
            if ((size & 0xc0) != 0) throw new InvalidDataException("DNS label uses an unsupported encoding.");
            if (size == 0)
            {
                if (!jumped) offset = position;
                return string.Join('.', labels);
            }
            Require(packet, position, size);
            nameLength += size + 1;
            if (nameLength > 254) throw new InvalidDataException("DNS response name exceeds its wire length bound.");
            var bytes = packet.Slice(position, size);
            foreach (var b in bytes) if (b < 33 || b > 126 || b == '.') throw new InvalidDataException("DNS response name contains an invalid label byte.");
            labels.Add(Encoding.ASCII.GetString(bytes)); position += size;
        }
        throw new InvalidDataException("DNS compression exceeds the bounded parser depth.");
    }
}
