using System.Collections.ObjectModel;
using System.Globalization;
using Synups.Core.Monitoring;
using Synups.Core.Alerts;
using Synups.Desktop.Integrations;
using Synups.Core.Persistence;

namespace Synups.Desktop;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly MonitoringService service;
    private readonly ConfigurationStore store;
    private readonly PreferencesStore preferencesStore;
    public AppPreferences SavedPreferences { get; private set; } = new();
    public string? PreferencesError { get; private set; }
    public string HistoryStatus => service.PersistenceError ?? (service.HasUnsavedEvidence ? "Unsaved evidence awaiting Retry." : service.HasDurableHistory ? "SQLite history enabled; recovery is never inferred at session end." : "Session-memory history supplied by host.");
    private readonly AlertPolicy alertPolicy = new();
    private readonly AlertDelivery alertDelivery = new();
    private string alertBanner = "", lastStorageAlert = "";
    public string AlertBanner { get => alertBanner; private set => Set(ref alertBanner, value); }
    public string NativeAlertStatus => NativeAlertSink.Capability + " " + alertDelivery.Status;
    public bool CanPreferences => !busy && !IsRunning;
    private bool prefConfirmExit, prefTopmost, prefTray, prefMinimized, prefAutoStart, prefOutage = true, prefRecovery = true, prefErrors = true, prefNative, prefLogs = true;
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
    private bool disposed, resultsMatchConfiguration;

    public MainWindowViewModel() : this(new MonitoringService(history: new SqliteHistoryStore(SynupsPaths.HistoryFile)),
        new ConfigurationStore(SynupsPaths.ConfigurationFile), new PreferencesStore(SynupsPaths.PreferencesFile)) { }
    public MainWindowViewModel(MonitoringService service, ConfigurationStore store, PreferencesStore? preferencesStore = null)
    {
        this.service = service; this.store = store;
        this.preferencesStore = preferencesStore ?? new PreferencesStore(store.FileName + ".preferences.json", store.FileName + ".logs");
        snapshot = service.Capture();
    }
    public IReadOnlyList<string> NavigationItems { get; } = new[] { "Overview", "Targets and groups", "Activity and outages", "Preferences" };
    public ObservableCollection<GroupRow> Groups { get; } = new();
    public ObservableCollection<OutageRow> Outages { get; } = new();
    public ObservableCollection<MonitorEvent> Activity { get; } = new();
    public string ConfigurationPath => store.FileName;
    public bool IsRunning => snapshot.State is SessionState.Running or SessionState.Stopping;
    public bool CanEdit => loaded && !busy && !IsRunning && !disposed;
    public bool CanStart => CanEdit && service.HistoryReady && !service.HasUnsavedEvidence && configuration.Groups.Any(g => g.Enabled);
    public bool CanStop => IsRunning && !busy;
    public bool CanRecover => recoveryRequired && !busy && !IsRunning;
    public bool CanReload => !busy && !IsRunning;
    public bool CanComment => selectedOutage is not null && !busy;
    public bool CanExport => Outages.Count > 0 && !busy;
    public bool HasNoGroups => Groups.Count == 0;
    public bool HasNoOutages => service.HistoryReady && Outages.Count == 0;
    public bool HasSelectedGroup => SelectedGroup is not null;
    public string Message { get => message; private set => Set(ref message, value); }
    public string MonitoringState => snapshot.State.ToString();
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
        "Configured groups responding. A group needs only one responding peer.";

    public int SelectedPage
    {
        get => selectedPage;
        set { if (value is < 0 or > 3 || !Set(ref selectedPage, value)) return; Changed(nameof(IsOverview)); Changed(nameof(IsTargets)); Changed(nameof(IsHistory)); Changed(nameof(IsPreferences)); }
    }
    public bool IsOverview => SelectedPage == 0;
    public bool IsTargets => SelectedPage == 1;
    public bool IsHistory => SelectedPage == 2;
    public bool IsPreferences => SelectedPage == 3;
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
        set { if (!Set(ref selectedTarget, value)) return; TargetName = value?.Name ?? ""; TargetAddress = value?.Address ?? ""; TargetGroup = SelectedGroup; }
    }
    public OutageRow? SelectedOutage
    {
        get => selectedOutage;
        set { if (!Set(ref selectedOutage, value)) return; OutageComment = value?.Comment ?? ""; Changed(nameof(CanComment)); }
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
        SavedPreferences = preferences.Preferences; PreferencesError = preferences.Error; LoadPreferencesDraft();
        try { await service.InitializeHistoryAsync(); }
        catch (Exception ex) { Message = $"History unavailable. Existing evidence is preserved: {ex.Message}"; }
        alertPolicy.IgnoreExisting(service.Capture().Events);
        if (service.HistoryReady && SavedPreferences.RetentionDays > 0) await service.ApplyRetentionAsync(SavedPreferences.RetentionDays);
        var result = await store.LoadAsync();
        recoveryRequired = result.NeedsRecovery; loaded = !result.NeedsRecovery;
        if (result.Configuration is { } config) SetConfiguration(config);
        Message = result.Error ?? service.PersistenceError ?? PreferencesError ?? (Groups.Count == 0 ? "No groups configured. Add a group or restore original defaults. No probes run until Start." : "Configuration loaded. Monitoring stays stopped until Start.");
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
        PrefLogDirectory = SavedPreferences.LogDirectory; PrefRetentionDays = SavedPreferences.RetentionDays.ToString(CultureInfo.InvariantCulture);
    }
    public Task SavePreferencesAsync(bool recover = false) => ExecuteAsync(async () =>
    {
        if (IsRunning) throw new InvalidOperationException("Stop monitoring before applying preferences.");
        if (!int.TryParse(PrefRetentionDays, out var days)) throw new ArgumentException("Retention must be an integer number of days.");
        var next = SavedPreferences with { ConfirmExit = PrefConfirmExit, AlwaysOnTop = PrefTopmost, MinimizeToTray = PrefTray,
            LaunchMinimized = PrefMinimized, MonitorOnLaunch = PrefAutoStart, NotifyOutages = PrefOutage, NotifyRecovery = PrefRecovery,
            NotifyErrors = PrefErrors, NativeNotifications = PrefNative, AutomaticLogs = PrefLogs, LogDirectory = PrefLogDirectory.Trim(), RetentionDays = days };
        next.Validate(); await preferencesStore.SaveAsync(next, recover); SavedPreferences = next; PreferencesError = null;
        if (days > 0 && service.HistoryReady) await service.ApplyRetentionAsync(days);
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
        Message = "History storage ready; pending evidence saved.";
    });
    private Task WriteAutomaticLogsAsync() => SavedPreferences.AutomaticLogs ? ExportAutomaticAsync() : Task.CompletedTask;
    private async Task ExportAutomaticAsync() => await AutomaticLogExporter.WriteAsync(SavedPreferences.LogDirectory, await service.ReadArchiveAsync());
    public Task ExportAsync(Stream destination) => ExecuteAsync(async () =>
    { await CsvExporter.WriteAsync(destination, service.Capture().Outages); Message = "Outage CSV exported with UTC timestamps. Empty recovery fields mean no recovery was observed."; });
    public void ReportError(string error) => Message = error;

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
        finally { busy = false; Refresh(); }
    }
    public void Refresh()
    {
        var previousState = snapshot.State;
        snapshot = service.Capture();
        // Reusing a target ID after an address edit must never attach an old measurement to the new address.
        if (!resultsMatchConfiguration) snapshot = snapshot with { Groups = Array.Empty<GroupSnapshot>(), LastMeasurementUtc = null };
        foreach (var notification in alertPolicy.Select(snapshot.Events, SavedPreferences))
        {
            AlertBanner = $"In-app alert — {notification.Title}: {notification.Body}";
            if (SavedPreferences.NativeNotifications) alertDelivery.Enqueue(notification);
        }
        if (service.PersistenceError is { } storageError && storageError != lastStorageAlert && SavedPreferences.NotifyErrors)
        {
            lastStorageAlert = storageError; AlertBanner = $"In-app storage alert — {storageError}";
            if (SavedPreferences.NativeNotifications) alertDelivery.Enqueue(new(Guid.NewGuid(), "Synups: evidence storage error", storageError, MonitorEventKind.ProbeError));
        }
        if (service.PersistenceError is null) lastStorageAlert = "";
        Changed(nameof(NativeAlertStatus)); Changed(nameof(CanPreferences));
        foreach (var g in Groups) g.Apply(snapshot.Groups.FirstOrDefault(s => s.Id == g.Id), IsRunning);
        if (seenHistory != snapshot.HistoryRevision)
        {
            foreach (var removed in Outages.Where(o => !snapshot.Outages.Any(r => r.Id == o.Id)).ToArray()) Outages.Remove(removed);
            foreach (var record in snapshot.Outages)
            {
                var row = Outages.FirstOrDefault(o => o.Id == record.Id);
                if (row is null) Outages.Add(new(record)); else row.Apply(record);
            }
            // No window replacement: the bounded event viewport changes only when the event sequence changes.
            if (!Activity.Select(e => e.Sequence).SequenceEqual(snapshot.Events.Select(e => e.Sequence)))
            {
                while (Activity.Count > 0 && !snapshot.Events.Any(e => e.Sequence == Activity[0].Sequence)) Activity.RemoveAt(0);
                var last = Activity.LastOrDefault()?.Sequence ?? 0;
                foreach (var e in snapshot.Events.Where(e => e.Sequence > last)) Activity.Add(e);
            }
            seenHistory = snapshot.HistoryRevision;
        }
        if (snapshot.Error is { } error) Message = error;
        else if (previousState == SessionState.Stopping && snapshot.State == SessionState.Stopped)
            Message = "Monitoring has finished stopping. Restart and editing are available.";
        Changed(nameof(MonitoringState)); Changed(nameof(OverviewSummary)); Changed(nameof(ConnectivityAssessment)); Changed(nameof(LastMeasurement));
        Changed(nameof(HasNoOutages)); Changed(nameof(HistoryStatus)); NotifyControls();
    }
    private void NotifyControls()
    {
        foreach (var name in new[] { nameof(IsRunning), nameof(CanEdit), nameof(CanStart), nameof(CanStop), nameof(CanRecover), nameof(CanReload), nameof(CanComment), nameof(CanExport), nameof(CanPreferences) }) Changed(name);
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        // Preserve ownership if Stop times out: caller must keep window open and retry instead of abandoning workers.
        await service.StopAsync(); await WriteAutomaticLogsAsync();
        await service.DisposeAsync(); await alertDelivery.DisposeAsync(); disposed = true; Refresh();
    }
}
