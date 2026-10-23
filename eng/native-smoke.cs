// Verification harness only. Not compiled into Synups production projects.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;

internal static class NativeSmoke
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected a verification configuration path.");
        var store = new ConfigurationStore(args[0]);
        var config = new MonitorConfiguration(1, [new GroupConfiguration(Guid.NewGuid(), "Explicit loopback smoke", true,
            500, 1000, 500, [new TargetConfiguration(Guid.NewGuid(), "This computer", "127.0.0.1")])]);
        if (!File.Exists(args[0])) store.SaveAsync(config).GetAwaiter().GetResult();
        var saved = store.LoadAsync().GetAwaiter().GetResult().Configuration;
        if (saved is null || saved.Groups.SelectMany(g => g.Targets).Any(t => t.Address != "127.0.0.1"))
            throw new InvalidOperationException("Smoke harness only permits its explicit loopback profile.");
        var historyPath = args[0] + ".history.db";
        var preferences = new PreferencesStore(args[0] + ".preferences.json", args[0] + ".logs");
        if (!File.Exists(args[0] + ".preferences.json")) preferences.SaveAsync(new() { MinimizeToTray = true, LogDirectory = args[0] + ".logs" }).GetAwaiter().GetResult();
        var vm = new MainWindowViewModel(new MonitoringService(history: new SqliteHistoryStore(historyPath)), store, preferences);
        var exitCode = 1;
        async Task VerifyAsync()
        {
            MainWindow? window = null;
            Size initialSize = default;
            try
            {
                await WaitForAsync(() =>
                {
                    window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
                    return window?.IsVisible == true && vm.CanStart;
                });
                await Dispatcher.UIThread.InvokeAsync(() => window!.FindControl<Button>("StartMonitoring")!
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                await WaitForAsync(() => vm.Groups[0].Targets[0].Status.StartsWith("Responded", StringComparison.Ordinal));
                var observeSeconds = int.Parse(Environment.GetEnvironmentVariable("SYNUPS_SMOKE_SECONDS") ?? "1");
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(observeSeconds, 1, 300)));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"NATIVE_VISIBLE={window!.IsVisible}");
                    Console.WriteLine($"UI_SUMMARY={window.FindControl<TextBlock>("MonitoringSummary")!.Text}");
                    Console.WriteLine($"UI_MEASUREMENT={window.FindControl<TextBlock>("LastMeasurement")!.Text}");
                    Console.WriteLine($"REAL_TARGET={vm.Groups[0].Targets[0].Address}; {vm.Groups[0].Targets[0].Status}; {vm.Groups[0].Targets[0].Details}");
                    Console.WriteLine($"TRAY_STATUS={window.FindControl<TextBlock>("TrayCapability")!.Text}");
                    if (Environment.GetEnvironmentVariable("SYNUPS_SCREENSHOT_DIR") is { Length: > 0 } captureDirectory)
                    {
                        Directory.CreateDirectory(captureDirectory);
                        Capture(window, Path.Combine(captureDirectory, "synups-overview.png"));
                        initialSize = window.ClientSize;
                        window.Width = 820; window.Height = 560;
                    }
                });
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Environment.GetEnvironmentVariable("SYNUPS_SCREENSHOT_DIR") is { Length: > 0 } captureDirectory)
                    {
                        Capture(window!, Path.Combine(captureDirectory, "synups-overview-compact.png"));
                        window!.Width = initialSize.Width; window.Height = initialSize.Height;
                        vm.SelectedPage = 1;
                    }
                });
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Environment.GetEnvironmentVariable("SYNUPS_SCREENSHOT_DIR") is { Length: > 0 } captureDirectory)
                        window!.FindControl<ListBox>("TargetList")!.BringIntoView();
                });
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Environment.GetEnvironmentVariable("SYNUPS_SCREENSHOT_DIR") is { Length: > 0 } captureDirectory)
                        Capture(window!, Path.Combine(captureDirectory, "synups-targets.png"));
                    window!.WindowState = WindowState.Minimized;
                    Console.WriteLine($"MINIMIZED_VISIBLE={window.IsVisible}");
                    window.RestoreFromTray();
                    if (!window.IsVisible) throw new InvalidOperationException("Window did not restore.");
                    window.FindControl<Button>("StopMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                await WaitForAsync(() => !vm.IsRunning && vm.CanStart);
                await Dispatcher.UIThread.InvokeAsync(() => vm.SelectedPage = 3);
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() => window!.FindControl<TextBlock>("HistoryStorageHeading")!.BringIntoView());
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Environment.GetEnvironmentVariable("SYNUPS_SCREENSHOT_DIR") is { Length: > 0 } captureDirectory)
                        Capture(window!, Path.Combine(captureDirectory, "synups-storage.png"));
                    Console.WriteLine($"STORAGE_SUMMARY={vm.StorageSummary}");
                });
                Console.WriteLine("NATIVE_START_MEASURE_STOP=PASS");
                exitCode = 0;
            }
            catch (Exception ex) { Console.WriteLine($"NATIVE_SMOKE_FAILED={ex}"); }
            finally { await Dispatcher.UIThread.InvokeAsync(() => window?.Close()); }
        }
        AppBuilder.Configure(() => new App(() => vm)).UsePlatformDetect().WithInterFont().LogToTrace()
            .AfterSetup(_builder => { _ = Task.Run(VerifyAsync); })
            .StartWithClassicDesktopLifetime([]);
        using (var verifier = new StoreScope(historyPath))
        {
            var archive = verifier.Store.LoadAsync(includeAllEvents: true).GetAwaiter().GetResult();
            if (archive.Sessions.Count == 0 || archive.Sessions.Any(s => s.EndedUtc is null || s.Interrupted)) return 1;
            Console.WriteLine($"DURABLE_CLEAN_SESSIONS={archive.Sessions.Count}; EVENTS={archive.Events.Count}");
            Console.WriteLine($"LAST_SESSION_OBSERVED_SECONDS={archive.Sessions.Last().Elapsed.TotalSeconds:F3}");
            var usage = verifier.Store.GetStorageUsageAsync(args[0] + ".logs").GetAwaiter().GetResult();
            if (usage.ConfiguredBytes != HistoryCapacity.DefaultBytes || usage.EffectiveLimitBytes != HistoryCapacity.DefaultBytes) return 1;
            Console.WriteLine($"SQLITE_CONFIGURED_BYTES={usage.ConfiguredBytes}; EFFECTIVE_LIMIT_BYTES={usage.EffectiveLimitBytes}; PAGE_SIZE={usage.PageSize}");
        }
        return exitCode;
    }

    private sealed class StoreScope(string path) : IDisposable
    {
        public SqliteHistoryStore Store { get; } = new(path);
        public void Dispose() => Store.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    private static void Capture(Window window, string path)
    {
        // Real native control tree/client area. No fabricated values; no OS chrome or desktop capture claimed.
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
        bitmap.Render(window); bitmap.Save(path, PngBitmapEncoderOptions.Default);
        Console.WriteLine($"NATIVE_CLIENT_CAPTURE={path}");
    }

    private static async Task WaitForAsync(Func<bool> check)
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (!await Dispatcher.UIThread.InvokeAsync(check))
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Native UI condition did not complete.");
            await Task.Delay(50);
        }
    }
}
