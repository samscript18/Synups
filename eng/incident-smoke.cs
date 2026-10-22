// Engineering verification only; never compiled into a shipped production project.
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;

internal static class IncidentSmoke
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[1] is not ("observe" or "reload")) throw new ArgumentException("Expected output directory and observe/reload stage.");
        var root = Path.GetFullPath(args[0]);
        var store = new ConfigurationStore(Path.Combine(root, "configuration.v1.json"));
        if (args[1] == "observe")
        {
            if (File.Exists(store.FileName)) throw new IOException("Refusing an existing profile.");
            store.SaveAsync(new(1, [new(Guid.NewGuid(), "Isolated real ICMP incident", true, 250, 2000, 200,
                [new(Guid.NewGuid(), "This disposable namespace", "127.0.0.1")])])).GetAwaiter().GetResult();
        }
        var config = store.LoadAsync().GetAwaiter().GetResult().Configuration ?? throw new IOException("Missing profile.");
        if (config.Groups.Count != 1 || config.Groups[0].Targets.Count != 1 || config.Groups[0].Targets[0].Address != "127.0.0.1")
            throw new IOException("Only the explicit isolated loopback profile is permitted.");
        var historyPath = Path.Combine(root, "history.v1.db");
        var engine = new MonitoringService(history: new SqliteHistoryStore(historyPath));
        var vm = new MainWindowViewModel(engine, store, new PreferencesStore(Path.Combine(root, "preferences.v1.json"), Path.Combine(root, "logs")));
        var exit = 1;
        MainWindow? window = null;
        async Task VerifyAsync()
        {
            try
            {
                await UntilAsync(() =>
                {
                    window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
                    return window?.IsVisible == true && vm.CanStart;
                });
                if (args[1] == "reload")
                {
                    var persisted = await engine.ReadArchiveAsync();
                    Require(persisted.Outages.Count == 1 && persisted.Outages[0].RecoveredUtc.HasValue, "Incident was not reloaded recovered.");
                    Require(persisted.Events.Count(e => e.Kind == MonitorEventKind.OutageStarted) == 1 && persisted.Events.Count(e => e.Kind == MonitorEventKind.Recovery) == 1, "Restart duplicated transitions.");
                    Require(!vm.IsRunning && vm.Outages.Count == 1 && persisted.Outages[0].Comment.Contains("namespace", StringComparison.Ordinal), "Reload lost comment/UI state or started monitoring.");
                    await Dispatcher.UIThread.InvokeAsync(() => vm.SelectedPage = 2);
                    await CaptureAsync(window!, Path.Combine(root, "restart-history.png"));
                    await File.WriteAllTextAsync(Path.Combine(root, "restart.json"), JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => window!.FindControl<Button>("StartMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                    await UntilAsync(() => engine.Capture().Groups.Single().Connectivity == GroupConnectivity.Connected);
                    await CaptureAsync(window!, Path.Combine(root, "healthy.png"));
                    await File.WriteAllTextAsync(Path.Combine(root, "healthy.ready"), DateTimeOffset.UtcNow.ToString("O"));
                    await UntilAsync(() => engine.Capture().Groups.Single().Connectivity == GroupConnectivity.Disconnected);
                    Require(engine.Capture().Outages.Count == 0, "First failure skipped the grace period.");
                    var graceObservedUtc = DateTimeOffset.UtcNow;
                    await CaptureAsync(window!, Path.Combine(root, "failure-grace.png"));
                    await UntilAsync(() => engine.Capture().Outages.Count == 1 && vm.Outages.Count == 1);
                    var outage = engine.Capture().Outages.Single();
                    Require(!outage.RecoveredUtc.HasValue && outage.StartedUtc - outage.FirstFailureUtc >= TimeSpan.FromSeconds(2), "Confirmation grace or unresolved state incorrect.");
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        vm.SelectedPage = 2; vm.SelectedOutage = vm.Outages.Single();
                        vm.OutageComment = "Controlled disposable namespace ICMP filter; real packets, not an Internet outage.";
                        await vm.SaveCommentAsync();
                    });
                    Require(vm.AlertBanner.Contains("outage confirmed", StringComparison.OrdinalIgnoreCase), "Actual in-app outage alert missing.");
                    await CaptureAsync(window!, Path.Combine(root, "confirmed-outage.png"));
                    await File.WriteAllTextAsync(Path.Combine(root, "outage.ready"), outage.Id.ToString());
                    await UntilAsync(() => engine.Capture().Outages.Single().RecoveredUtc.HasValue);
                    var recovered = engine.Capture().Outages.Single();
                    Require(recovered.Id == outage.Id && recovered.Elapsed > TimeSpan.Zero && !recovered.ContainsObservationGap, "Recovery identity/duration/confidence incorrect.");
                    Require(Math.Abs(((recovered.RecoveredUtc!.Value - recovered.StartedUtc) - recovered.Elapsed).TotalMilliseconds) < 500, "Monotonic duration differs materially from stable UTC observations.");
                    await Dispatcher.UIThread.InvokeAsync(() => { vm.Refresh(); vm.SelectedPage = 2; });
                    Require(vm.AlertBanner.Contains("connectivity recovered", StringComparison.OrdinalIgnoreCase), "Actual in-app recovery alert missing.");
                    await CaptureAsync(window!, Path.Combine(root, "recovered-history.png"));
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await using var csv = new FileStream(Path.Combine(root, "real-incident.csv"), FileMode.CreateNew, FileAccess.Write);
                        await vm.ExportAsync(csv);
                    });
                    await Dispatcher.UIThread.InvokeAsync(() => window!.FindControl<Button>("StopMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                    await UntilAsync(() => !vm.IsRunning && vm.CanStart);
                    var saved = await engine.ReadArchiveAsync();
                    Require(saved.Outages.Count == 1 && saved.Events.Count(e => e.Kind == MonitorEventKind.OutageStarted) == 1 && saved.Events.Count(e => e.Kind == MonitorEventKind.Recovery) == 1, "Duplicate incident transitions.");
                    await File.WriteAllTextAsync(Path.Combine(root, "incident.json"), JsonSerializer.Serialize(new
                    {
                        source_revision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                        probe = typeof(IcmpProbe).FullName, target = "127.0.0.1", grace_milliseconds = 2000,
                        grace_observed_utc = graceObservedUtc, outage = recovered, saved.Events,
                        actual_native_client = true, physical_interaction = false,
                        explanation = "Real ICMP dropped and restored by a filter confined to a disposable network namespace. Not an Internet outage."
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
                exit = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Console.Error.WriteLine(JsonSerializer.Serialize(engine.Capture()));
                if (window?.IsVisible == true) await CaptureAsync(window, Path.Combine(root, "failure.png"));
            }
            finally { await Dispatcher.UIThread.InvokeAsync(() => window?.Close()); }
        }
        AppBuilder.Configure(() => new App(() => vm)).UsePlatformDetect().WithInterFont().LogToTrace()
            .AfterSetup(builder => { _ = Task.Run(VerifyAsync); }).StartWithClassicDesktopLifetime([]);
        return exit;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!await Dispatcher.UIThread.InvokeAsync(condition))
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Real incident stage did not reach the expected observation.");
            await Task.Delay(50);
        }
    }
    private static async Task CaptureAsync(Window window, string path)
    {
        await Task.Delay(200);
        var bytes = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
            bitmap.Render(window); using var memory = new MemoryStream(); bitmap.Save(memory, PngBitmapEncoderOptions.Default); return memory.ToArray();
        });
        await File.WriteAllBytesAsync(path, bytes);
    }
}
