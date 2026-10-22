using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
        foreach (var name in new[] { "TargetList", "IncidentTimeline" })
            this.FindControl<ListBox>(name)!.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (e.Source is Control control && control.FindAncestorOfType<ListBoxItem>() is { } item &&
                    item.FindAncestorOfType<ListBox>() is { } list) list.SelectedItem = item.DataContext;
            }, RoutingStrategies.Tunnel);
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
    private async void CopyTargetValueClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedTargetValue, "target value");
    private async void CopyTargetRowClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedTargetRow, "target row");
    private async void CopyIncidentValueClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedIncidentValue, "incident value");
    private async void CopyIncidentRowClicked(object? sender, RoutedEventArgs e) => await CopyAsync(ViewModel?.SelectedIncidentRow, "incident row");
    private void UnselectTargetClicked(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.SelectedTarget = null; }
    private void UnselectIncidentClicked(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.SelectedOutage = null; }
    private void EditSelectedClicked(object? sender, RoutedEventArgs e) => EditSelection();
    private void EditSelection()
    {
        if (ViewModel is not { } vm) return;
        if (vm.IsTargets && vm.SelectedTarget is not null)
        {
            if (!vm.CanEdit) { vm.ReportInformation("Stop monitoring before editing a target."); return; }
            this.FindControl<TextBox>("TargetNameEditor")!.BringIntoView();
            this.FindControl<TextBox>("TargetNameEditor")!.Focus(NavigationMethod.Tab);
        }
        else if (vm.IsHistory && vm.CanComment)
        {
            this.FindControl<TextBox>("IncidentCommentEditor")!.BringIntoView();
            this.FindControl<TextBox>("IncidentCommentEditor")!.Focus(NavigationMethod.Tab);
        }
    }
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (closing || ViewModel is not { } vm) { base.OnKeyDown(e); return; }
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var focused = FocusManager?.GetFocusedElement() as Control;
        var list = focused as ListBox ?? focused?.FindAncestorOfType<ListBox>();
        var targetList = list?.Name == "TargetList";
        var incidentList = list?.Name == "IncidentTimeline";
        var page = e.Key switch { Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4, Key.D6 => 5, _ => -1 };
        if (e.KeyModifiers == command && page >= 0) { vm.SelectedPage = page; e.Handled = true; }
        else if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None) { vm.SelectedPage = 4; e.Handled = true; }
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        { e.Handled = true; if (vm.CanStart) await RunAsync(vm.StartAsync); else vm.ReportInformation(vm.StartGuidance); }
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.Shift)
        { e.Handled = true; if (vm.CanStop) await RunAsync(vm.StopAsync); }
        else if (e.Key == Key.Escape && vm.IsDiagnosticRunning) { vm.CancelDiagnostic(); e.Handled = true; }
        else if (e.Key == Key.Escape && vm.IsDiscovering) { vm.CancelDiscovery(); e.Handled = true; }
        else if (e.Key == Key.Escape && (targetList || incidentList))
        { if (targetList) vm.SelectedTarget = null; else vm.SelectedOutage = null; e.Handled = true; }
        else if (e.KeyModifiers is KeyModifiers.None && e.Key == Key.F4 && (targetList || incidentList))
        { e.Handled = true; EditSelection(); }
        else if (e.KeyModifiers is KeyModifiers.None && e.Key == Key.Delete && targetList && vm.CanEdit && vm.HasSelectedTarget)
        { e.Handled = true; RemoveTargetClicked(sender: this, new RoutedEventArgs()); }
        else if (e.Key == Key.C && (targetList || incidentList) && (e.KeyModifiers == command || e.KeyModifiers == (command | KeyModifiers.Shift)))
        {
            e.Handled = true; var row = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            await CopyAsync(targetList ? row ? vm.SelectedTargetRow : vm.SelectedTargetValue : row ? vm.SelectedIncidentRow : vm.SelectedIncidentValue,
                (targetList ? "target" : "incident") + (row ? " row" : " value"));
        }
        else if (e.Key == Key.S && e.KeyModifiers == command && vm.CanExport)
        { e.Handled = true; ExportClicked(this, new RoutedEventArgs()); }
        else if (e.Key == Key.OemComma && e.KeyModifiers == command) { vm.SelectedPage = 3; e.Handled = true; }
        else if (e.KeyModifiers == (command | KeyModifiers.Shift) && e.Key == Key.C)
        { e.Handled = true; await CopyAsync(vm.SelectedTarget?.Address, "target IP"); }
        else if (e.KeyModifiers == (command | KeyModifiers.Shift) && e.Key == Key.I)
        { e.Handled = true; await CopyAsync(vm.SelectedOutage?.Details, "incident details"); }
        else base.OnKeyDown(e);
    }
}
