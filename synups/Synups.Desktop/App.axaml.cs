using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Synups.Desktop.Diagnostics;
using Synups.Desktop.Integrations;

namespace Synups.Desktop;

public partial class App : Application
{
    private readonly Func<MainWindowViewModel> createViewModel;
    private readonly InstanceCoordinator? instance;
    private readonly InstallationCheck? installationCheck;
    private int activationQueued;
    public App() : this(() => new MainWindowViewModel()) { }
    public App(Func<MainWindowViewModel> createViewModel) => this.createViewModel = createViewModel;
    public App(DesktopOptions options, InstanceCoordinator instance, InstallationCheck? installationCheck)
        : this(() => new MainWindowViewModel(options.DataDirectory))
    { this.instance = instance; this.installationCheck = installationCheck; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var window = new MainWindow { DataContext = createViewModel() };
            desktop.MainWindow = window;
            instance?.Listen(() =>
            {
                if (Interlocked.Exchange(ref activationQueued, 1) != 0) return;
                Dispatcher.UIThread.Post(() =>
                {
                    try { if (!window.IsClosingPending) window.RestoreFromTray(); }
                    finally { Volatile.Write(ref activationQueued, 0); }
                });
            });
            if (installationCheck is not null)
                window.Opened += (_, _) => Dispatcher.UIThread.Post(async () => await installationCheck.RunAsync(window, desktop, instance!));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
