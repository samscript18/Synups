using Synups.Core.Persistence;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    private LegacyProfilePreview? legacyImport;
    public string LegacyImportPreview => legacyImport?.Description ?? "Choose your original WirePeep .stgx file for a read-only profile preview. Import never enables startup registration or monitoring.";
    public bool CanImportLegacy => CanEdit && legacyImport is not null;
    public Task PreviewLegacyImportAsync(string fileName) => ExecuteAsync(async () =>
    {
        EnsureEditing();
        if (IsDiscovering) throw new InvalidOperationException("Finish or cancel discovery before importing a legacy profile.");
        legacyImport = null; Changed(nameof(LegacyImportPreview)); Changed(nameof(CanImportLegacy));
        legacyImport = await Task.Run(() => LegacySettingsReader.ReadFile(fileName));
        Changed(nameof(LegacyImportPreview)); Changed(nameof(CanImportLegacy));
        Message = "Preview loaded. Existing Synups configuration is unchanged. Review before confirming replacement.";
    });
    public Task ImportLegacyAsync(bool confirmed) => ExecuteAsync(async () =>
    {
        EnsureEditing();
        if (IsDiscovering) throw new InvalidOperationException("Finish or cancel discovery before importing a legacy profile.");
        if (!confirmed || legacyImport is not { } preview) throw new InvalidOperationException("Review and explicitly confirm the legacy profile preview first.");
        var backup = await store.SaveWithBackupAsync(preview.Configuration);
        SetConfiguration(preview.Configuration.ValidatedCopy());
        legacyImport = null; Changed(nameof(LegacyImportPreview)); Changed(nameof(CanImportLegacy));
        Message = "WirePeep profile imported with original IDs. Monitoring remains stopped. Original file/history/preferences unchanged. " +
            (backup is null ? "No previous Synups configuration file existed." : $"Previous configuration backed up: {backup}");
    });
}
