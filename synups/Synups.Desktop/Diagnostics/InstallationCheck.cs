using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop.Integrations;

namespace Synups.Desktop.Diagnostics;

/// <summary>
/// Opt-in product installation check. Drives the published app's actual controls and normal composition.
/// It never substitutes a probe/clock/store, and refuses normal user data or non-loopback profiles.
/// This is programmatic UI automation, not physical OS input or accessibility certification.
/// </summary>
public sealed class InstallationCheck(DesktopOptions options)
{
    private const string Marker = "Synups isolated installation check v1\n";
    private readonly List<object> actions = new();
    private readonly DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private bool completed;
    private int entered;
    private bool prepared;
    private string? failure;
    private string ReportPath => Path.Combine(options.VerificationDirectory!, options.VerificationStage + ".json");
    private void Passed(string action, object? observed = null) => actions.Add(new { action, passed = true, utc = DateTimeOffset.UtcNow, observed });
    public async Task PrepareAsync()
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(options.DataDirectory, Path.GetFullPath(SynupsPaths.DataDirectory), comparison))
            throw new ArgumentException("Installation checking refuses the normal Synups data directory.");
        if (string.Equals(options.DataDirectory, options.VerificationDirectory, comparison)) throw new ArgumentException("Reports must be separate from data.");
        var marker = Path.Combine(options.DataDirectory, ".synups-installation-check");
        if (options.VerificationStage == "exercise")
        {
            if (Directory.Exists(options.DataDirectory) && Directory.EnumerateFileSystemEntries(options.DataDirectory).Any())
                throw new IOException("The initial installation-check data directory must be new or empty.");
            Directory.CreateDirectory(options.DataDirectory);
            await File.WriteAllTextAsync(marker, Marker);
        }
        else if (!File.Exists(marker) || await File.ReadAllTextAsync(marker) != Marker)
            throw new IOException("Restart checks require the launcher's isolated installation-check marker.");
        var config = await new ConfigurationStore(Path.Combine(options.DataDirectory, "configuration.v1.json")).LoadAsync();
        if (config.NeedsRecovery || config.Configuration!.Groups.SelectMany(g => g.Targets).Any(t => t.Address != "127.0.0.1"))
            throw new IOException("Installation checks only permit their validated explicit 127.0.0.1 profile.");
        var preferences = await new PreferencesStore(Path.Combine(options.DataDirectory, "preferences.v1.json"), Path.Combine(options.DataDirectory, "logs")).LoadAsync();
        if (preferences.Error is not null || preferences.Preferences.NativeNotifications ||
            !Path.GetFullPath(preferences.Preferences.LogDirectory).StartsWith(options.DataDirectory + Path.DirectorySeparatorChar, comparison))
            throw new IOException("Installation-check preferences must keep logs inside the isolated directory and native notifications off.");
        Directory.CreateDirectory(options.VerificationDirectory!);
        if (Directory.EnumerateFiles(options.VerificationDirectory!, options.VerificationStage + "*")
            .Any(p => Path.GetFileName(p) != options.VerificationStage + "-console.txt"))
            throw new IOException("This verification stage already has evidence; choose a new report directory.");
        prepared = true;
    }
    public async Task RunAsync(MainWindow window, IClassicDesktopStyleApplicationLifetime desktop, InstanceCoordinator instance)
    {
        if (Interlocked.Exchange(ref entered, 1) != 0) return; // Show after tray restoration can raise Opened again.
        try
        {
            var vm = (MainWindowViewModel)window.DataContext!;
            await UntilAsync(() => window.IsVisible && (vm.CanEdit || vm.IsRunning));
            var native = window.TryGetPlatformHandle();
            Require(native is not null && native.Handle != IntPtr.Zero, "Native OS window handle is missing.");
            Require(window.Icon is not null, "Synups window icon did not load.");
            Passed("actual_native_window_created", new { window.Title, window.IsVisible, window.ClientSize.Width, window.ClientSize.Height, window.RenderScaling,
                native_descriptor = native!.HandleDescriptor, native_handle_present = true, window_icon_loaded = true });
            if (options.VerificationStage == "storage-error")
            {
                Require(!vm.IsRunning && !vm.CanStart && vm.HistoryStatus.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "Read-only database did not produce a safe stopped/error state.");
                Passed("real_read_only_sqlite_error_visible_without_reset", new { vm.HistoryStatus, vm.Message, vm.MonitoringState });
                await CaptureAsync(window, "read-only"); completed = true; window.Close();
                if (vm.SavedPreferences.ConfirmExit) await ConfirmAsync(desktop, "Continue");
                return;
            }
            if (options.VerificationStage == "exercise") await ExerciseAsync(window, desktop, vm);
            else
            {
                await UntilAsync(() => vm.IsRunning && vm.Groups.Any(g => g.Targets.Any(t => t.Status.StartsWith("Responded", StringComparison.Ordinal))));
                var archive = await vm.ReadHistoryAsync();
                Require(archive.Sessions.Count >= 2, "Previous session history did not reload.");
                if (options.VerificationStage is "recover" or "storage-recovered")
                {
                    Require(archive.Sessions.Any(s => s.Interrupted), "Terminated session was not marked interrupted.");
                    Require(archive.Events.Any(e => e.Kind == MonitorEventKind.Interrupted), "Interruption evidence missing.");
                    Require(!archive.Events.Any(e => e.Kind == MonitorEventKind.Recovery), "Restart invented a recovery.");
                }
                else if (options.VerificationStage == "restart")
                    Require(archive.Sessions.Where(s => s.Id != vm.CaptureMonitoring().SessionId).All(s => s.EndedUtc.HasValue && !s.Interrupted), "Clean prior session was not preserved.");
                Passed("saved_configuration_and_opt_in_monitoring_on_launch", new { groups = vm.Groups.Select(g => new { g.Id, g.Name, targets = g.Targets.Select(t => new { t.Id, t.Address }) }), durable_sessions = archive.Sessions.Count });
                Passed("sqlite_native_history_reload", new { interrupted = archive.Sessions.Count(s => s.Interrupted), recovery_events = archive.Events.Count(e => e.Kind == MonitorEventKind.Recovery) });
            }
            await Task.Delay(TimeSpan.FromSeconds(options.ObserveSeconds));
            var measured = vm.CaptureMonitoring();
            Require(measured.LastMeasurementUtc.HasValue && measured.Groups.Any(g => g.Connectivity == GroupConnectivity.Connected), "No real loopback connectivity was observed.");
            Passed("real_icmp_and_live_ui", new { measured.LastMeasurementUtc, summary = window.FindControl<TextBlock>("MonitoringSummary")!.Text,
                targets = measured.Groups.SelectMany(g => g.Targets).Select(t => new { t.Id, t.CheckedAtUtc, t.LastSuccessUtc, t.Result.Outcome, t.Result.LatencyMilliseconds }), diagnostics = vm.CaptureDiagnostics() });
            await CaptureAsync(window, "overview");
            var size = window.ClientSize; window.Width = 820; window.Height = 560; await Task.Delay(200); await CaptureAsync(window, "compact");
            window.Activate();
            var stopControl = window.FindControl<Button>("StopMonitoring")!;
            await UntilAsync(() => stopControl.IsEffectivelyEnabled &&
                (ReferenceEquals(window.FocusManager?.GetFocusedElement(), stopControl) || stopControl.Focus(NavigationMethod.Tab)));
            Require(ReferenceEquals(window.FocusManager?.GetFocusedElement(), stopControl), "Stop control could not receive keyboard-style focus.");
            await Task.Delay(100); // Let the real focus visual reach a rendered frame before capture.
            Passed("programmatic_tab_focus_and_minimum_layout", new { focused = window.FocusManager?.GetFocusedElement()?.GetType().Name, width = window.ClientSize.Width, height = window.ClientSize.Height });
            await CaptureAsync(window, "focus"); window.Width = size.Width; window.Height = size.Height;
            if (options.VerificationStage == "interruption")
            {
                Passed("ready_for_external_process_termination", new { measured.SessionId });
                await WriteReportAsync("ready-for-termination", null);
                Console.WriteLine("SYNUPS_INSTALLATION_READY_FOR_TERMINATION");
                await Task.Delay(Timeout.Infinite); return; // Launcher terminates only this isolated child; no simulated network outage.
            }
            await ClickAsync(window, "StopMonitoring"); await UntilAsync(() => vm.CanEdit && !vm.IsRunning);
            Require(vm.CaptureDiagnostics().ActiveGroupWorkers == 0, "Monitoring workers remain after Stop.");
            Passed("stop_drains_workers_without_recovery", vm.CaptureDiagnostics());
            if (options.VerificationStage == "exercise")
            {
                // There are deliberately no fabricated outage rows. The native file picker is not automated.
                await using (var csv = new FileStream(Path.Combine(options.VerificationDirectory!, "exercise-empty-outages.csv"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await vm.ExportAsync(csv);
                Passed("csv_export_service_empty_header", new { outage_rows = vm.Outages.Count, native_file_picker = "not automated" });
                vm.SelectedPage = 3;
                Check(window, "PrefTopmost", true); Check(window, "PrefConfirmExit", true); Check(window, "PrefTray", true);
                Check(window, "PrefAutoStart", true); Check(window, "PrefOutage", false); Check(window, "PrefRecovery", false); Check(window, "PrefErrors", true);
                await ClickAsync(window, "ApplyPreferences"); await UntilAsync(() => vm.CanPreferences && vm.SavedPreferences.MonitorOnLaunch && window.Topmost);
                Require(!vm.SavedPreferences.NotifyOutages && !vm.SavedPreferences.NotifyRecovery && vm.SavedPreferences.NotifyErrors, "Alert preferences did not persist.");
                Passed("preferences_topmost_alerts_and_explicit_autostart_saved", new { window.Topmost, vm.SavedPreferences.MonitorOnLaunch, vm.SavedPreferences.ConfirmExit, vm.NativeAlertStatus });
                await CaptureAsync(window, "preferences");
            }
            window.WindowState = WindowState.Minimized; await Task.Delay(150);
            var wasVisible = window.IsVisible;
            var previousActivations = instance.ActivationCount;
            var executable = Environment.ProcessPath ?? throw new IOException("No process apphost path.");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("--data-directory"); start.ArgumentList.Add(options.DataDirectory);
            using (var second = Process.Start(start) ?? throw new IOException("Secondary apphost did not start."))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                var stdout = second.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = second.StandardError.ReadToEndAsync(timeout.Token);
                await second.WaitForExitAsync(timeout.Token); await Task.WhenAll(stdout, stderr);
                Require(second.ExitCode == 0, "Secondary apphost failed: " + await stderr);
            }
            await UntilAsync(() => instance.ActivationCount > previousActivations && window.IsVisible && window.WindowState == WindowState.Normal);
            Passed("second_actual_apphost_restores_existing_instance", new { minimized_visible = wasVisible, activation_count = instance.ActivationCount, window.IsVisible, window.WindowState });
            // This checks the real confirmation buttons; it does not replace the confirmation delegate.
            if (vm.SavedPreferences.ConfirmExit)
            {
                window.Close(); await ConfirmAsync(desktop, "Cancel"); await UntilAsync(() => window.IsVisible && vm.CanStart && !window.IsClosingPending);
                Passed("exit_confirmation_cancel_keeps_window_open");
            }
            completed = true;
            window.Close();
            if (vm.SavedPreferences.ConfirmExit) await ConfirmAsync(desktop, "Continue");
            Passed("normal_close_requested", "External launcher must verify exit code and durable session end.");
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
            if (window.IsVisible) await CaptureAsync(window, "failure");
            Console.Error.WriteLine("Synups installation check failed: " + ex.Message);
            await WriteReportAsync("failed", null);
            try { window.Close(); if (desktop.Windows.Any(w => w != window && w.IsVisible)) await ConfirmAsync(desktop, "Continue"); }
            catch (Exception closeError) { Console.Error.WriteLine("Installation-check close needs attention: " + closeError.Message); }
        }
    }
    private async Task ExerciseAsync(MainWindow window, IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel vm)
    {
        Require(vm.HasNoGroups && !vm.IsRunning && vm.LastMeasurement == "Not observed", "Fresh startup was not passive and empty.");
        Passed("passive_startup_without_network_configuration"); vm.SelectedPage = 1;
        await ClickAsync(window, "NewGroup"); Text(window, "GroupNameEditor", "Installation loopback"); Text(window, "PollSecondsEditor", "0.5");
        Text(window, "FailSecondsEditor", "1"); Text(window, "TimeoutEditor", "500"); await ClickAsync(window, "ApplyGroup");
        await UntilAsync(() => vm.Groups.Count == 1 && vm.CanEdit);
        await ClickAsync(window, "NewTarget"); Text(window, "TargetNameEditor", "This computer"); Text(window, "TargetAddressEditor", "127.0.0.1");
        await ClickAsync(window, "ApplyTarget"); await UntilAsync(() => vm.Groups[0].Targets.Count == 1 && vm.CanEdit);
        var groupId = vm.Groups[0].Id; var targetId = vm.Groups[0].Targets[0].Id;
        await ClickAsync(window, "NewTarget"); Text(window, "TargetNameEditor", "Temporary target"); Text(window, "TargetAddressEditor", "127.0.0.1");
        await ClickAsync(window, "ApplyTarget"); await UntilAsync(() => vm.Groups[0].Targets.Count == 2 && vm.CanEdit);
        var temporaryId = vm.SelectedTarget!.Id;
        Text(window, "TargetNameEditor", "Edited temporary target"); await ClickAsync(window, "ApplyTarget");
        await UntilAsync(() => vm.SelectedTarget?.Name == "Edited temporary target" && vm.CanEdit);
        Require(vm.SelectedTarget!.Id == temporaryId, "Target edit changed identity.");
        await ClickAsync(window, "RemoveTarget"); await ConfirmAsync(desktop, "Continue"); await UntilAsync(() => vm.Groups[0].Targets.Count == 1 && vm.CanEdit);
        Text(window, "GroupNameEditor", "Published application loopback"); Check(window, "GroupEnabledEditor", true); await ClickAsync(window, "ApplyGroup");
        await UntilAsync(() => vm.Groups[0].Name == "Published application loopback" && vm.CanStart);
        await ClickAsync(window, "NewGroup"); Text(window, "GroupNameEditor", "Temporary group"); await ClickAsync(window, "ApplyGroup");
        await UntilAsync(() => vm.Groups.Count == 2 && vm.CanEdit);
        await ClickAsync(window, "RemoveGroup"); await ConfirmAsync(desktop, "Continue"); await UntilAsync(() => vm.Groups.Count == 1 && vm.CanEdit);
        window.FindControl<ListBox>("GroupList")!.SelectedItem = vm.Groups[0];
        window.FindControl<ListBox>("TargetList")!.SelectedItem = vm.Groups[0].Targets[0];
        var before = await File.ReadAllBytesAsync(vm.ConfigurationPath);
        Text(window, "TargetAddressEditor", "invalid-address"); await ClickAsync(window, "ApplyTarget");
        await UntilAsync(() => vm.Message.Contains("literal", StringComparison.Ordinal) && vm.CanEdit);
        var after = await File.ReadAllBytesAsync(vm.ConfigurationPath);
        Require(before.SequenceEqual(after), "Invalid input changed persisted configuration.");
        Text(window, "TargetAddressEditor", "127.0.0.1"); await ClickAsync(window, "ApplyTarget"); await UntilAsync(() => vm.CanStart);
        Require(vm.Groups[0].Id == groupId && vm.Groups[0].Targets[0].Id == targetId, "Stable identities were lost.");
        Passed("bound_controls_crud_groups_targets_validation_and_stable_ids", new { group_id = groupId, target_id = targetId });
        vm.SelectedPage = 0; await ClickAsync(window, "StartMonitoring");
        await UntilAsync(() => vm.IsRunning && vm.Groups[0].Targets[0].Status.StartsWith("Responded", StringComparison.Ordinal));
    }
    private async Task CaptureAsync(Window window, string name)
    {
        using var image = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
        image.Render(window);
        using var encoded = new MemoryStream(); image.Save(encoded, PngBitmapEncoderOptions.Default);
        await File.WriteAllBytesAsync(Path.Combine(options.VerificationDirectory!, options.VerificationStage + "-" + name + ".png"), encoded.ToArray());
    }
    private static void Text(Window window, string name, string value) => window.FindControl<TextBox>(name)!.Text = value;
    private static void Check(Window window, string name, bool value) => window.FindControl<CheckBox>(name)!.IsChecked = value;
    private static async Task ClickAsync(Window window, string name)
    {
        var button = (window is MainWindow ? window.FindControl<Button>(name) : FindButton(window, name)) ?? throw new InvalidOperationException("Missing actual UI control: " + name);
        await UntilAsync(() => button.IsEffectivelyEnabled);
        button.BringIntoView();
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static Button? FindButton(Window window, string name) => window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name);
    private static async Task ConfirmAsync(IClassicDesktopStyleApplicationLifetime desktop, string content)
    {
        await UntilAsync(() => desktop.Windows.Any(w => FindButton(w, content == "Continue" ? "ConfirmContinue" : "ConfirmCancel") is { IsVisible: true }));
        var dialog = desktop.Windows.First(w => FindButton(w, content == "Continue" ? "ConfirmContinue" : "ConfirmCancel") is not null);
        await ClickAsync(dialog, content == "Continue" ? "ConfirmContinue" : "ConfirmCancel");
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var time = Stopwatch.StartNew();
        while (!condition())
        {
            if (time.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("An actual desktop interaction did not reach its expected state.");
            await Task.Delay(50);
        }
    }
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    public async Task<int> FinishAsync(int exitCode)
    {
        var success = completed && failure is null && exitCode == 0;
        await WriteReportAsync(success ? "passed" : "failed", exitCode); return success ? 0 : 1;
    }
    public async Task RecordStartupFailureAsync(Exception exception)
    { failure = exception.ToString(); if (prepared) await WriteReportAsync("startup-failed", 1); }
    private async Task WriteReportAsync(string status, int? exitCode)
    {
        var executable = Environment.ProcessPath;
        var report = new
        {
            status, stage = options.VerificationStage, started_utc = startedUtc, recorded_utc = DateTimeOffset.UtcNow, elapsed_seconds = elapsed.Elapsed.TotalSeconds,
            executable, apphost_sha256 = executable is null ? null : Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable))).ToLowerInvariant(),
            assembly_informational_version = typeof(InstallationCheck).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            process_id = Environment.ProcessId, architecture = RuntimeInformation.ProcessArchitecture.ToString(), rid = RuntimeInformation.RuntimeIdentifier,
            os = RuntimeInformation.OSDescription, runtime = Environment.Version.ToString(), data_directory = options.DataDirectory, exit_code = exitCode,
            core_library_path = typeof(object).Assembly.Location,
            actual_ui_actions = actions, error = failure,
            limitations = new[] { "Programmatic real-control input; physical mouse/keyboard and screen-reader behavior not certified.", "Native file picker and physical tray/menu clicks not automated.", "Loopback only; no external network outage, OS notification delivery or physical power-loss claim.", "Client-area captures have no OS window chrome." }
        };
        var temporary = ReportPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.Move(temporary, ReportPath, true);
    }
}
