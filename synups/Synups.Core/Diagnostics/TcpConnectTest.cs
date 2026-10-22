using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Synups.Core.Diagnostics;

public sealed class TcpConnectTest : IDiagnosticTest
{
    public DiagnosticKind Kind => DiagnosticKind.TcpConnect;
    public async Task<DiagnosticTestResult> ExecuteAsync(DiagnosticRequest request, IProgress<DiagnosticProgress>? progress, CancellationToken cancellation)
    {
        request = request.ValidatedCopy();
        var utc = DateTimeOffset.UtcNow; var stamp = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(request.Parameters.ProbeTimeoutMilliseconds);
        var observations = new List<DiagnosticObservation>();
        var outcome = DiagnosticOutcome.Failed; string? error = null;
        var resolving = !IPAddress.TryParse(request.Target, out _);
        try
        {
            var addresses = resolving ? await Dns.GetHostAddressesAsync(request.Target, timeout.Token).ConfigureAwait(false) : [IPAddress.Parse(request.Target)];
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            // One explicit port/attempt. Record the exact resolved address used, rather than silently scanning addresses/ports.
            var address = addresses[0]; resolving = false;
            observations.Add(new(DateTimeOffset.UtcNow, "Endpoint", DiagnosticCertainty.Observed, Text: $"{address}:{request.Parameters.Port}"));
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var connect = Stopwatch.GetTimestamp();
            await socket.ConnectAsync(new IPEndPoint(address, request.Parameters.Port), timeout.Token).ConfigureAwait(false);
            observations.Add(new(DateTimeOffset.UtcNow, "TCP handshake", DiagnosticCertainty.Observed, Stopwatch.GetElapsedTime(connect).TotalMilliseconds, "ms", "Connected; application/HTTPS behavior was not tested."));
            outcome = DiagnosticOutcome.Success;
        }
        catch (OperationCanceledException) { outcome = cancellation.IsCancellationRequested ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Timeout; error = "TCP connect/resolution stopped at the configured deadline or user cancellation."; }
        catch (SocketException ex)
        {
            outcome = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => DiagnosticOutcome.Refused,
                SocketError.TimedOut => DiagnosticOutcome.Timeout,
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => DiagnosticOutcome.DnsFailure,
                SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.NetworkDown => DiagnosticOutcome.Unreachable,
                SocketError.AccessDenied => DiagnosticOutcome.PermissionDenied,
                _ => resolving ? DiagnosticOutcome.DnsFailure : DiagnosticOutcome.NetworkError
            }; error = $"{ex.SocketErrorCode}: {ex.Message}";
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException) { outcome = ex is UnauthorizedAccessException ? DiagnosticOutcome.PermissionDenied : DiagnosticOutcome.Unsupported; error = ex.Message; }
        observations.Add(new(DateTimeOffset.UtcNow, "Interpretation", DiagnosticCertainty.Unknown, Text: "A handshake or refusal does not establish application health or firewall policy. Hostname lookup uses the system resolver; its raw DNS status is not exposed by this test."));
        return new(Guid.NewGuid(), Kind, request.Target, utc, DateTimeOffset.UtcNow, outcome, "System hostname resolution (if needed), then one Socket.ConnectAsync TCP handshake; no raw SYN probing",
            Stopwatch.GetElapsedTime(stamp).TotalMilliseconds, observations, error);
    }
}
