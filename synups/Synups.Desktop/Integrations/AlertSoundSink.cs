using System.Diagnostics;
using Synups.Core.Alerts;

namespace Synups.Desktop.Integrations;

/// <summary>Opt-in local WAV playback, owned by the existing bounded alert worker. Never a monitoring data source.</summary>
public sealed class AlertSoundSink : INativeAlertSink
{
    public static string Capability => OperatingSystem.IsWindows() ? "Windows: local WAV through the OS SoundPlayer; an audio device/session is required." :
        OperatingSystem.IsMacOS() ? "macOS: local WAV through afplay; OS volume/output policy applies." :
        OperatingSystem.IsLinux() ? "Linux: local WAV requires /usr/bin/paplay and a working audio server; otherwise in-app alerts remain." : "Alert sounds unsupported; in-app alerts remain.";
    public async Task<NativeAlertResult> SendAsync(AlertMessage message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Process? process = null;
        Task<string>? errors = null, output = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var path = message.Body;
            if (!Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                return new(false, "Choose an absolute local WAV file. No sound was requested.");
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is < 12 or > 2_000_000) return new(false, "WAV missing or outside the 12-byte–2MB limit. In-app alerts remain.");
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                var header = new byte[12]; await file.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(false);
                if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !header.AsSpan(8, 4).SequenceEqual("WAVE"u8))
                    return new(false, "File is not a RIFF/WAVE sound. No playback requested.");
            }
            ProcessStartInfo start;
            if (OperatingSystem.IsWindows())
            {
                start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"));
                start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("$ErrorActionPreference='Stop';$p=New-Object System.Media.SoundPlayer;try{$p.SoundLocation=[Environment]::GetEnvironmentVariable('SYNUPS_ALERT_WAV');$p.Load();$p.PlaySync()}finally{$p.Dispose()}");
                start.Environment["SYNUPS_ALERT_WAV"] = path; // User paths never become PowerShell code.
            }
            else if (OperatingSystem.IsMacOS()) { start = new("/usr/bin/afplay"); start.ArgumentList.Add(path); }
            else if (OperatingSystem.IsLinux()) { start = new("/usr/bin/paplay"); start.ArgumentList.Add("--"); start.ArgumentList.Add(path); }
            else return new(false, Capability);
            start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardError = start.RedirectStandardOutput = true;
            process = Process.Start(start) ?? throw new IOException("Sound playback process could not start.");
            errors = process.StandardError.ReadToEndAsync(deadline.Token); output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); await output.ConfigureAwait(false); var detail = await errors.ConfigureAwait(false);
            return new(process.ExitCode == 0, process.ExitCode == 0 ? "Sound playback process completed. Audible delivery is not verified; OS output/volume policy applies." : $"Sound playback unavailable: {detail}. In-app alert remains.");
        }
        catch (Exception ex) { return new(false, $"Sound unavailable or cancelled: {ex.Message}. In-app alerts remain."); }
        finally
        {
            if (process is not null)
            {
                try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().ConfigureAwait(false); } }
                catch (InvalidOperationException) { }
                finally
                {
                    deadline.Cancel();
                    if (errors is not null && output is not null)
                        try { await Task.WhenAll(errors, output).ConfigureAwait(false); } catch (OperationCanceledException) { } catch (IOException) { }
                    process.Dispose();
                }
            }
        }
    }
}
