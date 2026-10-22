using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Synups.Desktop;

public partial class MainWindow
{
    private void OpenDiagnosticsFromTray(bool quick)
    {
        RestoreFromTray();
        if(ViewModel is not { } vm) return;
        if(quick && vm.CanRunDiagnostic) vm.SelectedDiagnosticProfile=Synups.Core.Diagnostics.DiagnosticProfile.BuiltIn.First(p=>p.Id=="quick");
        vm.SelectedPage=5;
        if(!vm.IsDiagnosticRunning) vm.ReportInformation("Review the diagnostic target and parameters, then confirm Run. Opening from the tray sends no probes.");
    }
    private async void RunDiagnosticClicked(object? sender, RoutedEventArgs e)
    {
        if(closing || ViewModel is not { CanRunDiagnostic:true } vm) return;
        try
        {
            var request=vm.PrepareDiagnosticRequest();
            if(await ConfirmAsync("Run approved diagnostics",MainWindowViewModel.DescribeDiagnosticRequest(request)))
                await vm.RunDiagnosticAsync(request,confirmed:true);
        }
        catch(Exception ex) { vm.ReportError(ex.Message); }
    }
    private void CancelDiagnosticClicked(object? sender,RoutedEventArgs e) => ViewModel?.CancelDiagnostic();
    private void DiagnoseIncidentClicked(object? sender,RoutedEventArgs e) => ViewModel?.DiagnoseSelectedIncident();
    private void ClearDiagnosticIncidentClicked(object? sender,RoutedEventArgs e) => ViewModel?.ClearDiagnosticIncident();
    private async void RefreshDiagnosticHistoryClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.RefreshDiagnosticHistoryAsync(); }
    private async void OpenDiagnosticClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.OpenDiagnosticHistoryAsync(); }
    private async void SaveDiagnosticBaselineClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.SaveDiagnosticBaselineAsync(); }
    private async void CompareDiagnosticClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.CompareDiagnosticAsync(); }
    private async void PreviewDiagnosticEvidenceClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.PreviewDiagnosticEvidenceAsync(); }
    private async void ExportDiagnosticEvidenceClicked(object? sender,RoutedEventArgs e)
    {
        if(closing || ViewModel is not { CanUseDiagnosticEvidence:true } vm) return;
        await RunAsync(async () =>
        {
            await vm.PreviewDiagnosticEvidenceAsync();
            if(!vm.HasDiagnosticEvidencePreview || !await ConfirmAsync("Export reviewed evidence","Export the exact evidence currently shown in the preview as JSON, Markdown and PDF? Review redaction before continuing. A new evidence folder is created; existing files are preserved.")) return;
            var folders=await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title="Choose evidence export parent folder",AllowMultiple=false });
            if(folders.Count!=1) return;
            var directory=folders[0].TryGetLocalPath() ?? throw new IOException("Choose a local folder for evidence export.");
            await vm.ExportDiagnosticEvidenceAsync(directory);
        });
    }
    private async void SaveDiagnosticProfileClicked(object? sender,RoutedEventArgs e)
    { if(ViewModel is { } vm) await vm.SaveDiagnosticProfileAsync(); }
}
