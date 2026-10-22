using System.Diagnostics;
using Synups.Core.Monitoring;
using Synups.Desktop.Integrations;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class NativeWindowsDeliveryTests
{
    [Fact]
    public async Task NativeWindowsNotificationDeliveryIsIndependentlyObservedInShellUi()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("SYNUPS_DESKTOP_DELIVERY_TESTS") != "1")
            Assert.Skip("Explicit Windows Shell delivery test only; adapter success alone is insufficient.");
        var root = Path.Combine(Path.GetTempPath(), "Synups-native-notification-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var evidence = Environment.GetEnvironmentVariable("SYNUPS_NOTIFICATION_EVIDENCE") ?? root; Directory.CreateDirectory(evidence);
        var script = Path.Combine(root, "observe.ps1"); var token = "Synups-proof-" + Guid.NewGuid();
        await using (var input = typeof(NativeWindowsDeliveryTests).Assembly.GetManifestResourceStream("Synups.Tests.Fixtures.ObserveWindowsNotification.ps1")!)
        await using (var file = File.Create(script)) await input.CopyToAsync(file, TestContext.Current.CancellationToken);
        var ready = Path.Combine(root, "ready");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-STA"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(script);
        start.Environment["SYNUPS_OBSERVER_READY"] = ready; start.Environment["SYNUPS_OBSERVER_TOKEN"] = token;
        start.Environment["SYNUPS_OBSERVER_REPORT"] = Path.Combine(evidence, "windows-native-delivery.json");
        start.Environment["SYNUPS_OBSERVER_CAPTURE"] = Path.Combine(evidence, "windows-native-delivery.png");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var observer = Process.Start(start)!;
        var error = observer.StandardError.ReadToEndAsync(timeout.Token); var output = observer.StandardOutput.ReadToEndAsync(timeout.Token);
        try
        {
            while (!File.Exists(ready) && !observer.HasExited) await Task.Delay(100, timeout.Token);
            Assert.False(observer.HasExited, "Native Shell observer failed before readiness: " + (observer.HasExited ? await error : ""));
            var submitted = await new NativeAlertSink().SendAsync(new(Guid.NewGuid(), "Synups explicit native notification verification", token, MonitorEventKind.ProbeError), timeout.Token);
            await observer.WaitForExitAsync(timeout.Token); await output;
            Assert.True(submitted.RequestAccepted, submitted.Status);
            Assert.True(observer.ExitCode == 0, "Independent OS delivery was not observed. " + await error);
        }
        finally
        {
            if (!observer.HasExited) { observer.Kill(true); await observer.WaitForExitAsync(TestContext.Current.CancellationToken); }
            await Task.WhenAll(error, output); Directory.Delete(root, true);
        }
    }
}
