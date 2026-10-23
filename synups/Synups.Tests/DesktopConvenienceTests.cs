using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Synups.Core.Alerts;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class DesktopConvenienceTests
{
    [Fact]
    public void EventFocusIsOptInDeduplicatedIndependentAndDoesNotReplayHistory()
    {
        var policy = new AlertPolicy(); var session = Guid.NewGuid();
        MonitorEvent Event(long sequence, MonitorEventKind kind) => new(sequence, session, null, DateTimeOffset.UtcNow, kind, "Test-only event");
        var outage = Event(1, MonitorEventKind.OutageStarted);
        Assert.False(policy.ShouldFocus([outage], false)); Assert.False(policy.ShouldFocus([outage], true));
        var recovery = Event(2, MonitorEventKind.Recovery);
        Assert.Empty(policy.Select([recovery], new() { NotifyRecovery = false }));
        Assert.True(policy.ShouldFocus([outage, recovery], true)); Assert.False(policy.ShouldFocus([outage, recovery], true));
        Assert.False(policy.ShouldFocus([Event(3, MonitorEventKind.ProbeError), Event(4, MonitorEventKind.SessionStopped)], true));
        policy.IgnoreExisting([Event(8, MonitorEventKind.OutageStarted)]);
        Assert.False(policy.ShouldFocus([Event(8, MonitorEventKind.OutageStarted)], true));
    }

    [Fact]
    public async Task NewFocusPreferenceDefaultsOffAndSurvivesRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-focus-prefs-"+Guid.NewGuid()); var path = Path.Combine(root, "prefs.json");
        try
        {
            var store = new PreferencesStore(path); var defaults = (await store.LoadAsync()).Preferences;
            Assert.False(defaults.FocusOnConfirmedEvents);
            await store.SaveAsync(defaults with { FocusOnConfirmedEvents = true });
            Assert.True((await new PreferencesStore(path).LoadAsync()).Preferences.FocusOnConfirmedEvents);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task HelpKeyboardAndCopyUseRealControlsAndPreserveUnknownValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-copy-help-"+Guid.NewGuid()); var store = new ConfigurationStore(Path.Combine(root,"config.json"));
        await store.SaveAsync(Config(Group()));
        var vm = new MainWindowViewModel(new MonitoringService(new ScriptedProbe()), store); var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.CanStart);
            vm.SelectedPage = 1;
            window.FindControl<ListBox>("TargetList")!.SelectedItem = vm.Groups[0].Targets[0];
            window.FindControl<Button>("CopyTargetAddress")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => vm.Message.Contains("Copied target IP", StringComparison.Ordinal));
            Assert.Equal("127.0.0.1", await window.Clipboard!.TryGetTextAsync());
            Assert.Equal("Not checked", vm.SelectedTarget!.Status); Assert.False(vm.IsRunning);
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 });
            Assert.True(vm.IsHelp); Assert.Equal("Help and About", vm.PageTitle);
            Assert.Equal(vm.VersionText, window.FindControl<TextBlock>("ApplicationVersion")!.Text);
            var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.D1, KeyModifiers = modifier });
            Assert.True(vm.IsOverview);
            var utc = DateTimeOffset.UtcNow; var record = new OutageRecord(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"A real-field contract",utc,utc,null,utc,TimeSpan.FromSeconds(2),false,null,"Keep, \"quoted\" comment");
            vm.SelectedOutage = new(record, interrupted: true); vm.SelectedPage = 2;
            window.FindControl<Button>("CopyIncidentDetails")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => vm.Message.Contains("Copied incident details", StringComparison.Ordinal));
            var copied = await window.Clipboard!.TryGetTextAsync(); Assert.Contains(record.Id.ToString(), copied); Assert.Contains(utc.ToString("O"), copied);
            Assert.Contains("Recovery UTC: Not observed", copied); Assert.Contains(record.Comment, copied);
        }
        finally { window.Close(); await UntilAsync(() => !window.IsVisible); await vm.DisposeAsync(); if (Directory.Exists(root)) Directory.Delete(root,true); }
    }

    [AvaloniaFact]
    public async Task FocusOnlyOnNewConfirmedEventsAndPermissionErrorsExplainUncertainty()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-focus-events-"+Guid.NewGuid()); var store = new ConfigurationStore(Path.Combine(root,"config.json"));
        await store.SaveAsync(Config(Group())); var clock = new ManualClock(); var engine = new MonitoringService(new ScriptedProbe(Error, Fail, Fail, Ok), clock);
        var vm = new MainWindowViewModel(engine, store); var focus = 0; vm.FocusRequested += (_,_) => focus++;
        try
        {
            await vm.InitializeAsync(); vm.PrefFocus = true; vm.PrefOutage = false; vm.PrefRecovery = false; await vm.SavePreferencesAsync();
            await vm.StartAsync(); await UntilAsync(() => clock.PendingDelays == 1); vm.Refresh();
            Assert.Contains("permission", vm.StartGuidance); Assert.Empty(vm.Outages); Assert.Equal(0,focus);
            clock.Advance(1000); await UntilAsync(() => clock.PendingDelays == 1); vm.Refresh(); Assert.Equal(0,focus);
            clock.Advance(1000); await UntilAsync(() => engine.Capture().Outages.Count == 1 && clock.PendingDelays == 1); vm.Refresh(); Assert.Equal(1,focus);
            vm.Refresh(); Assert.Equal(1,focus);
            clock.Advance(1000); await UntilAsync(() => engine.Capture().Outages[0].RecoveredUtc.HasValue); vm.Refresh(); Assert.Equal(2,focus);
            await vm.StopAsync(); vm.Refresh(); Assert.Equal(2,focus);
        }
        finally { await vm.DisposeAsync(); if (Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
