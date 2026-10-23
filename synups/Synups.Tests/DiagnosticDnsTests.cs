using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Synups.Core.Diagnostics;
using Xunit;

namespace Synups.Tests;

public class DiagnosticDnsTests
{
    private static DiagnosticRequest Request(DnsRecordType type = DnsRecordType.A, int timeout = 500) =>
        new(Guid.NewGuid(), "fixture.example", [DiagnosticKind.Dns], new() { RecordType = type, ProbeTimeoutMilliseconds = timeout }, new(true));

    [Theory]
    [InlineData(DnsRecordType.A, "192.0.2.42")]
    [InlineData(DnsRecordType.AAAA, "2001:db8::42")]
    public async Task ExplicitWireQueryPreservesAddressTypeTtlResolverAndRcode(DnsRecordType type, string address)
    {
        await using var server = new DnsServer(q => Answer(q, (ushort)type, IPAddress.Parse(address).GetAddressBytes(), 123));
        var result = await new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(Request(type), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome);
        var dns = Assert.IsType<DnsDiagnosticData>(result.Dns);
        Assert.Equal(server.Endpoint.ToString(), dns.Resolver);
        Assert.Equal(0, dns.ResponseCode);
        Assert.Equal(type, dns.QueryType);
        var answer = Assert.Single(dns.Answers);
        Assert.Equal(address, answer.Address); Assert.Equal(type, answer.Type); Assert.Equal(123u, answer.TtlSeconds);
        Assert.Equal("fixture.example", answer.Name);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(1, server.Count);
    }

    [Theory]
    [InlineData(3, DiagnosticOutcome.DnsFailure)]
    [InlineData(2, DiagnosticOutcome.DnsFailure)]
    [InlineData(5, DiagnosticOutcome.Refused)]
    public async Task NegativeResponsesRetainObservedRcode(int code, DiagnosticOutcome outcome)
    {
        await using var server = new DnsServer(q => Header(q, (ushort)(0x8180 | code)));
        var result = await new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(code, result.Dns!.ResponseCode);
        Assert.Empty(result.Dns.Answers);
    }

    [Fact]
    public async Task NoDataAndTruncationAreDistinctFromSuccess()
    {
        await using var empty = new DnsServer(q => Header(q, 0x8180));
        var noData = await new DnsDiagnosticTest(empty.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.NoAnswer, noData.Outcome);
        await using var truncated = new DnsServer(q => Header(q, 0x8380));
        var incomplete = await new DnsDiagnosticTest(truncated.Endpoint).ExecuteAsync(Request() with { Parameters = new() { MaximumProbes = 1 } }, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Unsupported, incomplete.Outcome);
        Assert.Contains("approved probe cap", incomplete.Error);
        Assert.Empty(incomplete.Dns!.Answers);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("id")]
    [InlineData("question")]
    [InlineData("loop")]
    [InlineData("out-of-bounds")]
    [InlineData("length")]
    [InlineData("count")]
    [InlineData("trailing")]
    public async Task MalformedWireDataIsRejected(string mutation)
    {
        await using var server = new DnsServer(q =>
        {
            var response = Answer(q, 1, [192, 0, 2, 1]);
            var end = q.Length;
            switch (mutation)
            {
                case "short": return [0, 1];
                case "id": response[0] ^= 1; break;
                case "question": response[13] ^= 1; break;
                case "loop": response[end] = (byte)(0xc0 | (end >> 8)); response[end + 1] = (byte)end; break;
                case "out-of-bounds": response[end] = 0xff; response[end + 1] = 0xff; break;
                case "length": response[end + 11] = 16; break;
                case "count": response[6] = 1; response[7] = 1; break;
                case "trailing": return [.. response, 0];
            }
            return response;
        });
        var result = await new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.MalformedResponse, result.Outcome);
        Assert.Empty(result.Dns!.Answers);
    }

    [Fact]
    public async Task OversizedDatagramIsRejectedBeforeParsingOnEveryPlatform()
    {
        await using var server = new DnsServer(q =>
        {
            var prefix = Answer(q, 1, [192, 0, 2, 1]);
            var excessive = new byte[9000]; prefix.CopyTo(excessive, 0); return excessive;
        });
        var result = await new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.MalformedResponse, result.Outcome);
        Assert.Empty(result.Dns!.Answers); Assert.Contains("4096", result.Error);
    }

    [Fact]
    public async Task CnameAnswersFollowOnlyTheRequestedChain()
    {
        await using var server = new DnsServer(q =>
        {
            var cname = Name("alias.example");
            var packet = Answer(q, 5, cname);
            packet[7] = 2;
            return [.. packet, .. Name("alias.example"), 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 8];
        });
        var result = await new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome);
        Assert.Equal("alias.example", Assert.Single(result.Dns!.Answers).Name);
        await using var unrelated = new DnsServer(q =>
        {
            var packet = Header(q, 0x8180); packet[7] = 1;
            return [.. packet, .. Name("unrelated.example"), 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 9];
        });
        var rejected = await new DnsDiagnosticTest(unrelated.Endpoint).ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.NoAnswer, rejected.Outcome);
    }

    [Fact]
    public async Task TimeoutsAndCancellationCloseOwnedQueries()
    {
        await using var silent = new DnsServer(_ => null);
        var adapter = new DnsDiagnosticTest(silent.Endpoint);
        var timeout = await adapter.ExecuteAsync(Request(timeout: 30), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Timeout, timeout.Outcome);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var cancelled = await adapter.ExecuteAsync(Request(), null, cancel.Token);
        Assert.Equal(DiagnosticOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(1, silent.Count);
    }

    [Fact]
    public async Task AlternativeResolverRequiresExplicitConsentBeforeNetworkActivity()
    {
        await using var server = new DnsServer(q => Answer(q, 1, [192, 0, 2, 1]));
        var request = Request() with { Parameters = new() { ResolverAddress = "127.0.0.1" } };
        await Assert.ThrowsAsync<ArgumentException>(() => new DnsDiagnosticTest(server.Endpoint).ExecuteAsync(request, null, TestContext.Current.CancellationToken));
        Assert.Equal(0, server.Count);
    }

    [Fact]
    public async Task ReverseCacheDeduplicatesAndCachesPtrWithRawFallback()
    {
        await using var server = new DnsServer(q => Answer(q, 12, Name("router.fixture.example"), 60));
        await using var cache = new ReverseDnsCache(resolver: server.Endpoint);
        var address = IPAddress.Parse("192.0.2.1");
        var lookups = Enumerable.Range(0, 20).Select(_ => cache.ResolveAsync(address, TestContext.Current.CancellationToken)).ToArray();
        Assert.All(await Task.WhenAll(lookups), name => Assert.Equal("router.fixture.example", name));
        Assert.Equal("router.fixture.example", await cache.ResolveAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal(1, server.Count);
        Assert.Equal("1.2.0.192.in-addr.arpa", server.LastQuestion);
        Assert.Equal(1, cache.EntryCount); Assert.Equal(0, cache.ActiveLookups);
    }

    [Fact]
    public async Task ReverseCacheHonorsZeroTtlAndIpv6NibbleOrdering()
    {
        await using var server = new DnsServer(q => Answer(q, 12, Name("ipv6.fixture.example"), 0));
        await using var cache = new ReverseDnsCache(resolver: server.Endpoint);
        var address = IPAddress.Parse("2001:db8::1");
        Assert.Equal("ipv6.fixture.example", await cache.ResolveAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal("ipv6.fixture.example", await cache.ResolveAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal(2, server.Count);
        Assert.Equal("1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa", server.LastQuestion);
    }

    [Fact]
    public async Task ReverseCacheCapsPendingTasksAndCancellationDrainsThem()
    {
        await using var server = new DnsServer(_ => null);
        var cache = new ReverseDnsCache(capacity: 2, maximumConcurrent: 2, timeoutMilliseconds: 1000, resolver: server.Endpoint);
        var first = cache.ResolveAsync(IPAddress.Parse("192.0.2.1"), TestContext.Current.CancellationToken);
        var second = cache.ResolveAsync(IPAddress.Parse("192.0.2.2"), TestContext.Current.CancellationToken);
        Assert.Equal("192.0.2.3", await cache.ResolveAsync(IPAddress.Parse("192.0.2.3"), TestContext.Current.CancellationToken));
        Assert.Equal(2, cache.EntryCount); Assert.Equal(2, cache.ActiveLookups);
        await cache.DisposeAsync();
        Assert.Equal("192.0.2.1", await first); Assert.Equal("192.0.2.2", await second);
        Assert.Equal(0, cache.ActiveLookups); Assert.Equal(0, cache.EntryCount);
        Assert.Equal("192.0.2.4", await cache.ResolveAsync(IPAddress.Parse("192.0.2.4"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReverseCallerCancellationDoesNotAbortSharedLookupAndFailuresAreCached()
    {
        await using var server = new DnsServer(_ => null);
        await using var cache = new ReverseDnsCache(timeoutMilliseconds: 80, resolver: server.Endpoint);
        var address = IPAddress.Parse("192.0.2.1");
        var shared = cache.ResolveAsync(address, TestContext.Current.CancellationToken);
        using var caller = new CancellationTokenSource(); caller.Cancel();
        Assert.Equal(address.ToString(), await cache.ResolveAsync(address, caller.Token));
        Assert.Equal(1, cache.ActiveLookups);
        Assert.Equal(address.ToString(), await shared);
        Assert.Equal(address.ToString(), await cache.ResolveAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal(1, server.Count); Assert.Equal(0, cache.ActiveLookups);
    }

    [Fact]
    public async Task ReverseCacheEvictsCompletedEntriesAtCapacity()
    {
        await using var server = new DnsServer(q => Header(q, 0x8183));
        await using var cache = new ReverseDnsCache(capacity: 1, resolver: server.Endpoint);
        foreach (var ip in new[] { "192.0.2.1", "192.0.2.2", "192.0.2.1" })
            Assert.Equal(ip, await cache.ResolveAsync(IPAddress.Parse(ip), TestContext.Current.CancellationToken));
        Assert.Equal(3, server.Count); Assert.Equal(1, cache.EntryCount);
    }

    [Fact]
    public async Task MissingOsResolverIsExplicitAndReverseLookupStaysRaw()
    {
        var result = await new DnsDiagnosticTest(configuredResolverProvider: () => null)
            .ExecuteAsync(Request(), null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Unsupported, result.Outcome);
        Assert.Equal("Unavailable", result.Dns!.Resolver);
        Assert.Contains("No active interface DNS resolver", result.Error);
        await using var cache = new ReverseDnsCache(configuredResolverProvider: () => null);
        Assert.Equal("192.0.2.1", await cache.ResolveAsync(IPAddress.Parse("192.0.2.1"), TestContext.Current.CancellationToken));
        Assert.Equal(0, cache.ActiveLookups); Assert.Equal(0, cache.EntryCount);
    }

    [Fact]
    public async Task NativeConfiguredResolverQueryIsExplicitOptIn()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_REAL_NETWORK_TESTS") != "1")
            Assert.Skip("Opt-in configured-resolver query of example.com; set SYNUPS_REAL_NETWORK_TESTS=1.");
        var result = await new DnsDiagnosticTest().ExecuteAsync(Request() with { Target = "example.com" }, null, TestContext.Current.CancellationToken);
        Assert.Equal(DiagnosticOutcome.Success, result.Outcome);
        Assert.NotEmpty(result.Dns!.Answers);
        Assert.Equal(0, result.Dns.ResponseCode);
    }

    [Theory]
    [InlineData("answer", DiagnosticOutcome.Success)]
    [InlineData("short", DiagnosticOutcome.MalformedResponse)]
    [InlineData("oversized", DiagnosticOutcome.MalformedResponse)]
    [InlineData("wrong-id", DiagnosticOutcome.MalformedResponse)]
    [InlineData("truncated", DiagnosticOutcome.Unsupported)]
    [InlineData("refused", DiagnosticOutcome.Refused)]
    [InlineData("silent", DiagnosticOutcome.Timeout)]
    [InlineData("cancel", DiagnosticOutcome.Cancelled)]
    public async Task TruncatedUdpRetriesOneBoundedFramedTcpQueryWithSharedDeadline(string mode, DiagnosticOutcome expected)
    {
        using var stop = new CancellationTokenSource();
        var tcp = new TcpListener(IPAddress.Loopback, 0); tcp.Start();
        var endpoint = (IPEndPoint)tcp.LocalEndpoint;
        using var udp = new UdpClient(endpoint);
        var reachedTcp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = Task.Run(async () =>
        {
            try
            {
                var q = await udp.ReceiveAsync(stop.Token);
                await udp.SendAsync(Header(q.Buffer, 0x8380), q.RemoteEndPoint, stop.Token);
                using var peer = await tcp.AcceptTcpClientAsync(stop.Token);
                await using var stream = peer.GetStream();
                var prefix = new byte[2]; await stream.ReadExactlyAsync(prefix, stop.Token);
                var query = new byte[BinaryPrimitives.ReadUInt16BigEndian(prefix)];
                await stream.ReadExactlyAsync(query, stop.Token);
                Assert.Equal(q.Buffer, query); reachedTcp.SetResult();
                if (mode is "silent" or "cancel") { await Task.Delay(Timeout.Infinite, stop.Token); return; }
                var response = mode == "truncated" ? Header(query, 0x8380) : mode == "refused" ? Header(query, 0x8185) : Answer(query, 1, [192, 0, 2, 42]);
                if (mode == "wrong-id") response[0] ^= 1;
                BinaryPrimitives.WriteUInt16BigEndian(prefix, mode == "oversized" ? (ushort)4097 : checked((ushort)response.Length));
                // Deliberately fragmented length prefix and body exercise exact stream reads.
                await stream.WriteAsync(prefix.AsMemory(0, 1), stop.Token);
                await stream.WriteAsync(prefix.AsMemory(1, 1), stop.Token);
                if (mode == "oversized") return;
                if (mode == "short") { await stream.WriteAsync(response.AsMemory(0, 5), stop.Token); return; }
                foreach (var value in response) await stream.WriteAsync(new byte[] { value }, stop.Token);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }, TestContext.Current.CancellationToken);
        using var caller = new CancellationTokenSource();
        try
        {
            var operation = new DnsDiagnosticTest(endpoint).ExecuteAsync(Request(timeout: 1000), null, caller.Token);
            await reachedTcp.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (mode == "cancel") caller.Cancel();
            var result = await operation;
            Assert.Equal(expected, result.Outcome);
            Assert.Equal("UDP → TCP", result.Dns!.Transport);
            if (expected == DiagnosticOutcome.Success) Assert.Equal("192.0.2.42", Assert.Single(result.Dns.Answers).Address);
            else Assert.Empty(result.Dns.Answers);
        }
        finally { stop.Cancel(); tcp.Stop(); await fixture; }
    }

    private static byte[] Header(byte[] query, ushort flags)
    {
        var packet = query.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), flags);
        packet.AsSpan(6, 6).Clear();
        return packet;
    }
    private static byte[] Answer(byte[] query, ushort type, byte[] data, uint ttl = 60)
    {
        var packet = Header(query, 0x8180); packet[7] = 1;
        var record = new byte[12 + data.Length];
        record[0] = 0xc0; record[1] = 0x0c;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(2), type);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(6), ttl);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(10), (ushort)data.Length);
        data.CopyTo(record, 12);
        return [.. packet, .. record];
    }
    private static byte[] Name(string name) => [.. name.Split('.').SelectMany(label => new[] { (byte)label.Length }.Concat(Encoding.ASCII.GetBytes(label))), 0];

    private sealed class DnsServer : IAsyncDisposable
    {
        private readonly UdpClient _socket = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _receive;
        private int _count;
        internal IPEndPoint Endpoint => (IPEndPoint)_socket.Client.LocalEndPoint!;
        internal int Count => Volatile.Read(ref _count);
        internal string? LastQuestion { get; private set; }
        internal DnsServer(Func<byte[], byte[]?> respond) => _receive = RunAsync(respond);
        private async Task RunAsync(Func<byte[], byte[]?> respond)
        {
            try
            {
                while (true)
                {
                    var query = await _socket.ReceiveAsync(_stop.Token);
                    var labels = new List<string>(); var position = 12;
                    while (position < query.Buffer.Length && query.Buffer[position] != 0)
                    {
                        var length = query.Buffer[position++];
                        labels.Add(Encoding.ASCII.GetString(query.Buffer, position, length)); position += length;
                    }
                    LastQuestion = string.Join('.', labels);
                    Interlocked.Increment(ref _count);
                    var response = respond(query.Buffer);
                    if (response is not null) await _socket.SendAsync(response, query.RemoteEndPoint, _stop.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel(); await _receive; _socket.Dispose(); _stop.Dispose();
        }
    }
}
