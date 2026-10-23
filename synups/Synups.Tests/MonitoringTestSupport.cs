using System.Collections.Concurrent;
using System.Net;
using Synups.Core.Monitoring;

namespace Synups.Tests;

internal sealed class ManualClock : IMonitorClock
{
    private readonly object gate = new();
    private long ticks;
    private DateTimeOffset utc = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    private readonly List<(long Due, TaskCompletionSource Signal)> delays = new();
    public DateTimeOffset UtcNow { get { lock (gate) return utc; } }
    public long Timestamp { get { lock (gate) return ticks; } }
    public TimeSpan Elapsed(long start) { lock (gate) return TimeSpan.FromTicks(ticks - start); }
    public int PendingDelays { get { lock (gate) return delays.Count(x => !x.Signal.Task.IsCompleted); } }
    public Task DelayAsync(TimeSpan duration, CancellationToken token)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (duration <= TimeSpan.Zero) return Task.CompletedTask;
            delays.Add((ticks + duration.Ticks, signal));
        }
        return WaitAsync(signal, token);
    }
    private static async Task WaitAsync(TaskCompletionSource signal, CancellationToken token)
    {
        using var registration = token.Register(() => signal.TrySetCanceled(token));
        await signal.Task;
    }
    public void Advance(int milliseconds)
    {
        lock (gate)
        {
            ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
            utc += TimeSpan.FromMilliseconds(milliseconds);
            foreach (var d in delays.Where(d => d.Due <= ticks)) d.Signal.TrySetResult();
            delays.RemoveAll(d => d.Signal.Task.IsCompleted);
        }
    }
    public void JumpUtc(TimeSpan change) { lock (gate) utc += change; }
}

internal sealed class ScriptedProbe(params ProbeResult[] results) : IConnectivityProbe
{
    private readonly ConcurrentQueue<ProbeResult> results = new(results);
    public ConcurrentQueue<string> Addresses { get; } = new();
    public Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Addresses.Enqueue(address.ToString());
        return Task.FromResult(results.TryDequeue(out var result) ? result : new(ProbeOutcome.Success, 3));
    }
}

internal static class TestConfig
{
    public static ProbeResult Ok => new(ProbeOutcome.Success, 3);
    public static ProbeResult Fail => new(ProbeOutcome.Failure, Error: ProbeError.Timeout, Detail: "Timed out");
    public static ProbeResult Error => new(ProbeOutcome.Unavailable, Error: ProbeError.PermissionDenied, Detail: "Denied");
    public static GroupConfiguration Group(int targets = 1, int failMs = 1000) => new(Guid.NewGuid(), "Test", true, 1000, failMs, 200,
        Enumerable.Range(1, targets).Select(i => new TargetConfiguration(Guid.NewGuid(), $"Target {i}", $"127.0.0.{i}")).ToArray());
    public static MonitorConfiguration Config(params GroupConfiguration[] groups) => new(1, groups);
    public static async Task UntilAsync(Func<bool> predicate)
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Test condition did not complete.");
            await Task.Delay(5);
        }
    }
}
