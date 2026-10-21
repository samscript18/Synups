using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Synups.Desktop.Integrations;

/// <summary>One desktop per data directory. Exclusive lease protects startup; a user-only pipe forwards activation.</summary>
public sealed class InstanceCoordinator : IAsyncDisposable
{
    private readonly FileStream lease;
    private readonly string pipeName;
    private readonly CancellationTokenSource stop = new();
    private Task? listener;
    private int activationCount;
    public int ActivationCount => Volatile.Read(ref activationCount);
    private InstanceCoordinator(FileStream lease, string directory) { this.lease = lease; pipeName = Name(directory); }
    private static string Name(string directory)
    {
        var canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (OperatingSystem.IsWindows()) canonical = canonical.ToUpperInvariant();
        return "Synups-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
    }
    public static InstanceCoordinator? TryAcquire(string directory)
    {
        Directory.CreateDirectory(directory);
        try { return new(new FileStream(Path.Combine(directory, "desktop.instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), directory); }
        catch (IOException) { return null; }
    }
    public void Listen(Action activate)
    {
        if (listener is not null) throw new InvalidOperationException("Activation listener already started.");
        listener = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); request.CancelAfter(TimeSpan.FromSeconds(2));
                    var buffer = new byte[5]; var count = 0;
                    while (count < buffer.Length)
                    {
                        var read = await server.ReadAsync(buffer.AsMemory(count), request.Token).ConfigureAwait(false);
                        if (read == 0) break; count += read;
                    }
                    if (count == 5 && Encoding.ASCII.GetString(buffer) == "show\n") { Interlocked.Increment(ref activationCount); activate(); }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (OperationCanceledException) { }
                catch (IOException ex) { Console.Error.WriteLine("Synups activation request failed: " + ex.Message); await Task.Delay(100, stop.Token).ConfigureAwait(false); }
            }
        });
    }
    public static async Task<bool> ActivateExistingAsync(string directory, CancellationToken token = default)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", Name(directory), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(3000, token).ConfigureAwait(false);
            await client.WriteAsync(Encoding.ASCII.GetBytes("show\n"), token).ConfigureAwait(false);
            await client.FlushAsync(token).ConfigureAwait(false); return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException) { return false; }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        try { if (listener is not null) await listener.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { lease.Dispose(); stop.Dispose(); }
    }
}
