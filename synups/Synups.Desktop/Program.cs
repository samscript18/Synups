using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Synups.Desktop.Diagnostics;
using Synups.Desktop.Integrations;

namespace Synups.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--help" })) { Console.WriteLine(DesktopOptions.Help); return 0; }
        if (args.SequenceEqual(new[] { "--version" })) { Console.WriteLine($"Synups {typeof(Program).Assembly.GetName().Version}; {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}; .NET {Environment.Version}"); return 0; }
        InstanceCoordinator? instance = null;
        InstallationCheck? check = null;
        var platformStarted = false;
        try
        {
            var options = DesktopOptions.Parse(args);
            if (options.VerificationDirectory is not null) { check = new(options); check.PrepareAsync().GetAwaiter().GetResult(); }
            instance = InstanceCoordinator.TryAcquire(options.DataDirectory);
            if (instance is null)
            {
                if (InstanceCoordinator.ActivateExistingAsync(options.DataDirectory).GetAwaiter().GetResult()) return 0;
                instance = InstanceCoordinator.TryAcquire(options.DataDirectory); // The previous owner may have just closed.
                if (instance is null) throw new IOException("Synups already owns this data directory, but activation failed. No second monitor was started. Close the other instance or choose a separate data directory.");
            }
            platformStarted = true;
            var exit = AppBuilder.Configure(() => new App(options, instance, check)).UsePlatformDetect().WithInterFont().LogToTrace()
                .StartWithClassicDesktopLifetime([]);
            return check is null ? exit : Task.Run(() => check.FinishAsync(exit)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Synups startup failed: " + ex.Message);
            if (check is not null) { Task.Run(() => check.RecordStartupFailureAsync(ex)).GetAwaiter().GetResult(); return 1; }
            if (platformStarted) return 1; // A failed/closed platform cannot be initialized a second time in this process.
            return ShowStartupError(ex.Message);
        }
        finally { instance?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private static int ShowStartupError(string message)
    {
        // A WinExe has no console for ordinary users. Preserve visible, actionable startup errors.
        AppBuilder.Configure(() => new StartupErrorApp(message)).UsePlatformDetect().WithInterFont()
            .StartWithClassicDesktopLifetime([]);
        return 1;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    private sealed class StartupErrorApp(string message) : Application
    {
        public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var close = new Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
                var window = new Window { Title = "Synups — startup needs attention", Width = 540, Height = 260,
                    Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children =
                    { new TextBlock { Text = "Synups could not open this workspace", FontSize = 20 },
                      new TextBlock { Text = message + " Existing files were not reset. Check the folder permissions or choose --data-directory with a writable local folder.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, close } } };
                close.Click += (_, _) => desktop.Shutdown(1); desktop.MainWindow = window;
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
