using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Synups.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Task pendingAction = Task.CompletedTask;
    private bool closing, closedCleanly;
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await RunAsync(() => ViewModel?.InitializeAsync() ?? Task.CompletedTask);
        refreshTimer.Tick += (_, _) => { ViewModel?.Refresh(); if (ViewModel?.IsRunning != true) refreshTimer.Stop(); };
        Closed += (_, _) => refreshTimer.Stop();
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (closing || !pendingAction.IsCompleted) return;
        pendingAction = ExecuteSafelyAsync(operation);
        await pendingAction;
        if (ViewModel?.IsRunning == true) refreshTimer.Start();
    }
    private async Task ExecuteSafelyAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception ex) { ViewModel?.ReportError(ex.Message); }
    }
    private async void RetryHistoryClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.RetryHistoryAsync() ?? Task.CompletedTask);
    private async void StartClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.StartAsync() ?? Task.CompletedTask);
    private async void StopClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.StopAsync() ?? Task.CompletedTask);
    private void ConfigureClicked(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.SelectedPage = 1; }
    private void NewGroupClicked(object? sender, RoutedEventArgs e) => ViewModel?.NewGroup();
    private void NewTargetClicked(object? sender, RoutedEventArgs e) => ViewModel?.NewTarget();
    private async void ApplyGroupClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.ApplyGroupAsync() ?? Task.CompletedTask);
    private async void ApplyTargetClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.ApplyTargetAsync() ?? Task.CompletedTask);
    private async void ReloadClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.InitializeAsync() ?? Task.CompletedTask);
    private async void SaveCommentClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.SaveCommentAsync() ?? Task.CompletedTask);
    private async void RemoveGroupClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedGroup is not null && await ConfirmAsync("Remove group", "Remove the selected group and all its targets from Synups?"))
            await RunAsync(() => ViewModel.RemoveGroupAsync());
    }
    private async void RemoveTargetClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedTarget is not null && await ConfirmAsync("Remove target", "Remove the selected target? Removing the last target disables its group."))
            await RunAsync(() => ViewModel.RemoveTargetAsync());
    }
    private async void RestoreClicked(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Restore defaults", "Replace the Synups profile with the original literal-IP defaults? Local discovery will not run. Original WirePeep settings are untouched."))
            await RunAsync(() => ViewModel?.RestoreDefaultsAsync() ?? Task.CompletedTask);
    }
    private async void RecoverClicked(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Recover configuration", "Back up the unreadable Synups configuration and replace it with the original literal defaults? The backup stays alongside the configuration file."))
            await RunAsync(() => ViewModel?.RestoreDefaultsAsync(recoverInvalid: true) ?? Task.CompletedTask);
    }
    private async void ExportClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Synups outages", SuggestedFileName = $"Synups-outages-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv", FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek) stream.SetLength(0);
        if (ViewModel is { } vm) await vm.ExportAsync(stream);
    });

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (closedCleanly) { base.OnClosing(e); return; }
        e.Cancel = true; base.OnClosing(e);
        if (closing) return;
        closing = true;
        try
        {
            await pendingAction;
            if (ViewModel is { } vm) await vm.DisposeAsync();
            refreshTimer.Stop(); closedCleanly = true; Close();
        }
        catch (Exception ex)
        {
            closing = false; ViewModel?.ReportError($"Could not finish shutdown: {ex.Message}");
            refreshTimer.Start();
        }
    }

    private async Task<bool> ConfirmAsync(string title, string text)
    {
        var dialog = new Window { Title = title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var accept = new Button { Content = "Continue" };
        var cancel = new Button { Content = "Cancel", IsCancel = true, IsDefault = true };
        accept.Click += (_, _) => dialog.Close(true); cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children =
        {
            new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { cancel, accept } },
        } };
        try { return await dialog.ShowDialog<bool>(this); }
        catch (Exception ex) { ViewModel?.ReportError(ex.Message); return false; }
    }
}
