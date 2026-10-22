using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Synups.Core.Diagnostics;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class DiagnosticUiTests
{
    private static string NewDirectory() => Path.Combine(Path.GetTempPath(),"Synups-diagnostic-ui-"+Guid.NewGuid());
    private sealed class StubRunner(IDiagnosticRepository repository,bool block=false) : IDiagnosticRunner
    {
        public int Calls { get; private set; }
        public DiagnosticRequest? Request { get; private set; }
        public bool Cancelled { get; private set; }
        public async Task<DiagnosticSession> RunAsync(DiagnosticRequest request,IProgress<DiagnosticProgress>? progress=null,CancellationToken cancellation=default)
        {
            Calls++; Request=request;
            var session=DiagnosticSession.Start(request);
            progress?.Report(new(request.Id,DiagnosticKind.TcpConnect,"Connecting",0,1));
            try { if(block) await Task.Delay(Timeout.Infinite,cancellation); }
            catch(OperationCanceledException) { Cancelled=true; }
            session=session with { CompletedUtc=DateTimeOffset.UtcNow,State=Cancelled?DiagnosticState.Cancelled:DiagnosticState.Completed,Results=request.Tests.Select(kind=>new DiagnosticTestResult(Guid.NewGuid(),kind,request.Target,session.StartedUtc,DateTimeOffset.UtcNow,Cancelled?DiagnosticOutcome.Cancelled:DiagnosticOutcome.Success,"Injected deterministic UI fixture",1,[])).ToArray() };
            await repository.SaveDiagnosticSessionAsync(session);
            return session;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [AvaloniaFact]
    public async Task DiagnosticsNavigationAndDraftDoNotProbeAndRejectUnsafeParameters()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository);
        var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); vm.SelectedPage=5;
            Assert.True(vm.IsDiagnostics); Assert.Equal("Help and About",vm.NavigationItems[4].Title); Assert.Equal(0,runner.Calls);
            Assert.Contains("Cannot run",vm.DiagnosticPreview);
            vm.DiagnosticTarget="127.0.0.1"; vm.DiagnosticPort="70000"; Assert.Throws<ArgumentException>(vm.PrepareDiagnosticRequest);
            vm.DiagnosticPort="443"; vm.DiagnosticResolver="8.8.8.8";
            var request=vm.PrepareDiagnosticRequest(); Assert.True(request.Consent.AlternativeResolverApproved);
            Assert.False(request.Parameters.ResolveHopNames); vm.DiagnosticResolveNames=true; Assert.Contains("PTR queries",vm.DiagnosticPreview);
            vm.DiagnosticReverseTimeout="2001"; Assert.Throws<ArgumentException>(vm.PrepareDiagnosticRequest); vm.DiagnosticReverseTimeout="500";
            await vm.RunDiagnosticAsync(request); Assert.Equal(0,runner.Calls); Assert.Contains("Confirm",vm.DiagnosticStatus);
            vm.DiagnosticTarget="127.0.0.1/24"; Assert.Throws<ArgumentException>(vm.PrepareDiagnosticRequest);
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task DiagnosticsRemainCancellableWhileMonitoringStopIsAvailableAndPersistCancelledEvidence()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository,true); var probe=new ScriptedProbe();
        var vm=new MainWindowViewModel(new MonitoringService(probe,history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); vm.DiagnosticTarget="127.0.0.1";
            var request=vm.PrepareDiagnosticRequest(); var running=vm.RunDiagnosticAsync(request,true);
            Assert.True(vm.IsDiagnosticRunning); Assert.False(vm.CanRunDiagnostic);
            await vm.StopAsync(); Assert.True(vm.IsDiagnosticRunning); Assert.Empty(probe.Addresses);
            vm.CancelDiagnostic(); await running;
            Assert.True(runner.Cancelled); Assert.False(vm.IsDiagnosticRunning); Assert.True(vm.CanRunDiagnostic);
            Assert.Equal(DiagnosticState.Cancelled,Assert.Single(vm.DiagnosticHistory).State); Assert.All(vm.DiagnosticResults,r=>Assert.Contains("Cancelled",r.Title));
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task CloseCancelsOwnedDiagnosticWorkAndWaitsForEvidence()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository,true);
        var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); vm.DiagnosticTarget="127.0.0.1"; var running=vm.RunDiagnosticAsync(vm.PrepareDiagnosticRequest(),true);
            await vm.DisposeAsync(); await running; Assert.True(runner.Cancelled); Assert.False(vm.IsDiagnosticRunning);
            await using var reopened=new SqliteHistoryStore(Path.Combine(dir,"history.db")); await reopened.InitializeDiagnosticsAsync();
            Assert.Equal(DiagnosticState.Cancelled,Assert.Single(await reopened.ReadDiagnosticSessionsAsync()).State);
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task CompletedSessionBaselineAndCustomProfileReopenWithoutNetworkActivity()
    {
        var dir=NewDirectory(); var path=Path.Combine(dir,"history.db"); var config=Path.Combine(dir,"configuration.json");
        var repository=new SqliteHistoryStore(path); var runner=new StubRunner(repository); var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(config),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); vm.DiagnosticTarget="127.0.0.1"; vm.DiagnosticProfileLabel="Local server"; await vm.SaveDiagnosticProfileAsync();
            await vm.RunDiagnosticAsync(vm.PrepareDiagnosticRequest(),true); Assert.Single(vm.DiagnosticHistory);
            await vm.SaveDiagnosticBaselineAsync(); vm.SelectedDiagnosticBaseline=Assert.Single(vm.DiagnosticBaselines); await vm.CompareDiagnosticAsync();
            Assert.NotEqual("No comparison selected.",vm.DiagnosticComparisonText);
            await vm.PreviewDiagnosticEvidenceAsync(); Assert.True(vm.HasDiagnosticEvidencePreview);
            Assert.DoesNotContain("127.0.0.1",vm.DiagnosticEvidencePreview);
            vm.DiagnosticRedact=false; Assert.False(vm.HasDiagnosticEvidencePreview);
            await vm.DisposeAsync();
            var repo2=new SqliteHistoryStore(path); var runner2=new StubRunner(repo2);
            await using var reopened=new MainWindowViewModel(new MonitoringService(history:repo2),new ConfigurationStore(config),diagnosticRunner:runner2);
            await reopened.InitializeAsync(); reopened.SelectedDiagnosticHistory=Assert.Single(reopened.DiagnosticHistory); await reopened.OpenDiagnosticHistoryAsync();
            Assert.Equal(3,reopened.DiagnosticResults.Count); Assert.Equal(0,runner2.Calls); Assert.Contains(reopened.DiagnosticProfiles,p=>p.Label=="Local server"); Assert.Single(reopened.DiagnosticBaselines);
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task IncidentDiagnoseUsesActualIdAndRequiresNewExplicitEndpoint()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository);
        await using var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); var id=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
            vm.SelectedOutage=new OutageRow(new(id,Guid.NewGuid(),Guid.NewGuid(),"Group is not endpoint",now,now,null,null,TimeSpan.Zero,false,null,""));
            vm.DiagnosticTarget="previous.example"; vm.DiagnoseSelectedIncident();
            Assert.Equal(5,vm.SelectedPage); Assert.Equal("",vm.DiagnosticTarget); Assert.Throws<ArgumentException>(vm.PrepareDiagnosticRequest);
            vm.DiagnosticTarget="127.0.0.1"; Assert.Equal(id,vm.PrepareDiagnosticRequest().IncidentId); Assert.Equal(0,runner.Calls);
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task CorruptCustomProfileIsPreservedWhenSavingAnotherProfile()
    {
        var dir=NewDirectory(); Directory.CreateDirectory(dir); var config=Path.Combine(dir,"configuration.json"); var profilePath=config+".diagnostic-profiles.json";
        await File.WriteAllTextAsync(profilePath,"{broken");
        var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository);
        await using var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(config),diagnosticRunner:runner);
        try
        {
            await vm.InitializeAsync(); vm.DiagnosticTarget="127.0.0.1"; await vm.SaveDiagnosticProfileAsync();
            Assert.Equal("{broken",await File.ReadAllTextAsync(profilePath)); Assert.Contains("unreadable",vm.DiagnosticStatus); Assert.Equal(0,runner.Calls);
        }
        finally { await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task ConfirmedRunUsesExactPreviewSnapshotEvenIfDraftChanges()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository);
        var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        var window=new MainWindow { DataContext=vm };
        try
        {
            window.Show(); await UntilAsync(()=>vm.CanEdit); vm.SelectedPage=5;
            window.FindControl<TextBox>("DiagnosticTarget")!.Text="127.0.0.1";
            window.FindControl<Button>("RunDiagnostic")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(()=>window.OwnedWindows.Count>0); var modal=Assert.Single(window.OwnedWindows);
            vm.DiagnosticTarget="127.0.0.2";
            modal.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ConfirmContinue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(()=>runner.Calls==1 && !vm.IsDiagnosticRunning && vm.DiagnosticHistory.Count==1);
            Assert.Equal("127.0.0.1",runner.Request!.Target); Assert.Equal(3,vm.DiagnosticResults.Count);
        }
        finally { window.Close(); await UntilAsync(()=>!window.IsVisible); await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [AvaloniaFact]
    public async Task ActualDiagnosticsPanelOpensConfirmationAndCancelDoesNotRun()
    {
        var dir=NewDirectory(); var repository=new SqliteHistoryStore(Path.Combine(dir,"history.db")); var runner=new StubRunner(repository);
        var vm=new MainWindowViewModel(new MonitoringService(history:repository),new ConfigurationStore(Path.Combine(dir,"configuration.json")),diagnosticRunner:runner);
        var window=new MainWindow { DataContext=vm };
        try
        {
            window.Show(); await UntilAsync(()=>vm.CanEdit); vm.SelectedPage=5; vm.DiagnosticTarget="127.0.0.1"; window.UpdateLayout();
            Assert.True(window.FindControl<TextBox>("DiagnosticTarget")!.IsEffectivelyVisible);
            window.FindControl<Button>("RunDiagnostic")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(()=>window.OwnedWindows.Count>0);
            var modal=Assert.Single(window.OwnedWindows); var cancel=Assert.IsType<Button>(modal.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ConfirmCancel")); cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(()=>window.OwnedWindows.Count==0); Assert.Equal(0,runner.Calls);
        }
        finally { window.Close(); await UntilAsync(()=>!window.IsVisible); await vm.DisposeAsync(); if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }
}
