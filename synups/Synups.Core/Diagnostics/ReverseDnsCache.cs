using System.Net;
using System.Net.Sockets;

namespace Synups.Core.Diagnostics;

/// <summary>
/// Session-owned, bounded PTR lookup cache. At capacity, names remain raw IPs.
/// Lookup cancellation closes its owned UDP socket; no native DNS work is abandoned.
/// </summary>
public sealed class ReverseDnsCache : IDisposable, IAsyncDisposable
{
    private sealed class Entry
    {
        internal Task<string> Task = null!;
        internal DateTimeOffset Expires = DateTimeOffset.MaxValue;
        internal long Access;
    }
    private readonly object _gate = new();
    private readonly Dictionary<IPAddress, Entry> _entries = new();
    private readonly CancellationTokenSource _lifetime;
    private readonly IPEndPoint? _resolver;
    private readonly int _capacity, _maximumConcurrent, _timeoutMilliseconds;
    private int _active;
    private long _access;
    private bool _disposed;

    public ReverseDnsCache(int capacity = 128, int maximumConcurrent = 4, int timeoutMilliseconds = 500,
        CancellationToken sessionCancellation = default, IPEndPoint? resolver = null, Func<IPEndPoint?>? configuredResolverProvider = null)
    {
        if (capacity is < 1 or > 1024 || maximumConcurrent is < 1 or > 16 || timeoutMilliseconds is < 10 or > 2000)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Reverse DNS bounds are capacity 1–1024, concurrency 1–16, timeout 10–2000 ms.");
        _capacity = capacity; _maximumConcurrent = maximumConcurrent; _timeoutMilliseconds = timeoutMilliseconds;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation);
        _resolver = resolver ?? (configuredResolverProvider ?? DnsWire.FindConfiguredResolver)();
    }

    public int EntryCount { get { lock (_gate) return _entries.Count; } }
    public int ActiveLookups { get { lock (_gate) return _active; } }

    /// <summary>Returns a PTR name or raw IP. Caller cancellation stops waiting without cancelling another caller's shared lookup.</summary>
    public async Task<string> ResolveAsync(IPAddress address, CancellationToken cancellation = default)
    {
        var raw = address.ToString();
        if (cancellation.IsCancellationRequested) return raw;
        Task<string> task;
        lock (_gate)
        {
            if (_disposed || _lifetime.IsCancellationRequested || _resolver is null) return raw;
            if (_entries.TryGetValue(address, out var cached))
            {
                if (!cached.Task.IsCompleted || cached.Expires > DateTimeOffset.UtcNow)
                {
                    cached.Access = ++_access;
                    task = cached.Task;
                    goto Wait;
                }
                _entries.Remove(address);
            }
            if (_active >= _maximumConcurrent) return raw;
            if (_entries.Count >= _capacity)
            {
                var oldest = _entries.Where(pair => pair.Value.Task.IsCompleted).OrderBy(pair => pair.Value.Access).FirstOrDefault();
                if (oldest.Key is null) return raw;
                _entries.Remove(oldest.Key);
            }
            var entry = new Entry { Access = ++_access };
            _entries.Add(address, entry);
            _active++;
            entry.Task = LookupAsync(address, entry);
            task = entry.Task;
        }
    Wait:
        try { return await task.WaitAsync(cancellation).ConfigureAwait(false); }
        catch (OperationCanceledException) { return raw; }
    }

    private async Task<string> LookupAsync(IPAddress address, Entry entry)
    {
        // Always yield so entry.Task is published before completion is observed under _gate.
        await Task.Yield();
        var ttl = 30u;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            deadline.CancelAfter(_timeoutMilliseconds);
            var bytes = address.GetAddressBytes();
            var reverse = address.AddressFamily == AddressFamily.InterNetwork
                ? string.Join('.', bytes.Reverse()) + ".in-addr.arpa"
                : string.Join('.', Convert.ToHexString(bytes).ToLowerInvariant().Reverse()) + ".ip6.arpa";
            var response = await DnsWire.QueryAsync(_resolver!, reverse, 12, deadline.Token).ConfigureAwait(false);
            if (response.ResponseCode == 0 && !response.Truncated)
            {
                var ptr = response.Answers.FirstOrDefault(r => r.Domain is not null);
                if (ptr is not null)
                {
                    ttl = Math.Min(response.CacheTtl ?? ptr.Ttl, 300u);
                    return ptr.Domain!;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or InvalidDataException or ArgumentException or ObjectDisposedException) { }
        finally
        {
            lock (_gate)
            {
                entry.Expires = DateTimeOffset.UtcNow.AddSeconds(ttl);
                _active--;
            }
        }
        return address.ToString();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
        }
        // Disposal of the CTS is deferred until all owned socket tasks have observed cancellation.
        _ = DrainAndDisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (!_disposed) { _disposed = true; _lifetime.Cancel(); }
        }
        await DrainAndDisposeAsync().ConfigureAwait(false);
    }

    private async Task DrainAndDisposeAsync()
    {
        Task<string>[] tasks;
        lock (_gate) tasks = _entries.Values.Select(e => e.Task).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        lock (_gate) { _entries.Clear(); _lifetime.Dispose(); }
    }
}
