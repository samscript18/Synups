using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Synups.Core.Monitoring;

public enum ProbeOutcome { NotChecked, Success, Failure, Unavailable }
public enum ProbeError { None, Timeout, Unreachable, PermissionDenied, Unsupported, NetworkUnavailable, Unexpected }
public sealed record ProbeResult(ProbeOutcome Outcome, long? LatencyMilliseconds = null,
    ProbeError Error = ProbeError.None, string? Detail = null);

public interface IConnectivityProbe
{
    // Cancellation is control flow. Implementations must use a finite timeout and drain their operation.
    Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class IcmpProbe : IConnectivityProbe
{
    public async Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (timeout.TotalMilliseconds is < 1 or > 5000)
                return new(ProbeOutcome.Unavailable, Error: ProbeError.Unexpected, Detail: "Timeout must be 1–5000ms.");
            using var ping = new Ping();
            // Empty payload also permits .NET's unprivileged Linux ping-utility fallback.
            // Await the native operation: cancellation can still wait for the finite native timeout on Windows.
            var reply = await ping.SendPingAsync(address, timeout, Array.Empty<byte>(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return MapStatus(reply.Status, reply.RoundtripTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { return MapException(ex); }
    }

    public static ProbeResult MapStatus(IPStatus status, long roundtripMilliseconds) => status switch
    {
        IPStatus.Success => new(ProbeOutcome.Success, roundtripMilliseconds),
        IPStatus.TimedOut => new(ProbeOutcome.Failure, Error: ProbeError.Timeout, Detail: "ICMP reply timed out."),
        _ => new(ProbeOutcome.Failure, Error: ProbeError.Unreachable, Detail: $"ICMP status: {status}"),
    };

    public static ProbeResult MapException(Exception exception)
    {
        var cause = exception;
        while (cause.InnerException is not null) cause = cause.InnerException;
        var error = cause switch
        {
            UnauthorizedAccessException => ProbeError.PermissionDenied,
            SocketException { SocketErrorCode: SocketError.AccessDenied } => ProbeError.PermissionDenied,
            Win32Exception { NativeErrorCode: 5 } when OperatingSystem.IsWindows() => ProbeError.PermissionDenied,
            Win32Exception { NativeErrorCode: 1 or 13 } when !OperatingSystem.IsWindows() => ProbeError.PermissionDenied,
            PlatformNotSupportedException or NotSupportedException => ProbeError.Unsupported,
            SocketException => ProbeError.NetworkUnavailable,
            OperationCanceledException or TimeoutException => ProbeError.Timeout,
            _ => ProbeError.Unexpected,
        };
        return new(ProbeOutcome.Unavailable, Error: error, Detail: $"{error}: {cause.Message}");
    }
}

public interface IMonitorClock
{
    DateTimeOffset UtcNow { get; }
    long Timestamp { get; }
    TimeSpan Elapsed(long start);
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class MonitorClock : IMonitorClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long Timestamp => System.Diagnostics.Stopwatch.GetTimestamp();
    public TimeSpan Elapsed(long start) => System.Diagnostics.Stopwatch.GetElapsedTime(start);
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}
