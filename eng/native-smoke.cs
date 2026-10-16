// Verification harness only. Not compiled into Synups production projects.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;

internal static class NativeSmoke
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected a temporary configuration path.");
        var store = new ConfigurationStore(args[0]);
        var config = new MonitorConfiguration(1, [new GroupConfiguration(Guid.NewGuid(), "Explicit loopback smoke", true,
            500, 1000, 500, [new TargetConfiguration(Guid.NewGuid(), "This computer", "127.0.0.1")])]);
        store.SaveAsync(config).GetAwaiter().GetResult();
        var vm = new MainWindowViewModel(new MonitoringService(), store);
        var exitCode = 1;
        async Task VerifyAsync()
        {
            MainWindow? window = null;
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
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"NATIVE_VISIBLE={window!.IsVisible}");
                    Console.WriteLine($"UI_SUMMARY={window.FindControl<TextBlock>("MonitoringSummary")!.Text}");
                    Console.WriteLine($"UI_MEASUREMENT={window.FindControl<TextBlock>("LastMeasurement")!.Text}");
                    Console.WriteLine($"REAL_TARGET={vm.Groups[0].Targets[0].Address}; {vm.Groups[0].Targets[0].Status}; {vm.Groups[0].Targets[0].Details}");
                    window.FindControl<Button>("StopMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                await WaitForAsync(() => !vm.IsRunning && vm.CanStart);
                Console.WriteLine("NATIVE_START_MEASURE_STOP=PASS");
                exitCode = 0;
            }
            catch (Exception ex) { Console.WriteLine($"NATIVE_SMOKE_FAILED={ex}"); }
            finally { await Dispatcher.UIThread.InvokeAsync(() => window?.Close()); }
        }
        AppBuilder.Configure(() => new App(() => vm)).UsePlatformDetect().WithInterFont().LogToTrace()
            .AfterSetup(_builder => { _ = Task.Run(VerifyAsync); })
            .StartWithClassicDesktopLifetime([]);
        return exitCode;
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
