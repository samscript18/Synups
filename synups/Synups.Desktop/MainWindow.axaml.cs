using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Synups.Core.Alerts;
using Synups.Core.Persistence;
using Synups.Desktop.Integrations;

namespace Synups.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Task pendingAction = Task.CompletedTask;
    private bool closing, closedCleanly, initialized;
    private TrayController? tray;
    private WindowPlacement? normalPlacement;
    internal bool IsClosingPending => closing;
    public bool StartMinimized { get; init; }
    public Func<Task<bool>>? ExitConfirmation { get; set; }
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        InitializeDesktopActions();
        Opened += async (_, _) => await RunAsync(async () =>
        {
            if (initialized) return;
            initialized = true;
            if (ViewModel is not { } vm) return;
            await vm.RefreshLoginAsync();
            await vm.InitializeAsync();
            tray = new TrayController(RestoreFromTray, Close);
            this.FindControl<TextBlock>("TrayCapability")!.Text = tray.Status;
            ApplyDesktopPreferences(restorePlacement: true);
            if (DesktopPolicy.StartOnLaunch(vm.SavedPreferences, vm.CanStart && vm.PreferencesError is null)) await vm.StartAsync();
            if (StartMinimized || vm.SavedPreferences.LaunchMinimized) WindowState = WindowState.Minimized;
        });
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized &&
                ViewModel is { } vm && DesktopPolicy.HideOnMinimize(vm.SavedPreferences, tray?.Restorable == true)) Hide();
        };
        PositionChanged += (_, _) => RememberNormalPlacement();
        SizeChanged += (_, _) => { Classes.Set("compact", Bounds.Width < 1060); RememberNormalPlacement(); };
        refreshTimer.Tick += (_, _) => { ViewModel?.Refresh(); if (ViewModel?.IsRunning != true) refreshTimer.Stop(); };
        Closed += (_, _) => { refreshTimer.Stop(); tray?.Dispose(); };
    }
    public void RestoreFromTray()
    {
        // Showing a still-minimized hidden NSWindow can enqueue another minimize
        // callback after restoration. Normalize first, then show and activate.
        WindowState = WindowState.Normal; Show(); Activate();
    }
    private void RememberNormalPlacement()
    {
        if (WindowState == WindowState.Normal && double.IsFinite(Bounds.Width) && Bounds.Width >= 300 && Bounds.Height >= 200)
            normalPlacement = new(Bounds.Width, Bounds.Height, Position.X, Position.Y, false);
    }
    private void ApplyDesktopPreferences(bool restorePlacement = false)
    {
        if (ViewModel is not { } vm) return;
        Topmost = vm.SavedPreferences.AlwaysOnTop;
        if (restorePlacement)
        {
            // Windows can clamp the initial native client to the work area
            // while Width/Height still contain the larger requested defaults.
            // Normalize fresh windows too, so their first layout fits the client.
            var saved = vm.SavedPreferences.Placement ?? new WindowPlacement(
                ClientSize.Width, ClientSize.Height, Position.X, Position.Y, false);
            var areas = Screens.All.Select(s => new WorkArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling)).ToArray();
            var safe = DesktopPolicy.Clamp(saved, areas);
            MinWidth = Math.Min(MinWidth, safe.Width); MinHeight = Math.Min(MinHeight, safe.Height);
            Width = safe.Width; Height = safe.Height; Position = new PixelPoint(safe.X, safe.Y);
            normalPlacement = safe;
            if (safe.Maximized) WindowState = WindowState.Maximized;
        }
        RememberNormalPlacement();
    }
    private async void EnableLoginClicked(object? sender, RoutedEventArgs e)
    {
        if(await ConfirmAsync("Enable launch at login", "Register this exact Synups executable and workspace for your next graphical login, minimized? No elevation or immediate launch. Monitoring only starts if its separate launch preference was explicitly enabled. Move/remove the app only after removing this registration."))
            await RunAsync(()=>ViewModel?.ChangeLoginAsync(true,true)??Task.CompletedTask);
    }
    private async void RemoveLoginClicked(object? sender, RoutedEventArgs e)
    {
        if(await ConfirmAsync("Remove launch at login", "Remove only this workspace's exact Synups startup entry? Existing configuration/history and unrelated startup entries stay unchanged."))
            await RunAsync(()=>ViewModel?.ChangeLoginAsync(false,true)??Task.CompletedTask);
    }
    private async void ApplyPreferencesClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && int.TryParse(vm.PrefRetentionDays, out var days) && days > 0 &&
            !await ConfirmAsync("Enable automatic history cleanup", $"Delete completed, recovered sessions older than {days} days now, at launch and once daily while monitoring? Unresolved or interrupted sessions are kept. This also deletes eligible events/comments. Logs and backups are kept separately. Set retention to 0 to disable cleanup.")) return;
        await RunAsync(async () => { if (ViewModel is { } model) await model.SavePreferencesAsync(confirmRetention: true); ApplyDesktopPreferences(); });
    }
    private async void RecoverPreferencesClicked(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Recover preferences", "Back up the unreadable preferences file and save the values currently shown? Retention, if enabled, deletes eligible old sessions."))
            await RunAsync(async () => { if (ViewModel is { } vm) await vm.SavePreferencesAsync(recover: true, confirmRetention: true); ApplyDesktopPreferences(); });
    }
    private async void ApplyCapacityClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.SaveCapacityAsync() ?? Task.CompletedTask);
    private async void RefreshStorageClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.RefreshStorageAsync(force: true) ?? Task.CompletedTask);
    private async void BackupHistoryClicked(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Back up history", "Create a verified, complete SQLite history snapshot in the backup folder shown in Settings? This needs additional disk space. Existing backups are kept. Configuration, preferences and automatic logs are separate files."))
            await RunAsync(() => ViewModel?.BackupHistoryAsync(confirmed: true) ?? Task.CompletedTask);
    }
    private async void CleanupHistoryClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (!int.TryParse(vm.CleanupDays, out var days) || days is < 1 or > 3650) { vm.ReportError("Cleanup age must be 1–3650 days."); return; }
        if (await ConfirmAsync("Clean history", $"First create a full SQLite backup, then delete sessions ended more than {days} days ago and their events, recovered outages and comments? Entire unresolved/interrupted sessions are kept. Freed pages are reused; the file is not shrunk. Automatic logs and backups remain. Cleanup is cancelled if backup fails."))
            await RunAsync(() => vm.CleanupHistoryAsync(confirmed: true));
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (closing || !pendingAction.IsCompleted) return;
        var workspace = this.FindControl<Grid>("Workspace")!;
        var priorFocus=FocusManager?.GetFocusedElement() as Control;
        workspace.IsEnabled = false;
        try { pendingAction = ExecuteSafelyAsync(operation); await pendingAction; }
        finally { workspace.IsEnabled = true; if(priorFocus is { IsEffectivelyVisible:true, IsEffectivelyEnabled:true }) priorFocus.Focus(Avalonia.Input.NavigationMethod.Tab); }
        if (ViewModel?.IsRunning == true) refreshTimer.Start();
    }
    private async Task ExecuteSafelyAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception ex) { ViewModel?.ReportError(ex.Message); }
    }
    private void TestSoundClicked(object? sender, RoutedEventArgs e) => ViewModel?.TestAlertSound();
    private async void ChooseSoundClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose optional local alert WAV", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("WAV sound") { Patterns = ["*.wav"] }] });
        if (files.Count == 1 && ViewModel is { } vm) vm.PrefSoundFile = files[0].TryGetLocalPath() ?? throw new IOException("Choose a local WAV file.");
    });
    private async void PreviewLegacyImportClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Preview original WirePeep settings", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("WirePeep settings") { Patterns = ["*.stgx"] }],
        });
        if (files.Count == 1 && ViewModel is { } vm)
        {
            var path = files[0].TryGetLocalPath() ?? throw new IOException("Choose a local settings file; remote providers are unsupported.");
            await vm.PreviewLegacyImportAsync(path);
        }
    });
    private async void ImportLegacyClicked(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Import WirePeep profile", "Replace all current Synups targets/groups with the previewed original IDs and timings? The current configuration will be backed up first. History, preferences, startup registration and the original file remain unchanged. No monitoring starts."))
            await RunAsync(() => ViewModel?.ImportLegacyAsync(true) ?? Task.CompletedTask);
    }
    private async void RetryHistoryClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.RetryHistoryAsync() ?? Task.CompletedTask);
    private async void LatestIncidentsClicked(object? sender, RoutedEventArgs e)
    { if (ViewModel is { } vm) await vm.LatestIncidentsAsync(); }
    private async void OlderIncidentsClicked(object? sender, RoutedEventArgs e)
    { if (ViewModel is { } vm) await vm.OlderIncidentsAsync(); }
    private async void StartClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.StartAsync() ?? Task.CompletedTask);
    private async void CreateFirstTargetClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.CreateFirstTargetAsync() ?? Task.CompletedTask);
    private async void StopClicked(object? sender, RoutedEventArgs e) => await RunAsync(() => ViewModel?.StopAsync() ?? Task.CompletedTask);
    private void NavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (this.FindControl<ScrollViewer>("PageScroll") is { } page) page.Offset = default;
    }
    private void ConfigureClicked(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.SelectedPage = 1; }
    private void NewGroupClicked(object? sender, RoutedEventArgs e) => ViewModel?.NewGroup();
    private void NewTargetClicked(object? sender, RoutedEventArgs e) => ViewModel?.NewTarget();
    private async void DiscoverGatewayClicked(object? sender, RoutedEventArgs e)
    { if (ViewModel is { } vm) await vm.DiscoverGatewaysAsync(); }
    private void CancelDiscoveryClicked(object? sender, RoutedEventArgs e) => ViewModel?.CancelDiscovery();
    private async void AcceptGatewayClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedGateway: { } gateway } vm && await ConfirmAsync("Add discovered gateway", $"Add {gateway.Address} on {gateway.InterfaceName} to your Synups profile? Existing targets stay unchanged. Monitoring will not start. This gateway is not proof of Internet availability."))
            await RunAsync(() => vm.AcceptGatewayAsync(confirmed: true));
    }
    private async void CheckLegacyCandidatesClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedLegacyRule: { } rule } vm && await ConfirmAsync("Check explicit legacy candidates", $"Send ICMP to {string.Join(", ",rule.Addresses)} in order, stopping at the first response? Timeout {rule.TimeoutMilliseconds}ms per request, total deadline 10s. These are candidate addresses, not verified modem/transceiver identities. No target or monitoring session will be created."))
            await vm.CheckLegacyCandidatesAsync(confirmed:true);
    }
    private async void AcceptLegacyCandidateClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedLegacyRule: { } rule } vm && await ConfirmAsync("Add confirmed candidate", $"Add the responding address to {rule.GroupName} and enable that group? Existing targets/IDs are kept. A response does not establish device identity or Internet availability. Monitoring stays stopped."))
            await RunAsync(()=>vm.AcceptLegacyCandidateAsync(confirmed:true));
    }
    private async void SetupOriginalRouterClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedGateway: { } gateway } vm && await ConfirmAsync("Set up original router group", $"Add {gateway.Address} to Local Router and enable that group? Existing targets/IDs stay. Monitoring will not start."))
            await RunAsync(()=>vm.AddGatewayToOriginalRouterAsync(confirmed:true));
    }
    private async void AddMissingDefaultsClicked(object? sender, RoutedEventArgs e)
    {
        if(await ConfirmAsync("Add missing original groups", "Add original groups whose names are missing, with their original literal IPs/timings? Existing groups/targets/IDs are preserved. Discovery-only groups stay disabled until confirmed setup; no probes or monitoring will start."))
            await RunAsync(()=>ViewModel?.AddMissingDefaultsAsync(confirmed:true)??Task.CompletedTask);
    }
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
        this.FindControl<Button>("ExportOutages")!.Focus(Avalonia.Input.NavigationMethod.Tab);
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
            if (ViewModel is { } vm)
            {
                if (vm.SavedPreferences.ConfirmExit && !IsVisible) RestoreFromTray();
                if (vm.SavedPreferences.ConfirmExit && !(await (ExitConfirmation?.Invoke() ?? ConfirmAsync("Exit Synups", "Stop monitoring, save evidence and exit Synups? Unresolved incidents will remain unresolved."))))
                { closing = false; return; }
                RememberNormalPlacement();
                if (normalPlacement is { } placement) await vm.SavePlacementAsync(placement with { Maximized = WindowState == WindowState.Maximized });
                await vm.DisposeAsync();
            }
            refreshTimer.Stop(); closedCleanly = true; Close();
        }
        catch (Exception ex)
        {
            closing = false;
            if (!IsVisible) RestoreFromTray();
            ViewModel?.Refresh(); ViewModel?.ReportError($"Could not finish shutdown: {ex.Message}");
            refreshTimer.Start();
        }
    }

    private async Task<bool> ConfirmAsync(string title, string text)
    {
        var dialog = new Window { Title = title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var accept = new Button { Name = "ConfirmContinue", Content = "Continue" };
        var cancel = new Button { Name = "ConfirmCancel", Content = "Cancel", IsCancel = true, IsDefault = true };
        accept.Click += (_, _) => dialog.Close(true); cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children =
        {
            new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { cancel, accept } },
        } };
        var priorFocus = FocusManager?.GetFocusedElement();
        dialog.Opened += (_, _) => cancel.Focus(Avalonia.Input.NavigationMethod.Tab);
        try { return await dialog.ShowDialog<bool>(this); }
        catch (Exception ex) { ViewModel?.ReportError(ex.Message); return false; }
        finally
        {
            if(priorFocus?.Focus(Avalonia.Input.NavigationMethod.Tab)!=true)
                Dispatcher.UIThread.Post(()=>{ if(IsVisible && priorFocus is Control { IsEffectivelyVisible:true, IsEffectivelyEnabled:true }) priorFocus.Focus(Avalonia.Input.NavigationMethod.Tab); },DispatcherPriority.Input);
        }
    }
}
