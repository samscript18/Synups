using System.Diagnostics;
using System.Threading.Channels;
using Synups.Core.Alerts;

namespace Synups.Desktop.Integrations;

public sealed record NativeAlertResult(bool RequestAccepted, string Status);
public interface INativeAlertSink { Task<NativeAlertResult> SendAsync(AlertMessage message, CancellationToken token); }
public sealed class NativeAlertSink : INativeAlertSink
{
    public static string Capability => OperatingSystem.IsMacOS() ? "macOS: optional osascript notification requests; OS delivery/permission is not guaranteed." :
        OperatingSystem.IsLinux() ? "Linux: optional notify-send when installed and a notification daemon is available." :
        OperatingSystem.IsWindows() ? "Windows: opt-in native tray notification requests via the built-in Windows desktop APIs; OS delivery remains policy-controlled." : "Native notification requests unsupported; in-app alerts remain.";
    public async Task<NativeAlertResult> SendAsync(AlertMessage message, CancellationToken token)
    {
        ProcessStartInfo start;
        if (OperatingSystem.IsMacOS())
        {
            start = new("/usr/bin/osascript");
            start.ArgumentList.Add("-e"); start.ArgumentList.Add("on run argv\ndisplay notification (item 2 of argv) with title (item 1 of argv)\nend run");
            start.ArgumentList.Add(message.Title); start.ArgumentList.Add(message.Body);
        }
        else if (OperatingSystem.IsLinux())
        {
            start = new("notify-send"); start.ArgumentList.Add("--app-name=Synups"); start.ArgumentList.Add("--");
            start.ArgumentList.Add(message.Title); start.ArgumentList.Add(message.Body);
        }
        else if (OperatingSystem.IsWindows())
        {
            start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"));
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-STA"); start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Windows.Forms;Add-Type -AssemblyName System.Drawing;$n=New-Object System.Windows.Forms.NotifyIcon;$icon=$null;try{$icon=[System.Drawing.Icon]::ExtractAssociatedIcon([Environment]::GetEnvironmentVariable('SYNUPS_NOTIFY_EXE'));$n.Icon=$icon;$n.Text='Synups';$n.Visible=$true;$n.ShowBalloonTip(3000,[Environment]::GetEnvironmentVariable('SYNUPS_NOTIFY_TITLE'),[Environment]::GetEnvironmentVariable('SYNUPS_NOTIFY_BODY'),[System.Windows.Forms.ToolTipIcon]::Info);$w=[Diagnostics.Stopwatch]::StartNew();while($w.ElapsedMilliseconds -lt 3000){[System.Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 50}}finally{$n.Visible=$false;$n.Dispose();if($icon){$icon.Dispose()}}");
            start.Environment["SYNUPS_NOTIFY_EXE"] = Environment.ProcessPath ?? throw new IOException("Application executable unavailable.");
            start.Environment["SYNUPS_NOTIFY_TITLE"] = message.Title;
            start.Environment["SYNUPS_NOTIFY_BODY"] = message.Body;
        }
        else return new(false, Capability);
        start.UseShellExecute = false; start.RedirectStandardError = true; start.RedirectStandardOutput = true; start.CreateNoWindow = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(OperatingSystem.IsWindows() ? 15 : 3));
        using var process = new Process { StartInfo = start };
        Task<string>? stderr = null, stdout = null;
        try
        {
            process.Start();
            stderr = process.StandardError.ReadToEndAsync(timeout.Token); stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await stdout.ConfigureAwait(false);
            var detail = await stderr.ConfigureAwait(false);
            return new(process.ExitCode == 0, process.ExitCode == 0 ? "Native request submitted; actual delivery is controlled by the OS." : $"Native notification failed: {detail}");
        }
        catch (Exception ex) { return new(false, $"Native notification unavailable: {ex.Message}. In-app alert remains visible."); }
        finally
        {
            try { if (process.Id > 0 && !process.HasExited) { process.Kill(true); await process.WaitForExitAsync().ConfigureAwait(false); } }
            catch (InvalidOperationException) { }
            finally
            {
                timeout.Cancel();
                try { if (stderr is not null && stdout is not null) await Task.WhenAll(stderr, stdout).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
        }
    }
}

public sealed class AlertDelivery : IAsyncDisposable
{
    private readonly Channel<AlertMessage> queue = Channel.CreateBounded<AlertMessage>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task worker;
    private string status = "Native notifications are opt-in. In-app alerts are separate.";
    public string Status => Volatile.Read(ref status);
    public AlertDelivery(INativeAlertSink? sink = null, string? initialStatus = null)
    {
        if (initialStatus is not null) status = initialStatus;
        sink ??= new NativeAlertSink();
        worker = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in queue.Reader.ReadAllAsync(cancellation.Token).ConfigureAwait(false))
                {
                    var result = await sink.SendAsync(message, cancellation.Token).ConfigureAwait(false);
                    Volatile.Write(ref status, result.Status);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { Volatile.Write(ref status, $"Native adapter failed: {ex.Message}; in-app fallback remains active."); }
        });
    }
    public void Enqueue(AlertMessage message)
    { if (!queue.Writer.TryWrite(message)) Volatile.Write(ref status, "Native alert queue full; alerts remain available in-app and in history."); }
    public async ValueTask DisposeAsync()
    { queue.Writer.TryComplete(); cancellation.Cancel(); await worker.ConfigureAwait(false); cancellation.Dispose(); }
}
