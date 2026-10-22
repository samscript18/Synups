using System.Collections.ObjectModel;
using System.Globalization;
using Synups.Core.Monitoring;
using Synups.Core.Alerts;
using Synups.Desktop.Integrations;
using Synups.Core.Persistence;
using Synups.Core.Diagnostics;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly MonitoringService service;
    private readonly ConfigurationStore store;
    private readonly PreferencesStore preferencesStore;
    public AppPreferences SavedPreferences { get; private set; } = new();
    public string? PreferencesError { get; private set; }
    public string HistoryStatus => service.PersistenceError ?? (service.HasUnsavedEvidence ? "Unsaved evidence awaiting Retry." : service.HasDurableHistory ? "SQLite history enabled; recovery is never inferred at session end." : "Session-memory history supplied by host.");
    private readonly AlertPolicy alertPolicy = new();
    private readonly StorageWarningPolicy storageWarnings = new();
    private Task storageRefresh = Task.CompletedTask;
    private bool storageClosing;
    private long nextStorageRefresh;
    private StorageUsage? storageUsage;
    private string storageReadError = "", prefCapacityMiB = "1024", cleanupDays = "30";
    public string PrefCapacityMiB { get => prefCapacityMiB; set => Set(ref prefCapacityMiB, value ?? ""); }
    public string CleanupDays { get => cleanupDays; set => Set(ref cleanupDays, value ?? ""); }
    public bool CanManageStorage => !busy && !IsRunning && !disposed && service.CanManageStorage;
    public bool CanCleanupHistory => CanManageStorage && service.HistoryReady && !service.HasUnsavedEvidence;
    public StorageUsage? StorageUsage => storageUsage;
    private static string Size(long bytes) => $"{bytes / (double)HistoryCapacity.MiB:F2} MiB ({bytes:N0} bytes)";
    public string StorageSummary => storageUsage is { } u
        ? $"Database allocated: {Size(u.AllocatedBytes)} of {Size(u.ConfiguredBytes)} configured ({100d * u.AllocatedBytes / u.ConfiguredBytes:F1}%). SQLite effective page limit: {Size(u.EffectiveLimitBytes)}."
        : "Storage measurement unavailable. " + storageReadError;
    public string StorageDetails => storageUsage is { } u
        ? $"Database file: {Size(u.DatabaseBytes)}; WAL: {Size(u.WalBytes)}; SHM: {Size(u.ShmBytes)}; rollback journal: {Size(u.JournalBytes)}.\n" +
          $"Reusable database pages: {Size(u.ReusableBytes)}; growth headroom: {Size(u.GrowthHeadroomBytes)}; available for records including reuse: {Size(u.AvailableDatabaseBytes)}.\n" +
          $"Managed backups: {Size(u.BackupBytes)}; automatic logs in the selected folder: {Size(u.DerivedLogBytes)}. Total of these files: {Size(u.TotalDiskBytes)}.\n" +
          $"Database volume free space: {(u.VolumeAvailableBytes is { } free ? Size(free) : "unavailable")}. This is shared disk space, not reserved capacity.\n" +
          $"Warning at 80%: {Size(u.WarningBytes)}; critical at 95%: {Size(u.CriticalBytes)} of allocated database pages. Backup folder: {u.BackupDirectory}.\n" + storageReadError
        : storageReadError;
    public string StorageWarning => storageUsage is { } u && u.WarningLevel > 0
        ? $"{(u.WarningLevel == 95 ? "Critical storage warning" : "Storage warning")}: database allocation is at least {u.WarningLevel}% of configured capacity. Increase capacity or back up and clean eligible history. Other files and temporary work require additional disk space."
        : "";
    public bool HasStorageWarning => !string.IsNullOrWhiteSpace(StorageWarning);
    private readonly AlertDelivery alertDelivery = new();
    private readonly AlertDelivery soundDelivery;
    public string SoundStatus => AlertSoundSink.Capability + " " + soundDelivery.Status;
    private bool prefOutageSound, prefRecoverySound;
    private string prefSoundFile = "";
    public bool PrefOutageSound { get => prefOutageSound; set => Set(ref prefOutageSound, value); }
    public bool PrefRecoverySound { get => prefRecoverySound; set => Set(ref prefRecoverySound, value); }
    public string PrefSoundFile { get => prefSoundFile; set => Set(ref prefSoundFile, value ?? ""); }
    public void TestAlertSound()
    {
        if (!CanPreferences) return;
        soundDelivery.Enqueue(new(Guid.NewGuid(), "Explicit user-requested sound test", PrefSoundFile, MonitorEventKind.ProbeError));
        Message = "Sound test queued; see playback status. This does not create a monitoring event or confirm audible delivery.";
    }
    private string alertBanner = "", lastStorageAlert = "";
    public string AlertBanner { get => alertBanner; private set { if (Set(ref alertBanner, value)) Changed(nameof(HasAlertBanner)); } }
    public bool HasAlertBanner => !string.IsNullOrWhiteSpace(AlertBanner);
    public string NativeAlertStatus => NativeAlertSink.Capability + " " + alertDelivery.Status;
    public bool CanPreferences => !busy && !IsRunning;
    private bool prefConfirmExit, prefTopmost, prefTray, prefMinimized, prefAutoStart, prefOutage = true, prefRecovery = true, prefErrors = true, prefNative, prefLogs = true, prefFocus;
    public bool PrefFocus { get => prefFocus; set => Set(ref prefFocus, value); }
    public event EventHandler? FocusRequested;
    private string prefLogDirectory = "", prefRetentionDays = "0";
    public bool PrefConfirmExit { get => prefConfirmExit; set => Set(ref prefConfirmExit, value); }
    public bool PrefTopmost { get => prefTopmost; set => Set(ref prefTopmost, value); }
    public bool PrefTray { get => prefTray; set => Set(ref prefTray, value); }
    public bool PrefMinimized { get => prefMinimized; set => Set(ref prefMinimized, value); }
    public bool PrefAutoStart { get => prefAutoStart; set => Set(ref prefAutoStart, value); }
    public bool PrefOutage { get => prefOutage; set => Set(ref prefOutage, value); }
    public bool PrefRecovery { get => prefRecovery; set => Set(ref prefRecovery, value); }
    public bool PrefErrors { get => prefErrors; set => Set(ref prefErrors, value); }
    public bool PrefNative { get => prefNative; set => Set(ref prefNative, value); }
    private bool prefDatedLogs;
    public bool PrefDatedLogs { get=>prefDatedLogs; set=>Set(ref prefDatedLogs,value); }
    public bool PrefLogs { get => prefLogs; set => Set(ref prefLogs, value); }
    public string PrefLogDirectory { get => prefLogDirectory; set => Set(ref prefLogDirectory, value ?? ""); }
    public string PrefRetentionDays { get => prefRetentionDays; set => Set(ref prefRetentionDays, value ?? ""); }
    private MonitorConfiguration configuration = MonitorConfiguration.Empty;
    private MonitoringSnapshot snapshot;
    private long seenHistory = -1;
    private int selectedPage;
    private bool busy, loaded, recoveryRequired;
    private string message = "Loading local configuration. Monitoring stays stopped.";
    private GroupRow? selectedGroup, targetGroup;
    private TargetRow? selectedTarget;
    private OutageRow? selectedOutage;
    private string groupName = "", pollSeconds = "5", failSeconds = "10", timeoutMilliseconds = "200";
    private bool groupEnabled;
    private string targetName = "", targetAddress = "", outageComment = "";
    private string firstGroupName = "Primary connection", firstTargetName = "First target", firstTargetAddress = "";
    public string FirstGroupName { get => firstGroupName; set => Set(ref firstGroupName, value ?? ""); }
    public string FirstTargetName { get => firstTargetName; set => Set(ref firstTargetName, value ?? ""); }
    public string FirstTargetAddress { get => firstTargetAddress; set => Set(ref firstTargetAddress, value ?? ""); }
    private bool disposed, resultsMatchConfiguration;

    public MainWindowViewModel() : this(SynupsPaths.DataDirectory) { }
    public MainWindowViewModel(string dataDirectory) : this(new MonitoringService(history: new SqliteHistoryStore(Path.Combine(dataDirectory, "history.v1.db"))),
        new ConfigurationStore(Path.Combine(dataDirectory, "configuration.v1.json")),
        new PreferencesStore(Path.Combine(dataDirectory, "preferences.v1.json"), Path.Combine(dataDirectory, "logs"))) { }
    internal MonitoringSnapshot CaptureMonitoring() => service.Capture();
    internal MonitoringDiagnostics CaptureDiagnostics() => service.Diagnostics;
    internal Task<HistoryState> ReadHistoryAsync() => service.ReadArchiveAsync();
    public MainWindowViewModel(MonitoringService service, ConfigurationStore store, PreferencesStore? preferencesStore = null, INativeAlertSink? soundSink = null, IDiagnosticRunner? diagnosticRunner = null, IDiagnosticRepository? diagnosticRepository = null)
    {
        soundDelivery = new(soundSink ?? new AlertSoundSink(), "Optional sound is off until explicitly configured.");
        this.service = service; this.store = store;
        this.preferencesStore = preferencesStore ?? new PreferencesStore(store.FileName + ".preferences.json", store.FileName + ".logs");
        snapshot = service.Capture();
        ConfigureDiagnostics(diagnosticRunner, diagnosticRepository);
    }
    public IReadOnlyList<WorkspacePage> NavigationItems { get; } =
    [
        new("Overview", "Network health", "M3,3 L9,3 L9,9 L3,9 Z M15,3 L21,3 L21,9 L15,9 Z M3,15 L9,15 L9,21 L3,21 Z M15,15 L21,15 L21,21 L15,21 Z"),
        new("Targets and groups", "Your monitoring profile", "M9,3 L15,3 L15,9 L9,9 Z M3,15 L9,15 L9,21 L3,21 Z M15,15 L21,15 L21,21 L15,21 Z M12,9 L12,12 M6,15 L6,12 L18,12 L18,15"),
        new("Activity and outages", "Evidence and comments", "M3,4 L21,4 M3,12 L7,12 L10,7 L14,17 L17,12 L21,12 M3,20 L21,20"),
        new("Preferences", "Make Synups yours", "M4,6 L8,6 M12,6 L20,6 M4,12 L14,12 M18,12 L20,12 M4,18 L6,18 M10,18 L20,18 M8,3 L12,3 L12,9 L8,9 Z M14,9 L18,9 L18,15 L14,15 Z M6,15 L10,15 L10,21 L6,21 Z"),
        new("Help and About", "Use Synups confidently", "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M9,8 Q12,5 15,8 Q17,11 12,13 L12,15 M12,17 L12,18"),
        new("Diagnostics", "Investigate one endpoint", "M3,12 L7,12 L10,4 L14,20 L17,12 L21,12"),
    ];
    public string PageTitle => NavigationItems[SelectedPage].Title;
    public ObservableCollection<GroupRow> Groups { get; } = new();
    public ObservableCollection<OutageRow> Outages { get; } = new();
    public ObservableCollection<MonitorEvent> Activity { get; } = new();
    public string ConfigurationPath => store.FileName;
    public bool IsRunning => snapshot.State is SessionState.Running or SessionState.Stopping;
    public bool CanEdit => loaded && !busy && !IsRunning && !disposed && !IsDiscovering;
    public bool CanStart => CanEdit && service.HistoryReady && !service.HasUnsavedEvidence && configuration.Groups.Any(g => g.Enabled);
    public string StartGuidance => disposed ? "This window is closing." : IsDiscovering ? "Finish or cancel discovery before starting monitoring." : busy ? "Finishing the current action…" :
        IsRunning ? (snapshot.Groups.SelectMany(g => g.Targets).Any(t => t.Result.Error == ProbeError.PermissionDenied)
            ? "ICMP permission was denied. Check OS/firewall policy; this is indeterminate, not confirmed downtime. Synups never elevates itself automatically."
            : "Stop monitoring before editing. ICMP filtering can cause missed replies without a service outage.") :
        !loaded ? "Load or recover your configuration before monitoring." :
        !service.HistoryReady || service.HasUnsavedEvidence ? "Repair history storage and Retry before starting; existing evidence is preserved." :
        Groups.Count == 0 ? "Add your first IP below. Nothing is probed until you choose Start monitoring." :
        !configuration.Groups.Any(g => g.Enabled) ? "No group is enabled. Add a target to a group, enable it and Apply group." :
        "Ready: choose Start monitoring. Not checked means no observation; stale means that peer was not checked in the latest cycle.";
    public bool CanStop => IsRunning && !busy;
    public bool CanRecover => recoveryRequired && !busy && !IsRunning;
    public bool CanReload => !busy && !IsRunning;
    public bool CanComment => selectedOutage is not null && !busy;
    public bool HasSelectedOutage => selectedOutage is not null;
    public bool CanExport => Outages.Count > 0 && !busy;
    public bool HasNoGroups => Groups.Count == 0;
    public bool HasNoOutages => service.HistoryReady && !timelineReading && Outages.Count == 0;
    public bool HasSelectedGroup => SelectedGroup is not null;
    public string Message { get => message; private set => Set(ref message, value); }
    public string MonitoringState => snapshot.State.ToString();
    public string OverviewEyebrow => IsRunning ? "LIVE MONITORING" : snapshot.LastMeasurementUtc.HasValue ? "LAST SESSION" : "READY WHEN YOU ARE";
    public string MetricContext => snapshot.LastMeasurementUtc is null ? "Awaiting real measurements" : IsRunning ? "Current session · group-level results" : "Last session · monitoring is stopped";
    public string EnabledGroupCount => configuration.Groups.Count(g => g.Enabled).ToString(CultureInfo.CurrentCulture);
    private string MeasuredCount(Func<GroupSnapshot, bool> predicate) => snapshot.LastMeasurementUtc is null ? "—" : snapshot.Groups.Count(predicate).ToString(CultureInfo.CurrentCulture);
    public string ConnectedGroupCount => MeasuredCount(g => g.Connectivity == GroupConnectivity.Connected);
    public string ConfirmedOutageCount => MeasuredCount(g => g.ConfirmedOutage);
    public string UnavailableGroupCount => MeasuredCount(g => g.Connectivity == GroupConnectivity.Unavailable);
    public bool HasCurrentOutage => snapshot.Groups.Any(g => g.ConfirmedOutage);
    public bool HasCurrentUncertainty => IsRunning && snapshot.Groups.Any(g => g.Connectivity is GroupConnectivity.NotChecked or GroupConnectivity.Unavailable or GroupConnectivity.Disconnected);
    public bool IsCurrentlyConnected => snapshot.State == SessionState.Running && snapshot.Groups.Any(g => g.Connectivity == GroupConnectivity.Connected) && !HasCurrentOutage && !HasCurrentUncertainty;
    public string MonitoredTime => snapshot.SessionId is { } id && snapshot.Sessions?.FirstOrDefault(s => s.Id == id) is { } session
        ? $"Observed session time: {session.Elapsed:g}{(session.ContainsObservationGap ? " · includes observation gaps" : "")} · through last completed poll"
        : "Observed session time: Not observed";
    public string LastMeasurement => TargetRow.DisplayTime(snapshot.LastMeasurementUtc);
    public string OverviewSummary
    {
        get
        {
            var enabled = snapshot.Groups.Where(g => g.Connectivity != GroupConnectivity.Disabled).ToArray();
            if (enabled.Length == 0 || enabled.All(g => g.LastMeasurementUtc is null))
                return "No measurements yet. Start monitoring to assess the configured ICMP groups.";
            return $"{(IsRunning ? "Session" : "Last session")}: {enabled.Length} active groups · " +
                $"{enabled.Count(g => g.Connectivity == GroupConnectivity.Connected)} connected · " +
                $"{enabled.Count(g => g.ConfirmedOutage)} confirmed unresolved outages · " +
                $"{enabled.Count(g => g.Connectivity == GroupConnectivity.Unavailable)} unavailable · " +
                $"{enabled.Count(g => g.Connectivity == GroupConnectivity.NotChecked)} not checked.";
        }
    }
    public string ConnectivityAssessment => !IsRunning ? "Monitoring is stopped. Results below are historical." :
        snapshot.Groups.Any(g => g.ConfirmedOutage) ? "Confirmed ICMP outage in one or more configured groups." :
        snapshot.Groups.Any(g => g.Connectivity is GroupConnectivity.NotChecked or GroupConnectivity.Unavailable) ? "Indeterminate — awaiting observations or resolving probe errors." :
        snapshot.Groups.Any(g => g.Connectivity == GroupConnectivity.Disconnected) ? "Disconnected observations — failure grace period in progress." :
        "Configured groups are responding.";

    public int SelectedPage
    {
        get => selectedPage;
        set { if (value is < 0 or > 5 || !Set(ref selectedPage, value)) return; Changed(nameof(IsOverview)); Changed(nameof(IsTargets)); Changed(nameof(IsHistory)); Changed(nameof(IsPreferences)); Changed(nameof(IsHelp)); Changed(nameof(PageTitle)); Changed(nameof(IsDiagnostics)); }
    }
    public bool IsOverview => SelectedPage == 0;
    public bool IsTargets => SelectedPage == 1;
    public bool IsHistory => SelectedPage == 2;
    public bool IsPreferences => SelectedPage == 3;
    public bool IsHelp => SelectedPage == 4;
    public string VersionText => typeof(MainWindowViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;
    public string KeyboardHelp => $"F1: Help · F5: Start · Shift+F5: Stop · {(OperatingSystem.IsMacOS() ? "Command" : "Ctrl")}+1…6: screens · {(OperatingSystem.IsMacOS() ? "Command" : "Ctrl")}+Shift+C: copy target IP · +Shift+I: copy incident. In a selected target/incident list: Ctrl/Cmd+C copies the chosen value, +Shift+C copies the row, F4 edits and Escape clears selection. Delete removes a selected target after confirmation; Ctrl/Cmd+S exports incidents. Escape cancels confirmation dialogs or active diagnostics/discovery. Tab/Shift+Tab move between controls; Enter/Space activate focused buttons.";
    public bool HasSelectedTarget => SelectedTarget is not null;
    public GroupRow? SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (!Set(ref selectedGroup, value)) return;
            GroupName = value?.Name ?? ""; GroupEnabled = value?.Configuration.Enabled ?? false;
            PollSeconds = ((value?.Configuration.PollMilliseconds ?? 5000) / 1000d).ToString(CultureInfo.InvariantCulture);
            FailSeconds = ((value?.Configuration.FailMilliseconds ?? 10000) / 1000d).ToString(CultureInfo.InvariantCulture);
            TimeoutMilliseconds = (value?.Configuration.TimeoutMilliseconds ?? 200).ToString(CultureInfo.InvariantCulture);
            SelectedTarget = null; TargetGroup = value;
            Changed(nameof(HasSelectedGroup));
        }
    }
    public GroupRow? TargetGroup { get => targetGroup; set => Set(ref targetGroup, value); }
    public TargetRow? SelectedTarget
    {
        get => selectedTarget;
        set { if (!Set(ref selectedTarget, value)) return; TargetName = value?.Name ?? ""; TargetAddress = value?.Address ?? ""; TargetGroup = SelectedGroup; Changed(nameof(HasSelectedTarget)); }
    }
    public OutageRow? SelectedOutage
    {
        get => selectedOutage;
        set { if (!Set(ref selectedOutage, value)) return; OutageComment = value?.Comment ?? ""; Changed(nameof(CanComment)); Changed(nameof(HasSelectedOutage)); }
    }
    public string GroupName { get => groupName; set => Set(ref groupName, value ?? ""); }
    public string PollSeconds { get => pollSeconds; set => Set(ref pollSeconds, value ?? ""); }
    public string FailSeconds { get => failSeconds; set => Set(ref failSeconds, value ?? ""); }
    public string TimeoutMilliseconds { get => timeoutMilliseconds; set => Set(ref timeoutMilliseconds, value ?? ""); }
    public bool GroupEnabled { get => groupEnabled; set => Set(ref groupEnabled, value); }
    public string TargetName { get => targetName; set => Set(ref targetName, value ?? ""); }
    public string TargetAddress { get => targetAddress; set => Set(ref targetAddress, value ?? ""); }
    public string OutageComment { get => outageComment; set => Set(ref outageComment, value ?? ""); }

    public Task InitializeAsync() => ExecuteAsync(async () =>
    {
        var preferences = await preferencesStore.LoadAsync();
        SavedPreferences = preferences.Preferences; PreferencesError = preferences.Error; LoadPreferencesDraft(); Changed(nameof(SavedPreferences));
        try { await service.InitializeHistoryAsync(); }
        catch (Exception ex) { Message = $"History unavailable. Existing evidence is preserved: {ex.Message}"; }
        alertPolicy.IgnoreExisting(service.Capture().Events);
        await service.SetAutomaticRetentionAsync(SavedPreferences.RetentionDays);
        await RefreshStorageAsync(force: true);
        if (storageUsage is { } usage) PrefCapacityMiB = (usage.ConfiguredBytes / HistoryCapacity.MiB).ToString(CultureInfo.InvariantCulture);
        var result = await store.LoadAsync();
        recoveryRequired = result.NeedsRecovery; loaded = !result.NeedsRecovery;
        if (result.Configuration is { } config) SetConfiguration(config);
        Message = result.Error ?? service.PersistenceError ?? PreferencesError ?? (Groups.Count == 0 ? "No groups configured. Add a group or restore original defaults. No probes run until Start." : "Configuration loaded. Monitoring stays stopped until Start.");
        await RequestTimelineRefresh();
        await RefreshDiagnosticHistoryAsync();
    });

    public Task StartAsync() => ExecuteAsync(async () =>
    {
        if (!loaded) throw new InvalidOperationException("Recover or reload configuration before starting.");
        await service.StartAsync(configuration);
        resultsMatchConfiguration = true;
        Message = "Monitoring configured literal IPs. ICMP failure alone does not prove the Internet is unavailable.";
    });
    public Task StopAsync() => ExecuteAsync(async () =>
    { await service.StopAsync(); await WriteAutomaticLogsAsync(); Message = "Monitoring stopped. History saved; unresolved outages were not recovered."; });

    public Task CreateFirstTargetAsync() => ExecuteAsync(async () =>
    {
        EnsureEditing();
        if (configuration.Groups.Count != 0) throw new InvalidOperationException("Use Targets and groups to edit your existing profile; quick setup never replaces it.");
        var target = new TargetConfiguration(Guid.NewGuid(), FirstTargetName.Trim(), FirstTargetAddress.Trim());
        var group = new GroupConfiguration(Guid.NewGuid(), FirstGroupName.Trim(), true, 5000, 10000, 1000, [target]);
        await SaveConfigurationAsync(new(1, [group]), group.Id, target.Id);
        Message = "Your target and enabled group are saved. Choose Start monitoring when ready; no network request has been made.";
    });

    public void NewGroup()
    {
        if (!CanEdit) return;
        SelectedGroup = null; GroupName = ""; GroupEnabled = false; PollSeconds = "5"; FailSeconds = "10"; TimeoutMilliseconds = "200";
        SelectedTarget = null; TargetGroup = null;
        Message = "Enter a new group name and apply. Add a target before enabling the group.";
    }
    public void NewTarget()
    {
        if (!CanEdit) return;
        SelectedTarget = null; TargetName = ""; TargetAddress = ""; TargetGroup = SelectedGroup;
        Message = "Enter a target name and literal IP address, choose its group, then apply.";
    }
    public Task ApplyGroupAsync() => ExecuteAsync(async () =>
    {
        EnsureEditing();
        var id = SelectedGroup?.Id ?? Guid.NewGuid();
        var g = new GroupConfiguration(id, GroupName.Trim(), GroupEnabled, Seconds(PollSeconds), Seconds(FailSeconds),
            int.TryParse(TimeoutMilliseconds, NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) ? timeout : 0,
            SelectedGroup?.Configuration.Targets ?? Array.Empty<TargetConfiguration>());
        var groups = configuration.Groups.Where(x => x.Id != id).Append(g).ToArray();
        await SaveConfigurationAsync(new(1, groups), id);
    });
    public Task RemoveGroupAsync() => ExecuteAsync(async () =>
    { EnsureEditing(); if (SelectedGroup is not null) await SaveConfigurationAsync(new(1, configuration.Groups.Where(g => g.Id != SelectedGroup.Id).ToArray())); });

    public Task ApplyTargetAsync() => ExecuteAsync(async () =>
    {
        EnsureEditing();
        if (TargetGroup is null) throw new ArgumentException("Choose a target group first.");
        var target = new TargetConfiguration(SelectedTarget?.Id ?? Guid.NewGuid(), TargetName.Trim(), TargetAddress.Trim());
        var destinationId = TargetGroup.Id;
        var groups = configuration.Groups.Select(g =>
        {
            var targets = g.Targets.Where(t => t.Id != target.Id).ToList();
            if (g.Id == destinationId) targets.Add(target);
            return g with { Targets = targets.ToArray(), Enabled = g.Enabled && targets.Count > 0 };
        }).ToArray();
        await SaveConfigurationAsync(new(1, groups), destinationId, target.Id);
    });
    public Task RemoveTargetAsync() => ExecuteAsync(async () =>
    {
        EnsureEditing(); if (SelectedTarget is null) return;
        var targetId = SelectedTarget.Id; var selected = SelectedGroup?.Id;
        await SaveConfigurationAsync(new(1, configuration.Groups.Select(g =>
        {
            var targets = g.Targets.Where(t => t.Id != targetId).ToArray();
            return g with { Targets = targets, Enabled = g.Enabled && targets.Length > 0 };
        }).ToArray()), selected);
    });
    public Task RestoreDefaultsAsync(bool recoverInvalid = false) => ExecuteAsync(async () =>
    {
        if (IsRunning) throw new InvalidOperationException("Stop monitoring before changing configuration.");
        await SaveConfigurationAsync(MonitorConfiguration.OriginalDefaults(), recoverInvalid: recoverInvalid);
        Message = "Original literal-IP defaults saved. Three discovery-only groups are disabled with no targets; discovery was not run.";
    });

    public async Task SaveCommentAsync()
    {
        try
        {
            if (SelectedOutage is null) return;
            await service.UpdateCommentAsync(SelectedOutage.Id, OutageComment);
            await WriteAutomaticLogsAsync();
            Message = "Comment and comment event saved.";
            Refresh();
        }
        catch (Exception ex) { Message = ex.Message; }
    }
    private void LoadPreferencesDraft()
    {
        PrefConfirmExit = SavedPreferences.ConfirmExit; PrefTopmost = SavedPreferences.AlwaysOnTop;
        PrefTray = SavedPreferences.MinimizeToTray; PrefMinimized = SavedPreferences.LaunchMinimized;
        PrefAutoStart = SavedPreferences.MonitorOnLaunch; PrefOutage = SavedPreferences.NotifyOutages;
        PrefRecovery = SavedPreferences.NotifyRecovery; PrefErrors = SavedPreferences.NotifyErrors;
        PrefNative = SavedPreferences.NativeNotifications; PrefLogs = SavedPreferences.AutomaticLogs;
        PrefOutageSound = SavedPreferences.PlayOutageSound; PrefRecoverySound = SavedPreferences.PlayRecoverySound; PrefSoundFile = SavedPreferences.AlertSoundFile;
        PrefLogName = LogNameChoices.Single(c => c.Style == (SavedPreferences.LogNaming ?? (SavedPreferences.DateLogFilenames ? LogFilenameStyle.UtcSessionStart : LogFilenameStyle.StableSessionId)));
        PrefGroupHeight = SavedPreferences.GroupPaneHeight; PrefTargetHeight = SavedPreferences.TargetPaneHeight; PrefIncidentHeight = SavedPreferences.IncidentPaneHeight;
        PrefFocus = SavedPreferences.FocusOnConfirmedEvents; PrefDatedLogs = SavedPreferences.DateLogFilenames;
        PrefLogDirectory = SavedPreferences.LogDirectory; PrefRetentionDays = SavedPreferences.RetentionDays.ToString(CultureInfo.InvariantCulture);
    }
    public Task SavePreferencesAsync(bool recover = false, bool confirmRetention = false) => ExecuteAsync(async () =>
    {
        if (IsRunning) throw new InvalidOperationException("Stop monitoring before applying preferences.");
        if (!int.TryParse(PrefRetentionDays, out var days)) throw new ArgumentException("Retention must be an integer number of days.");
        if (days > 0 && !confirmRetention) throw new InvalidOperationException("Confirm automatic history deletion before enabling retention.");
        var next = SavedPreferences with { ConfirmExit = PrefConfirmExit, AlwaysOnTop = PrefTopmost, MinimizeToTray = PrefTray,
            LaunchMinimized = PrefMinimized, MonitorOnLaunch = PrefAutoStart, NotifyOutages = PrefOutage, NotifyRecovery = PrefRecovery,
            NotifyErrors = PrefErrors, NativeNotifications = PrefNative, AutomaticLogs = PrefLogs, LogDirectory = PrefLogDirectory.Trim(), RetentionDays = days,
            FocusOnConfirmedEvents = PrefFocus, DateLogFilenames = PrefLogName?.Style is LogFilenameStyle.UtcSessionStart or LogFilenameStyle.LocalSessionStart, PlayOutageSound = PrefOutageSound, PlayRecoverySound = PrefRecoverySound, AlertSoundFile = PrefSoundFile, LogNaming = PrefLogName?.Style, GroupPaneHeight = PrefGroupHeight, TargetPaneHeight = PrefTargetHeight, IncidentPaneHeight = PrefIncidentHeight };
        next.Validate(); await preferencesStore.SaveAsync(next, recover); SavedPreferences = next; PreferencesError = null;
        await service.SetAutomaticRetentionAsync(days);
        await RefreshStorageAsync(force: true);
        Message = "Preferences saved. Monitoring on launch applies at the next launch; OS login settings were not changed.";
        Changed(nameof(SavedPreferences));
    });
    public async Task SavePlacementAsync(WindowPlacement placement)
    {
        if (PreferencesError is not null) return; // Never overwrite a corrupt file just to save geometry.
        var next = SavedPreferences with { Placement = placement }; await preferencesStore.SaveAsync(next); SavedPreferences = next;
    }
    public Task RetryHistoryAsync() => ExecuteAsync(async () =>
    {
        await service.InitializeHistoryAsync(); await service.RetryPersistenceAsync();
        await RefreshStorageAsync(force: true);
        Message = "History storage ready; pending evidence saved.";
    });
    public Task SaveCapacityAsync() => ExecuteAsync(async () =>
    {
        if (!long.TryParse(PrefCapacityMiB, NumberStyles.None, CultureInfo.InvariantCulture, out var mib) || mib is < 256 or > 10240)
            throw new ArgumentException("Enter a whole number of MiB from 256 to 10240 (10 GiB).");
        await service.SetHistoryCapacityAsync(checked(mib * HistoryCapacity.MiB));
        await RefreshStorageAsync(force: true);
        Message = "History capacity committed. Existing records are preserved. Use Retry history storage to save any pending evidence.";
    });
    public Task BackupHistoryAsync(bool confirmed = false) => ExecuteAsync(async () =>
    {
        if (!confirmed) throw new InvalidOperationException("Confirm creation of a full history backup first.");
        if (IsRunning) throw new InvalidOperationException("Stop monitoring before creating a backup.");
        var destination = await service.BackupHistoryAsync();
        await RefreshStorageAsync(force: true);
        Message = $"Verified SQLite backup saved: {destination}. It contains committed sessions, events and comments. It does not include preferences, configuration or derived logs.";
    });
    public Task CleanupHistoryAsync(bool confirmed = false) => ExecuteAsync(async () =>
    {
        if (!confirmed) throw new InvalidOperationException("Confirm history cleanup first.");
        if (IsRunning) throw new InvalidOperationException("Stop monitoring before cleaning history.");
        if (!int.TryParse(CleanupDays, NumberStyles.None, CultureInfo.InvariantCulture, out var days) || days is < 1 or > 3650)
            throw new ArgumentException("Cleanup age must be 1–3650 days.");
        var backup = await service.BackupHistoryAsync(); // No deletion when a complete pre-cleanup backup cannot be made.
        await service.ApplyRetentionAsync(days);
        await RefreshStorageAsync(force: true);
        Message = $"Eligible sessions older than {days} days cleaned. Unresolved/interrupted sessions kept. Freed pages can be reused; file size is unchanged. Backup: {backup}. Derived logs were kept.";
    });
    public async Task RefreshStorageAsync(bool force = false)
    {
        if (disposed || storageClosing || !service.CanManageStorage) return;
        while (!storageRefresh.IsCompleted) await storageRefresh;
        if (disposed || storageClosing || (!force && Environment.TickCount64 < nextStorageRefresh)) return;
        nextStorageRefresh = Environment.TickCount64 + 5000;
        await (storageRefresh = ReadStorageAsync());
    }
    private async Task ReadStorageAsync()
    {
        try
        {
            storageUsage = await service.GetStorageUsageAsync(SavedPreferences.LogDirectory);
            storageReadError = "";
            var warning = storageWarnings.Select(storageUsage);
            if (warning > 0 && SavedPreferences.NotifyErrors)
            {
                AlertBanner = $"In-app storage alert — {StorageWarning}";
                if (SavedPreferences.NativeNotifications) alertDelivery.Enqueue(new(Guid.NewGuid(), "Synups: history storage capacity", StorageWarning, MonitorEventKind.ProbeError));
            }
        }
        catch (Exception ex) { storageReadError = $"Storage measurement failed: {ex.Message}"; storageUsage = null; }
        Changed(nameof(StorageUsage)); Changed(nameof(StorageSummary)); Changed(nameof(StorageDetails)); Changed(nameof(StorageWarning)); Changed(nameof(HasStorageWarning));
    }
    private Task WriteAutomaticLogsAsync() => SavedPreferences.AutomaticLogs && service.HistoryReady ? ExportAutomaticAsync() : Task.CompletedTask;
    private async Task ExportAutomaticAsync() => await AutomaticLogExporter.WriteAsync(SavedPreferences.LogDirectory, await service.ReadArchiveAsync(), SavedPreferences.LogNaming ?? (SavedPreferences.DateLogFilenames ? LogFilenameStyle.UtcSessionStart : LogFilenameStyle.StableSessionId));
    public Task ExportAsync(Stream destination) => ExecuteAsync(async () =>
    {
        try { await service.ExportIncidentsAsync(destination); }
        catch (Exception ex) { throw new IOException($"CSV export did not complete; the destination may be partial. Source history is preserved. {ex.Message}", ex); }
        Message = "Outage CSV exported with UTC timestamps. Empty recovery fields mean no recovery was observed.";
    });
    public void ReportError(string error) => Message = error;
    public void ReportInformation(string information) => Message = information;

    private async Task SaveConfigurationAsync(MonitorConfiguration config, Guid? selectedGroupId = null, Guid? selectedTargetId = null, bool recoverInvalid = false)
    {
        var copy = config.ValidatedCopy();
        await store.SaveAsync(copy, recoverInvalid);
        loaded = true; recoveryRequired = false;
        SetConfiguration(copy, selectedGroupId, selectedTargetId);
        Message = "Configuration saved. Start monitoring to use these settings. Edits preserve existing IDs.";
    }
    private void SetConfiguration(MonitorConfiguration value, Guid? groupId = null, Guid? targetId = null)
    {
        configuration = value;
        resultsMatchConfiguration = false;
        SelectedGroup = null; Groups.Clear();
        foreach (var g in value.Groups) Groups.Add(new(g));
        SelectedGroup = Groups.FirstOrDefault(g => g.Id == groupId) ?? Groups.FirstOrDefault();
        SelectedTarget = SelectedGroup?.Targets.FirstOrDefault(t => t.Id == targetId);
        Changed(nameof(HasNoGroups)); Changed(nameof(HasSelectedGroup));
    }
    private void EnsureEditing()
    { if (!loaded || IsRunning) throw new InvalidOperationException("Stop monitoring and load a valid profile before editing."); }
    private static int Seconds(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) || seconds is <= 0 or > 36000 || seconds * 1000 != decimal.Truncate(seconds * 1000))
            throw new ArgumentException("Enter a positive number of seconds, with up to three decimal places (use a decimal point).");
        return (int)(seconds * 1000);
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy || disposed) return;
        busy = true; NotifyControls();
        try { await action(); }
        catch (Exception ex) { Message = ex.Message; }
        finally
        {
            try { await RefreshStorageAsync(force: true); }
            finally { busy = false; Refresh(); }
        }
    }
    public void Refresh()
    {
        _ = RefreshStorageAsync(); // At most one owned background measurement; no SQLite work on the dispatcher.
        var previousState = snapshot.State;
        snapshot = service.Capture();
        // Reusing a target ID after an address edit must never attach an old measurement to the new address.
        if (!resultsMatchConfiguration) snapshot = snapshot with { Groups = Array.Empty<GroupSnapshot>(), LastMeasurementUtc = null };
        foreach (var notification in alertPolicy.Select(snapshot.Events, SavedPreferences))
        {
            AlertBanner = $"In-app alert — {notification.Title}: {notification.Body}";
            if (SoundAlertPolicy.ShouldPlay(notification, SavedPreferences))
                soundDelivery.Enqueue(notification with { Body = SavedPreferences.AlertSoundFile });
            if (SavedPreferences.NativeNotifications) alertDelivery.Enqueue(notification);
        }
        if (alertPolicy.ShouldFocus(snapshot.Events, SavedPreferences.FocusOnConfirmedEvents)) FocusRequested?.Invoke(this, EventArgs.Empty);
        if (service.PersistenceError is { } storageError && storageError != lastStorageAlert && SavedPreferences.NotifyErrors)
        {
            lastStorageAlert = storageError; AlertBanner = $"In-app storage alert — {storageError}";
            if (SavedPreferences.NativeNotifications) alertDelivery.Enqueue(new(Guid.NewGuid(), "Synups: evidence storage error", storageError, MonitorEventKind.ProbeError));
        }
        if (service.PersistenceError is null) lastStorageAlert = "";
        Changed(nameof(SoundStatus)); Changed(nameof(NativeAlertStatus)); Changed(nameof(CanPreferences));
        foreach (var g in Groups) g.Apply(snapshot.Groups.FirstOrDefault(s => s.Id == g.Id), IsRunning);
        if (seenHistory != snapshot.HistoryRevision)
        {
            _ = RequestTimelineRefresh(); // One owned read, coalesced; no sample backlog or full-history UI load.
            // No window replacement: the bounded event viewport changes only when the event sequence changes.
            if (!Activity.Select(e => e.Sequence).SequenceEqual(snapshot.Events.Select(e => e.Sequence)))
            {
                var retainedSequences = snapshot.Events.Select(e => e.Sequence).ToHashSet();
                foreach (var removed in Activity.Where(e => !retainedSequences.Contains(e.Sequence)).ToArray()) Activity.Remove(removed);
                // Retention can remove middle sessions and backfill older events into the 500-item viewport.
                for (var i = 0; i < snapshot.Events.Count; i++)
                    if (i >= Activity.Count || Activity[i].Sequence != snapshot.Events[i].Sequence) Activity.Insert(i, snapshot.Events[i]);
            }
            seenHistory = snapshot.HistoryRevision;
        }
        if (snapshot.Error is { } error) Message = error;
        else if (previousState == SessionState.Stopping && snapshot.State == SessionState.Stopped)
            Message = "Monitoring has finished stopping. Restart and editing are available.";
        Changed(nameof(MonitoredTime)); Changed(nameof(MonitoringState)); Changed(nameof(OverviewSummary)); Changed(nameof(ConnectivityAssessment)); Changed(nameof(LastMeasurement));
        foreach (var name in new[] { nameof(OverviewEyebrow), nameof(MetricContext), nameof(EnabledGroupCount), nameof(ConnectedGroupCount), nameof(ConfirmedOutageCount), nameof(UnavailableGroupCount), nameof(HasCurrentOutage), nameof(HasCurrentUncertainty), nameof(IsCurrentlyConnected) }) Changed(name);
        Changed(nameof(HasNoOutages)); Changed(nameof(HistoryStatus)); NotifyControls();
    }
    private void NotifyControls()
    {
        TimelineControlsChanged();
        Changed(nameof(CanImportLegacy)); Changed(nameof(CanDiscover)); Changed(nameof(CanAcceptGateway)); Changed(nameof(CanCheckLegacy)); Changed(nameof(CanAcceptLegacy));
        foreach (var name in new[] { nameof(IsRunning), nameof(CanEdit), nameof(CanStart), nameof(StartGuidance), nameof(CanStop), nameof(CanRecover), nameof(CanReload), nameof(CanComment), nameof(CanExport), nameof(CanPreferences), nameof(CanManageStorage), nameof(CanCleanupHistory) }) Changed(name);
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await DisposeDiagnosticsAsync();
        CancelDiscovery(); await discoveryTask; discoveryCancellation?.Dispose(); discoveryCancellation = null;
        // Preserve ownership if Stop times out: caller must keep window open and retry instead of abandoning workers.
        await service.StopAsync(); await WriteAutomaticLogsAsync();
        storageClosing = true;
        timelineClosing = true;
        try
        {
            await timelineRead;
            await storageRefresh;
            await service.DisposeAsync(); await alertDelivery.DisposeAsync(); await soundDelivery.DisposeAsync(); disposed = true; Refresh();
        }
        finally { if (!disposed) { storageClosing = false; timelineClosing = false; } }
    }
}
