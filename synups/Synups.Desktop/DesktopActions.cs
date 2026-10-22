using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Synups.Desktop;

public partial class MainWindow
{
    private MainWindowViewModel? focusOwner;
    private void InitializeDesktopActions()
    {
        void Rebind()
        {
            if (focusOwner is not null) focusOwner.FocusRequested -= FocusRequested;
            focusOwner = ViewModel;
            if (focusOwner is not null) focusOwner.FocusRequested += FocusRequested;
        }
        DataContextChanged += (_, _) => Rebind(); Rebind();
        Closed += (_, _) => { if (focusOwner is not null) focusOwner.FocusRequested -= FocusRequested; focusOwner = null; };
    }
    private void FocusRequested(object? sender, EventArgs e)
    { if (!closing && sender == ViewModel) RestoreFromTray(); }
    private Task CopyAsync(string? text, string label) => RunAsync(async () =>
    {
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException($"Select a {label} first.");
        var clipboard = Clipboard ?? throw new PlatformNotSupportedException("Clipboard unavailable in this desktop session.");
        await clipboard.SetTextAsync(text);
        ViewModel?.ReportInformation($"Copied {label} to the clipboard.");
    });
    private async void CopyTargetClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedTarget?.Address, "target IP");
    private async void CopyIncidentClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedOutage?.Details, "incident details");
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (closing || ViewModel is not { } vm) { base.OnKeyDown(e); return; }
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var page = e.Key switch { Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4, _ => -1 };
        if (e.KeyModifiers == command && page >= 0) { vm.SelectedPage = page; e.Handled = true; }
        else if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None) { vm.SelectedPage = 4; e.Handled = true; }
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        { e.Handled = true; if (vm.CanStart) await RunAsync(vm.StartAsync); else vm.ReportInformation(vm.StartGuidance); }
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.Shift)
        { e.Handled = true; if (vm.CanStop) await RunAsync(vm.StopAsync); }
        else if (e.Key == Key.Escape && vm.IsDiscovering) { vm.CancelDiscovery(); e.Handled = true; }
        else if (e.KeyModifiers == (command | KeyModifiers.Shift) && e.Key == Key.C)
        { e.Handled = true; await CopyAsync(vm.SelectedTarget?.Address, "target IP"); }
        else if (e.KeyModifiers == (command | KeyModifiers.Shift) && e.Key == Key.I)
        { e.Handled = true; await CopyAsync(vm.SelectedOutage?.Details, "incident details"); }
        else base.OnKeyDown(e);
    }
}
