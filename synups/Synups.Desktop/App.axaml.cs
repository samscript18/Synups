using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Synups.Desktop;

public partial class App : Application
{
    private readonly Func<MainWindowViewModel> createViewModel;
    public App() : this(() => new MainWindowViewModel()) { }
    public App(Func<MainWindowViewModel> createViewModel) => this.createViewModel = createViewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new MainWindow { DataContext = createViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
