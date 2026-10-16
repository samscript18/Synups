using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

[assembly: AvaloniaTestApplication(typeof(Synups.Tests.TestAppBuilder))]

namespace Synups.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class ShellTests
{
    [AvaloniaFact]
    public async Task ShellLoadsConfigurationDoesNotProbeOnOpenAndDisplaysLiveEngineChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-ui-" + Guid.NewGuid() + ".json");
        var store = new ConfigurationStore(path); await store.SaveAsync(Config(Group()));
        var clock = new ManualClock(); var probe = new ScriptedProbe(Fail, Fail, Ok);
        var engine = new MonitoringService(probe, clock);
        var vm = new MainWindowViewModel(engine, store);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            await UntilAsync(() => vm.CanStart);
            Assert.Empty(probe.Addresses);
            Assert.Equal("Not checked", vm.Groups[0].Targets[0].Status);
            Assert.Equal("—", vm.ConnectedGroupCount);
            Assert.Equal("—", window.FindControl<TextBlock>("ConnectedMetric")!.Text);
            Assert.False(vm.IsCurrentlyConnected);
            Assert.False(vm.HasAlertBanner);
            Assert.Equal("Synups — Network reliability", window.Title);
            var navigation = window.FindControl<ListBox>("Navigation")!;
            navigation.SelectedIndex = 1; Assert.True(vm.IsTargets);
            navigation.SelectedIndex = 2; Assert.True(vm.IsHistory);
            navigation.SelectedIndex = 0; Assert.True(vm.IsOverview);
            var originalRow = vm.Groups[0];
            var originalTarget = originalRow.Targets[0];
            window.FindControl<Button>("StartMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => clock.PendingDelays == 1);
            vm.Refresh();
            Assert.Contains("failure grace", vm.ConnectivityAssessment);
            Assert.False(vm.CanEdit);
            clock.Advance(1000);
            await UntilAsync(() => engine.Capture().Outages.Count == 1);
            vm.Refresh();
            Assert.Single(vm.Outages);
            Assert.Equal("1", vm.ConfirmedOutageCount);
            Assert.True(vm.HasCurrentOutage);
            Assert.Contains("Confirmed", window.FindControl<TextBlock>("ConnectivityAssessment")!.Text);
            vm.SelectedOutage = vm.Outages[0]; vm.OutageComment = "Actual test comment"; await vm.SaveCommentAsync();
            Assert.Equal("Actual test comment", engine.Capture().Outages[0].Comment);
            await UntilAsync(() => clock.PendingDelays == 1); clock.Advance(1000);
            await UntilAsync(() => engine.Capture().Outages[0].RecoveredUtc.HasValue);
            vm.Refresh();
            Assert.Contains("Responded", originalTarget.Status);
            Assert.Equal("1", vm.ConnectedGroupCount);
            Assert.True(vm.IsCurrentlyConnected);
            Assert.True(originalRow.IsConnected);
            Assert.Same(originalRow, vm.Groups[0]); Assert.Same(originalTarget, vm.Groups[0].Targets[0]);
            Assert.Contains("1 connected", window.FindControl<TextBlock>("MonitoringSummary")!.Text);
            await using var csv = new MemoryStream(); await vm.ExportAsync(csv); Assert.True(csv.Length > 0);
            window.FindControl<Button>("StopMonitoring")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => !vm.IsRunning);
            Assert.True(vm.CanEdit);
            Assert.Contains("historical", vm.ConnectivityAssessment);
            Assert.False(vm.IsCurrentlyConnected);
            Assert.False(originalRow.IsConnected);
            Assert.Contains("Last session", vm.MetricContext);
            vm.SelectedGroup = vm.Groups[0]; vm.SelectedTarget = vm.Groups[0].Targets[0];
            var editedId = vm.SelectedTarget.Id;
            vm.TargetAddress = "127.0.0.2";
            await vm.ApplyTargetAsync();
            Assert.Equal(editedId, vm.SelectedTarget!.Id);
            Assert.Equal("Not checked", vm.SelectedTarget.Status);
            Assert.Equal("Not observed", vm.LastMeasurement);
            Assert.Equal("—", vm.ConnectedGroupCount);
            Assert.Single(vm.Outages);
        }
        finally
        {
            await vm.DisposeAsync(); window.Close(); Dispatcher.UIThread.RunJobs();
            await UntilAsync(() => !window.IsVisible);
            if (File.Exists(path)) File.Delete(path);
        }
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task ConfigurationEditorPersistsEditsMovesAndDeletesWithStableIds()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-editor-" + Guid.NewGuid() + ".json");
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe()), new ConfigurationStore(path));
        try
        {
            await vm.InitializeAsync(); Assert.True(vm.HasNoGroups);
            vm.NewGroup(); vm.GroupName = "First"; await vm.ApplyGroupAsync();
            var firstId = Assert.Single(vm.Groups).Id;
            vm.NewTarget(); vm.TargetName = "Local"; vm.TargetAddress = "127.0.0.1";
            await vm.ApplyTargetAsync();
            var targetId = Assert.Single(vm.SelectedGroup!.Targets).Id;
            vm.GroupEnabled = true; vm.PollSeconds = "0.5"; vm.FailSeconds = "2";
            await vm.ApplyGroupAsync(); Assert.Equal(firstId, vm.SelectedGroup!.Id); Assert.True(vm.CanStart);
            vm.NewGroup(); vm.GroupName = "Second"; await vm.ApplyGroupAsync();
            var second = vm.SelectedGroup!;
            vm.SelectedGroup = vm.Groups.Single(g => g.Id == firstId);
            vm.SelectedTarget = Assert.Single(vm.SelectedGroup.Targets);
            vm.TargetGroup = second; vm.TargetName = "Renamed"; await vm.ApplyTargetAsync();
            Assert.Equal(targetId, vm.SelectedTarget!.Id);
            Assert.Equal(second.Id, vm.SelectedGroup!.Id);
            var saved = (await new ConfigurationStore(path).LoadAsync()).Configuration!;
            Assert.False(saved.Groups.Single(g => g.Id == firstId).Enabled);
            Assert.Equal("Renamed", saved.Groups.Single(g => g.Id == second.Id).Targets[0].Name);
            await vm.RemoveTargetAsync(); Assert.Empty(vm.SelectedGroup!.Targets);
            await vm.RemoveGroupAsync(); Assert.Single(vm.Groups);
            vm.TargetAddress = "bad"; vm.TargetName = "Bad"; vm.TargetGroup = vm.SelectedGroup;
            await vm.ApplyTargetAsync(); Assert.Contains("literal", vm.Message);
            Assert.Empty(vm.SelectedGroup!.Targets);
            await vm.RestoreDefaultsAsync(); Assert.Equal(15, vm.Groups.Sum(g => g.Targets.Count));
        }
        finally { await vm.DisposeAsync(); if (File.Exists(path)) File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task CorruptedConfigurationShowsRecoverableErrorAndNeverSilentlyResets()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-ui-corrupt-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        await File.WriteAllTextAsync(path, "{bad", TestContext.Current.CancellationToken);
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe()), new ConfigurationStore(path));
        try
        {
            await vm.InitializeAsync(); Assert.True(vm.CanRecover); Assert.False(vm.CanStart); Assert.False(vm.CanEdit);
            Assert.Contains("untouched", vm.Message);
            await vm.RestoreDefaultsAsync(); // No explicit recovery: must fail safely.
            Assert.True(vm.CanRecover);
            Assert.Equal("{bad", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            await vm.RestoreDefaultsAsync(recoverInvalid: true);
            Assert.False(vm.CanRecover); Assert.True(vm.CanStart);
            Assert.Single(Directory.GetFiles(directory, "*.bak"));
        }
        finally { await vm.DisposeAsync(); Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task ClosingDuringProbeDrainsWorkerBeforeWindowDisappears()
    {
        var path = Path.Combine(Path.GetTempPath(), "Synups-close-" + Guid.NewGuid() + ".json");
        var store = new ConfigurationStore(path); await store.SaveAsync(Config(Group()));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = false;
        var probe = new DelegateProbe(async (_, _, token) =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); return Ok; }
            finally { drained = true; }
        });
        var engine = new MonitoringService(probe); var vm = new MainWindowViewModel(engine, store);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.CanStart); await vm.StartAsync();
            await started.Task.WaitAsync(TestContext.Current.CancellationToken);
            window.Close();
            await UntilAsync(() => !window.IsVisible);
            Assert.True(drained); Assert.Equal(SessionState.Stopped, engine.Capture().State);
            Assert.Empty(engine.Capture().Outages);
        }
        finally { await vm.DisposeAsync(); window.Close(); if (File.Exists(path)) File.Delete(path); }
    }
}
