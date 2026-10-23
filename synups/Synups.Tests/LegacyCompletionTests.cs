using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Core.Alerts;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class LegacyCompletionTests
{
    [Theory]
    [InlineData("/Minimize")]
    [InlineData("/mINIMIZE")]
    [InlineData("--minimized")]
    public void MinimizeIsExplicitAndDoesNotEnableMonitoring(string flag)
    {
        var directory=Path.GetFullPath(Path.GetTempPath());
        var options=DesktopOptions.Parse([flag,"--data-directory",directory]);
        Assert.True(options.LaunchMinimized); Assert.Null(options.VerificationDirectory);
        Assert.Equal(Path.TrimEndingDirectorySeparator(directory),options.DataDirectory);
        Assert.False(DesktopOptions.Parse([]).LaunchMinimized);
        Assert.Throws<ArgumentException>(()=>DesktopOptions.Parse([flag,"--minimized"]));
        Assert.Throws<ArgumentException>(()=>DesktopOptions.Parse([flag,"unexpected"]));
    }
    [Fact]
    public async Task ConfirmedChangeUsesMonotonicTimeAndNoInventedInitialChange()
    {
        var clock=new ManualClock();var monitor=new GroupMonitor(Group(),Guid.NewGuid(),new ScriptedProbe(Fail,Fail,Error,Ok),clock);
        var first=await monitor.PollAsync(TestContext.Current.CancellationToken);Assert.Null(first.Snapshot.ConfirmedStateChangedUtc);
        clock.Advance(1000);var confirmed=await monitor.PollAsync(TestContext.Current.CancellationToken);Assert.True(confirmed.OutageStarted);
        Assert.Equal(clock.UtcNow,confirmed.Snapshot.ConfirmedStateChangedUtc);Assert.Equal(TimeSpan.Zero,confirmed.Snapshot.SinceConfirmedStateChange);
        clock.JumpUtc(TimeSpan.FromDays(-2));clock.Advance(1000);var unknown=await monitor.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(confirmed.Snapshot.ConfirmedStateChangedUtc,unknown.Snapshot.ConfirmedStateChangedUtc);Assert.Equal(TimeSpan.FromSeconds(1),unknown.Snapshot.SinceConfirmedStateChange);
        clock.Advance(1000);var recovered=await monitor.PollAsync(TestContext.Current.CancellationToken);Assert.True(recovered.Recovered);
        Assert.Equal(clock.UtcNow,recovered.Snapshot.ConfirmedStateChangedUtc);Assert.Equal(TimeSpan.Zero,recovered.Snapshot.SinceConfirmedStateChange);
    }
    [Fact]
    public void CopiedFieldsKeepUnknownLatencyAndEscapeRows()
    {
        var row=new TargetRow(Group().Targets[0]);
        Assert.Equal("Not available",row.CopyValues.Single(c=>c.Column=="Response ms").Value);
        row.Apply(new(row.Id,Ok,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,true),true);
        Assert.Equal("3",row.CopyValues.Single(c=>c.Column=="Response ms").Value);
        row.Apply(new(row.Id,Error,DateTimeOffset.UtcNow,null,true),true);
        Assert.Equal("Not available",row.CopyValues.Single(c=>c.Column=="Response ms").Value);
        Assert.Equal("\"a\tb\"\t\"line\n\"\"quoted\"\"\"",ClipboardText.Row([new("A","a\tb"),new("B","line\n\"quoted\"")]));
    }
    [AvaloniaFact]
    public async Task RowValueContextAndKeysPreserveEditorScope()
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-parity-ui-"+Guid.NewGuid());var store=new ConfigurationStore(Path.Combine(root,"config.json"));await store.SaveAsync(Config(Group()));
        var vm=new MainWindowViewModel(new MonitoringService(new ScriptedProbe()),store);var window=new MainWindow{DataContext=vm};
        try
        {
            window.Show();await UntilAsync(()=>vm.CanStart);vm.SelectedPage=1;
            var list=window.FindControl<ListBox>("TargetList")!;list.SelectedItem=vm.Groups[0].Targets[0];
            Assert.NotNull(list.ContextMenu);Assert.Equal(7,list.ContextMenu.Items.Count);
            vm.TargetCopyColumn="Response ms";
            await UntilAsync(()=>window.FindControl<Grid>("Workspace")!.IsEnabled);Assert.Equal("Not available",vm.SelectedTargetValue);
            window.FindControl<Button>("CopyTargetValue")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(()=>vm.Message.Contains("Copied target value",StringComparison.Ordinal));Assert.Equal("Not available",await window.Clipboard!.TryGetTextAsync());
            await UntilAsync(()=>window.FindControl<Grid>("Workspace")!.IsEnabled);
            list.BringIntoView();await UntilAsync(()=>list.ContainerFromIndex(0) is ListBoxItem item && item.Focus(NavigationMethod.Tab));var command=OperatingSystem.IsMacOS()?KeyModifiers.Meta:KeyModifiers.Control;
            window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.C,KeyModifiers=command|KeyModifiers.Shift});
            await UntilAsync(()=>vm.Message.Contains("Copied target row",StringComparison.Ordinal));var copied=await window.Clipboard!.TryGetTextAsync();Assert.Contains("127.0.0.1",copied);Assert.Contains("Not checked",copied);
            await UntilAsync(()=>window.FindControl<Grid>("Workspace")!.IsEnabled && window.FocusManager!.GetFocusedElement() is not null);
            window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.F4});
            Assert.Same(window.FindControl<TextBox>("TargetNameEditor"),window.FocusManager!.GetFocusedElement());
            window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});Assert.NotNull(vm.SelectedTarget);
            list.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});Assert.Null(vm.SelectedTarget);
            Assert.Contains("Not observed",vm.MonitoredTime);
        }
        finally{window.Close();await UntilAsync(()=>!window.IsVisible);await vm.DisposeAsync();Directory.Delete(root,true);}
    }
    [AvaloniaFact]
    public async Task MinimizedWindowRestoresWithoutStartingMonitoring()
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-minimized-"+Guid.NewGuid());var vm=new MainWindowViewModel(root);var window=new MainWindow{DataContext=vm,StartMinimized=true};
        try
        {
            window.Show();await UntilAsync(()=>vm.CanEdit);await UntilAsync(()=>window.WindowState==WindowState.Minimized);
            Assert.False(vm.IsRunning);window.RestoreFromTray();Assert.True(window.IsVisible);Assert.Equal(WindowState.Normal,window.WindowState);
        }
        finally{window.Close();await UntilAsync(()=>!window.IsVisible);await vm.DisposeAsync();Directory.Delete(root,true);}
    }
    [Fact]
    public void PlacementKeepsSecondaryNegativeDisplayAndRejectsInvalidGeometry()
    {
        var areas=new WorkArea[]{new(0,0,1920,1080,1),new(-2560,0,2560,1440,2)};
        var safe=DesktopPolicy.Clamp(new(1200,700,-2500,50,true),areas);
        Assert.Equal(-2500,safe.X);Assert.Equal(40,safe.Y);Assert.True(safe.Maximized);
        Assert.InRange(safe.X+safe.Width*2,-2560,0);Assert.InRange(safe.Y+safe.Height*2,0,1440);
        var fallback=DesktopPolicy.Clamp(new(900,600,100,100,false),[new(0,0,100,100,0),new(0,0,0,100,double.NaN)]);
        Assert.Equal(0,fallback.X);Assert.Equal(1000,fallback.Width);
        Assert.False(DesktopPolicy.HideOnMinimize(new(){MinimizeToTray=true},false));
    }
    [AvaloniaFact]
    public async Task ConfirmationEscapeKeepsDataAndRestoresOwnerFocus()
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-dialog-"+Guid.NewGuid());var store=new ConfigurationStore(Path.Combine(root,"config.json"));await store.SaveAsync(Config(Group()));
        var vm=new MainWindowViewModel(new MonitoringService(new ScriptedProbe()),store);var window=new MainWindow{DataContext=vm};
        try
        {
            window.Show();await UntilAsync(()=>vm.CanStart && window.FindControl<Grid>("Workspace")!.IsEnabled);vm.SelectedPage=1;
            window.FindControl<ListBox>("TargetList")!.SelectedItem=vm.Groups[0].Targets[0];
            var button=window.FindControl<Button>("RemoveTarget")!;button.BringIntoView();await UntilAsync(()=>button.Focus(NavigationMethod.Tab));
            var bytes=await File.ReadAllBytesAsync(store.FileName,TestContext.Current.CancellationToken);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await UntilAsync(()=>window.OwnedWindows.Any());
            var dialog=Assert.Single(window.OwnedWindows);Assert.Equal("Cancel",(dialog.FocusManager!.GetFocusedElement() as Button)?.Content);
            dialog.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});
            await UntilAsync(()=>!dialog.IsVisible);Assert.Single(vm.Groups[0].Targets);
            Assert.Equal(bytes,await File.ReadAllBytesAsync(store.FileName,TestContext.Current.CancellationToken));await UntilAsync(()=>ReferenceEquals(button,window.FocusManager!.GetFocusedElement()));
        }
        finally{foreach(var child in window.OwnedWindows.ToArray())child.Close();window.Close();await UntilAsync(()=>!window.IsVisible);await vm.DisposeAsync();Directory.Delete(root,true);}
    }
}
