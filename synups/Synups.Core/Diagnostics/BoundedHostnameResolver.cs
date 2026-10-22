using System.Net;
using System.Net.Sockets;

namespace Synups.Core.Diagnostics;

/// <summary>
/// Caps diagnostic OS resolver work, including operations whose caller has cancelled.
/// Native getaddrinfo may ignore cancellation. Its slot remains owned until actual completion.
/// </summary>
public sealed class BoundedHostnameResolver
{
    public static BoundedHostnameResolver Shared { get; } = new();
    private readonly SemaphoreSlim slots;
    private readonly object gate = new();
    private readonly HashSet<Task<IPAddress[]>> owned = [];
    private readonly Func<string, Task<IPAddress[]>> resolve;
    public BoundedHostnameResolver(int maximumConcurrency = 2, Func<string, Task<IPAddress[]>>? resolve = null)
    {
        if (maximumConcurrency is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        slots = new(maximumConcurrency);
        this.resolve = resolve ?? (host => Dns.GetHostAddressesAsync(host, CancellationToken.None));
    }
    public int ActiveLookups { get { lock (gate) return owned.Count; } }
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (IPAddress.TryParse(host, out var literal)) return [literal];
        Task<IPAddress[]> task;
        lock (gate)
        {
            if (!slots.Wait(0)) throw new SocketException((int)SocketError.TryAgain);
            Task<IPAddress[]>? operation = null;
            operation = Task.Run(async () =>
            {
                lock (gate) { }
                try { return await resolve(host).ConfigureAwait(false); }
                finally { lock (gate) { owned.Remove(operation!); slots.Release(); } }
            });
            owned.Add(operation); task = operation;
            // If the caller cancels first, still observe any eventual native fault and retain ownership until completion.
            _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        return await task.WaitAsync(cancellation).ConfigureAwait(false);
    }
    public async Task DrainAsync(TimeSpan timeout)
    {
        Task[] pending;
        lock (gate) pending = owned.ToArray();
        try { await Task.WhenAll(pending).WaitAsync(timeout).ConfigureAwait(false); }
        catch (TimeoutException) { throw new TimeoutException("An owned system hostname lookup has not drained. Native resolver calls may ignore cancellation; retry shutdown. Literal IP diagnostics remain available when the application is open."); }
        catch (Exception) when (pending.All(t => t.IsCompleted)) { /* Native faults are returned to their original callers or observed above. */ }
    }
}
