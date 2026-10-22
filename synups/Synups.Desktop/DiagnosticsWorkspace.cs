using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Synups.Core.Diagnostics;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    private IDiagnosticRepository? diagnosticRepository;
    private IDiagnosticRunner? diagnosticRunner;
    private bool ownsDiagnosticRunner, diagnosticRunning, diagnosticsClosing;
    private CancellationTokenSource? diagnosticCancellation;
    private Task diagnosticTask = Task.CompletedTask;
    private DiagnosticSession? diagnosticSession;
    private DiagnosticSessionSummary? selectedDiagnosticHistory;
    private DiagnosticBaseline? selectedDiagnosticBaseline;
    private Guid? diagnosticIncidentId;
    private readonly System.Diagnostics.Stopwatch diagnosticClock = new();
    private string diagnosticStatus = "Choose one approved endpoint and review the parameters. Opening this page sends no probes.", diagnosticPhase = "Idle", diagnosticTarget = "", diagnosticPort = "443", diagnosticSamples = "5", diagnosticInterval = "250", diagnosticTimeout = "1000", diagnosticBudget = "60", diagnosticProbeCap = "100", diagnosticHops = "16", diagnosticPerHop = "2", diagnosticResolver = "", diagnosticBaselineLabel = "Known good", diagnosticComparisonText = "No comparison selected.";
    private bool diagnosticResolveNames;
    private string diagnosticReverseTimeout="500";
    public bool DiagnosticResolveNames { get => diagnosticResolveNames; set { if(Set(ref diagnosticResolveNames,value)) DiagnosticDraftChanged(); } }
    public string DiagnosticReverseTimeout { get => diagnosticReverseTimeout; set { if(Set(ref diagnosticReverseTimeout,value ?? "")) DiagnosticDraftChanged(); } }
    private bool diagnosticTcp = true, diagnosticDns, diagnosticPing = true, diagnosticNetwork = true, diagnosticTrace, diagnosticAaaa;
    private DiagnosticProfile selectedDiagnosticProfile = DiagnosticProfile.BuiltIn[0];
    internal DiagnosticSession? CaptureDiagnosticSession() => diagnosticSession;
    public bool IsDiagnostics => SelectedPage == 5;
    public bool IsDiagnosticRunning => diagnosticRunning;
    public bool CanRunDiagnostic => !diagnosticRunning && !diagnosticsClosing && !disposed && diagnosticRunner is not null;
    public bool CanOpenDiagnostic => !diagnosticRunning && SelectedDiagnosticHistory is not null;
    public bool CanUseDiagnosticEvidence => !diagnosticRunning && diagnosticSession is not null && diagnosticRepository is not null;
    public bool CanSaveDiagnosticBaseline => CanUseDiagnosticEvidence && diagnosticSession!.State==DiagnosticState.Completed && diagnosticSession.Results.Count>0 && diagnosticSession.Results.All(r=>r.Outcome==DiagnosticOutcome.Success);
    public bool CanCompareDiagnostic => CanUseDiagnosticEvidence && SelectedDiagnosticBaseline is not null;
    public string DiagnosticStatus { get => diagnosticStatus; private set => Set(ref diagnosticStatus, value); }
    public string DiagnosticPhase { get => diagnosticPhase; private set => Set(ref diagnosticPhase, value); }
    public string DiagnosticElapsed => diagnosticRunning ? $"Elapsed {diagnosticClock.Elapsed.TotalSeconds:F1} s" : diagnosticSession is { } s ? $"Session {s.State} · {(s.CompletedUtc - s.StartedUtc)?.TotalSeconds:F1} s · {s.Request.Id}" : "No session run.";
    public string DiagnosticAnalysis => diagnosticSession is null ? "No diagnostic observations yet." : string.Join("\n",(diagnosticSession.Analysis ?? []).Select(o=>$"[{o.Certainty}] {o.Name}: {o.Value?.ToString("G",CultureInfo.InvariantCulture)} {o.Unit} {o.Text}"));
    public string DiagnosticIncident => diagnosticIncidentId is { } id ? $"Linked incident: {id}. Choose an approved endpoint; the incident group is not a destination." : "Standalone investigation; no incident linked.";
    public string DiagnosticTarget { get => diagnosticTarget; set { if(Set(ref diagnosticTarget,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticPort { get => diagnosticPort; set { if(Set(ref diagnosticPort,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticSamples { get => diagnosticSamples; set { if(Set(ref diagnosticSamples,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticInterval { get => diagnosticInterval; set { if(Set(ref diagnosticInterval,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticTimeout { get => diagnosticTimeout; set { if(Set(ref diagnosticTimeout,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticBudget { get => diagnosticBudget; set { if(Set(ref diagnosticBudget,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticProbeCap { get => diagnosticProbeCap; set { if(Set(ref diagnosticProbeCap,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticHops { get => diagnosticHops; set { if(Set(ref diagnosticHops,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticPerHop { get => diagnosticPerHop; set { if(Set(ref diagnosticPerHop,value ?? "")) DiagnosticDraftChanged(); } }
    public string DiagnosticResolver { get => diagnosticResolver; set { if(Set(ref diagnosticResolver,value ?? "")) DiagnosticDraftChanged(); } }
    public bool DiagnosticTcp { get => diagnosticTcp; set { if(Set(ref diagnosticTcp,value)) DiagnosticDraftChanged(); } }
    public bool DiagnosticDns { get => diagnosticDns; set { if(Set(ref diagnosticDns,value)) DiagnosticDraftChanged(); } }
    public bool DiagnosticPing { get => diagnosticPing; set { if(Set(ref diagnosticPing,value)) DiagnosticDraftChanged(); } }
    public bool DiagnosticNetwork { get => diagnosticNetwork; set { if(Set(ref diagnosticNetwork,value)) DiagnosticDraftChanged(); } }
    public bool DiagnosticTrace { get => diagnosticTrace; set { if(Set(ref diagnosticTrace,value)) DiagnosticDraftChanged(); } }
    public bool DiagnosticAaaa { get => diagnosticAaaa; set { if(Set(ref diagnosticAaaa,value)) DiagnosticDraftChanged(); } }
    public string DiagnosticBaselineLabel { get => diagnosticBaselineLabel; set => Set(ref diagnosticBaselineLabel,value ?? ""); }
    public string DiagnosticComparisonText { get => diagnosticComparisonText; private set => Set(ref diagnosticComparisonText,value); }
    public ObservableCollection<DiagnosticProfile> DiagnosticProfiles { get; } = new(DiagnosticProfile.BuiltIn);
    private string diagnosticProfileLabel="Custom profile";
    public string DiagnosticProfileLabel { get => diagnosticProfileLabel; set => Set(ref diagnosticProfileLabel,value ?? ""); }
    public DiagnosticProfile SelectedDiagnosticProfile
    {
        get => selectedDiagnosticProfile;
        set
        {
            if (value is null || !Set(ref selectedDiagnosticProfile,value)) return;
            DiagnosticTcp=value.Tests.Contains(DiagnosticKind.TcpConnect); DiagnosticDns=value.Tests.Contains(DiagnosticKind.Dns); DiagnosticPing=value.Tests.Contains(DiagnosticKind.PingQuality); DiagnosticNetwork=value.Tests.Contains(DiagnosticKind.NetworkConfiguration); DiagnosticTrace=value.Tests.Contains(DiagnosticKind.TraceRoute);
            var p=value.Parameters; DiagnosticPort=p.Port.ToString(); DiagnosticSamples=p.SampleCount.ToString(); DiagnosticInterval=p.IntervalMilliseconds.ToString(); DiagnosticTimeout=p.ProbeTimeoutMilliseconds.ToString(); DiagnosticBudget=p.SessionTimeoutSeconds.ToString(); DiagnosticProbeCap=p.MaximumProbes.ToString(); DiagnosticHops=p.MaximumHops.ToString(); DiagnosticPerHop=p.ProbesPerHop.ToString(); DiagnosticResolver=p.ResolverAddress ?? ""; DiagnosticAaaa=p.RecordType==DnsRecordType.AAAA; DiagnosticResolveNames=p.ResolveHopNames; DiagnosticReverseTimeout=p.ReverseDnsTimeoutMilliseconds.ToString();
            DiagnosticDraftChanged();
        }
    }
    public ObservableCollection<DiagnosticSessionSummary> DiagnosticHistory { get; } = new();
    public ObservableCollection<DiagnosticBaseline> DiagnosticBaselines { get; } = new();
    public ObservableCollection<DiagnosticResultRow> DiagnosticResults { get; } = new();
    public DiagnosticSessionSummary? SelectedDiagnosticHistory { get => selectedDiagnosticHistory; set { if(Set(ref selectedDiagnosticHistory,value)) DiagnosticControlsChanged(); } }
    public DiagnosticBaseline? SelectedDiagnosticBaseline { get => selectedDiagnosticBaseline; set { if(Set(ref selectedDiagnosticBaseline,value)) DiagnosticControlsChanged(); } }
    private void ConfigureDiagnostics(IDiagnosticRunner? runner, IDiagnosticRepository? repository)
    {
        diagnosticRepository=repository ?? service.DiagnosticRepository;
        diagnosticRunner=runner;
        if(diagnosticRunner is null && diagnosticRepository is not null) { diagnosticRunner=new DiagnosticRunner(diagnosticRepository); ownsDiagnosticRunner=true; }
        if(diagnosticRunner is null) DiagnosticStatus="Diagnostics unavailable: this host did not supply a durable diagnostic repository.";
    }
    private void DiagnosticDraftChanged() => Changed(nameof(DiagnosticPreview));
    private void DiagnosticControlsChanged()
    {
        foreach(var name in new[]{nameof(IsDiagnosticRunning),nameof(CanRunDiagnostic),nameof(CanOpenDiagnostic),nameof(CanUseDiagnosticEvidence),nameof(CanCompareDiagnostic),nameof(CanSaveDiagnosticBaseline),nameof(DiagnosticElapsed),nameof(DiagnosticAnalysis)}) Changed(name);
    }
    public DiagnosticRequest PrepareDiagnosticRequest()
    {
        static int Number(string text,string label) => int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out var n) ? n : throw new ArgumentException($"{label} must be a whole number.");
        var kinds=new List<DiagnosticKind>(); if(DiagnosticTcp) kinds.Add(DiagnosticKind.TcpConnect); if(DiagnosticDns) kinds.Add(DiagnosticKind.Dns); if(DiagnosticPing) kinds.Add(DiagnosticKind.PingQuality); if(DiagnosticTrace) kinds.Add(DiagnosticKind.TraceRoute); if(DiagnosticNetwork) kinds.Add(DiagnosticKind.NetworkConfiguration);
        var p=new DiagnosticParameters { Port=Number(DiagnosticPort,"Port"),SampleCount=Number(DiagnosticSamples,"Samples"),IntervalMilliseconds=Number(DiagnosticInterval,"Interval"),ProbeTimeoutMilliseconds=Number(DiagnosticTimeout,"Probe timeout"),SessionTimeoutSeconds=Number(DiagnosticBudget,"Session budget"),MaximumProbes=Number(DiagnosticProbeCap,"Probe cap"),MaximumHops=Number(DiagnosticHops,"Maximum hops"),ProbesPerHop=Number(DiagnosticPerHop,"Probes per hop"),ResolverAddress=string.IsNullOrWhiteSpace(DiagnosticResolver)?null:DiagnosticResolver.Trim(),RecordType=DiagnosticAaaa?DnsRecordType.AAAA:DnsRecordType.A,ResolveHopNames=DiagnosticResolveNames,ReverseDnsTimeoutMilliseconds=Number(DiagnosticReverseTimeout,"Reverse DNS timeout") };
        var profile=SelectedDiagnosticProfile;
        var exactProfile=profile.Tests.Order().SequenceEqual(kinds.Order()) && profile.Parameters==p;
        return new DiagnosticRequest(Guid.NewGuid(),DiagnosticTarget,kinds,p,new(true,p.ResolverAddress is not null),diagnosticIncidentId,exactProfile?profile.Id:"custom",exactProfile?profile.Version:1).ValidatedCopy();
    }
    public string DiagnosticPreview
    {
        get { try { return DescribeDiagnosticRequest(PrepareDiagnosticRequest()); } catch(Exception ex) { return "Cannot run: "+ex.Message; } }
    }
    public static string DescribeDiagnosticRequest(DiagnosticRequest r) => $"Endpoint: {r.Target} · TCP port {r.Parameters.Port}\nTests: {string.Join(", ",r.Tests)} · profile {r.ProfileId} v{r.ProfileVersion}\nPing samples {r.Parameters.SampleCount}, interval {r.Parameters.IntervalMilliseconds} ms; each probe timeout {r.Parameters.ProbeTimeoutMilliseconds} ms. Session deadline {r.Parameters.SessionTimeoutSeconds} s, maximum {r.Parameters.MaximumProbes} probes. Estimated maximum {r.Tests.Sum(t=>t switch { DiagnosticKind.PingQuality=>r.Parameters.SampleCount, DiagnosticKind.TraceRoute=>r.Parameters.MaximumHops*r.Parameters.ProbesPerHop, DiagnosticKind.NetworkConfiguration=>0, _=>1 })} probes for selected tests. Path: ≤{r.Parameters.MaximumHops} hops × {r.Parameters.ProbesPerHop} probes.\nDNS: {r.Parameters.RecordType}; resolver {r.Parameters.ResolverAddress ?? "system configured resolver"}. Hostname tests may disclose the target to system DNS; an explicit resolver receives the query. TCP performs a connection without application data. ICMP and path responses may be filtered. Protocols: TCP connect, DNS UDP/53 when selected; ICMP echo for ping/path. No external lookup services. ICMP/path may require OS permission; permission failures are recorded, and Synups never elevates automatically.\nHop names: {(r.Parameters.ResolveHopNames?$"enabled, explicit PTR queries disclose responding hop addresses to the configured resolver; timeout {r.Parameters.ReverseDnsTimeoutMilliseconds} ms each; ≤64 distinct hop PTR candidates, cache32, concurrency1; shared session deadline":"disabled; literal hop addresses only, no PTR lookups")}.\nIncident: {r.IncidentId?.ToString() ?? "none"}. Running makes network requests to this exact endpoint; a failed probe does not establish an outage cause.";
    public void DiagnoseSelectedIncident()
    {
        if(IsDiagnosticRunning || SelectedOutage is null) return;
        diagnosticIncidentId=SelectedOutage.Id; DiagnosticTarget=""; SelectedDiagnosticProfile=DiagnosticProfile.BuiltIn.First(p=>p.Id=="incident"); SelectedPage=5; Changed(nameof(DiagnosticIncident)); DiagnosticDraftChanged();
    }
    public void ClearDiagnosticIncident()
    { if(IsDiagnosticRunning) return; diagnosticIncidentId=null; Changed(nameof(DiagnosticIncident)); DiagnosticDraftChanged(); }
    public Task RunDiagnosticAsync(DiagnosticRequest request,bool confirmed=false)
    {
        if(!confirmed) { DiagnosticStatus="Confirm the exact target and parameters before running."; return Task.CompletedTask; }
        if(!CanRunDiagnostic) return Task.CompletedTask;
        return diagnosticTask=RunDiagnosticCoreAsync(request);
    }
    private async Task RunDiagnosticCoreAsync(DiagnosticRequest request)
    {
        try
        {
            request=request.ValidatedCopy(); diagnosticRunning=true; diagnosticClock.Restart(); diagnosticCancellation=new(); DiagnosticResults.Clear(); diagnosticSession=null; diagnosticComparison=null; diagnosticEvidence=null; DiagnosticEvidencePreview="Preview the completed or cancelled session before exporting."; Changed(nameof(HasDiagnosticEvidencePreview)); DiagnosticPhase="Starting"; DiagnosticStatus="Running approved diagnostics…"; DiagnosticControlsChanged();
            var progress=new Progress<DiagnosticProgress>(p=> { if(!diagnosticRunning || p.SessionId!=request.Id) return; DiagnosticPhase=$"{p.Kind?.ToString() ?? "Session"}: {p.Phase} · {p.Completed}/{p.Total}"; Changed(nameof(DiagnosticElapsed)); });
            var clock=UpdateDiagnosticClockAsync(diagnosticCancellation.Token);
            try { ShowDiagnosticSession(await diagnosticRunner!.RunAsync(request,progress,diagnosticCancellation.Token)); }
            finally { diagnosticCancellation.Cancel(); await clock; }
            DiagnosticStatus=$"{diagnosticSession!.State}. Evidence retained with typed outcomes and UTC timestamps.";
        }
        catch(Exception ex) { DiagnosticStatus=$"Diagnostics did not finish: {ex.Message}. Reopen history to review retained partial evidence."; }
        finally
        {
            diagnosticClock.Stop(); diagnosticRunning=false; diagnosticCancellation?.Dispose(); diagnosticCancellation=null; DiagnosticPhase=diagnosticSession?.State.ToString() ?? "Stopped"; DiagnosticControlsChanged(); await RefreshDiagnosticHistoryAsync();
        }
    }
    private async Task UpdateDiagnosticClockAsync(CancellationToken token)
    { try { while(!token.IsCancellationRequested) { await Task.Delay(250,token); Changed(nameof(DiagnosticElapsed)); } } catch(OperationCanceledException) { } }
    public void CancelDiagnostic() { diagnosticCancellation?.Cancel(); if(diagnosticRunning) DiagnosticPhase="Cancelling; retaining completed observations"; }
    private void ShowDiagnosticSession(DiagnosticSession session)
    {
        diagnosticSession=session; diagnosticComparison=null; diagnosticEvidence=null; DiagnosticEvidencePreview="Preview the viewed session before exporting."; Changed(nameof(HasDiagnosticEvidencePreview)); DiagnosticResults.Clear(); foreach(var result in session.Results) DiagnosticResults.Add(new(result)); DiagnosticComparisonText="No comparison selected."; DiagnosticControlsChanged();
    }
    public async Task RefreshDiagnosticHistoryAsync()
    {
        if(diagnosticRepository is null || diagnosticsClosing) return;
        try
        {
            await diagnosticRepository.InitializeDiagnosticsAsync();
            await LoadDiagnosticProfilesAsync();
            var rows=await diagnosticRepository.ReadDiagnosticSessionsAsync(50); var baselines=await diagnosticRepository.ReadDiagnosticBaselinesAsync(50);
            DiagnosticHistory.Clear(); foreach(var r in rows) DiagnosticHistory.Add(r);
            DiagnosticBaselines.Clear(); foreach(var b in baselines) DiagnosticBaselines.Add(b);
        }
        catch(Exception ex) { DiagnosticStatus="Diagnostic history unavailable: "+ex.Message; }
    }
    public async Task OpenDiagnosticHistoryAsync()
    {
        if(!CanOpenDiagnostic || diagnosticRepository is null) return;
        try { var s=await diagnosticRepository.FindDiagnosticSessionAsync(SelectedDiagnosticHistory!.Id); if(s is null) throw new IOException("Session no longer available."); if(IsDiagnosticRunning || diagnosticsClosing) return; ShowDiagnosticSession(s); DiagnosticStatus=$"Reopened {s.Request.Target} · {s.State} · {s.StartedUtc:O}. No probes run."; }
        catch(Exception ex) { DiagnosticStatus=ex.Message; }
    }
    public async Task SaveDiagnosticBaselineAsync()
    {
        if(!CanSaveDiagnosticBaseline) { DiagnosticStatus="Only a completed session with successful results can be saved as a baseline."; return; }
        try
        {
            var label=DiagnosticBaselineLabel.Trim(); if(label.Length is <1 or >120) throw new ArgumentException("Baseline label must contain 1–120 characters.");
            var s=diagnosticSession!; await diagnosticRepository!.SaveDiagnosticBaselineAsync(new(Guid.NewGuid(),s.Request.Id,s.Request.Target,label,DateTimeOffset.UtcNow)); await RefreshDiagnosticHistoryAsync(); DiagnosticStatus="Baseline saved from the viewed session; no probes run.";
        } catch(Exception ex) { DiagnosticStatus=ex.Message; }
    }
    private bool diagnosticRedact=true;
    private string diagnosticEvidencePreview="No evidence preview generated.";
    private EvidenceDocument? diagnosticEvidence;
    private DiagnosticComparison? diagnosticComparison;
    public bool DiagnosticRedact { get => diagnosticRedact; set { if(!Set(ref diagnosticRedact,value)) return; diagnosticEvidence=null; DiagnosticEvidencePreview="Redaction changed; preview again before export."; Changed(nameof(HasDiagnosticEvidencePreview)); } }
    public bool HasDiagnosticEvidencePreview => diagnosticEvidence is not null;
    public string DiagnosticEvidencePreview { get => diagnosticEvidencePreview; private set => Set(ref diagnosticEvidencePreview,value); }
    public async Task CompareDiagnosticAsync()
    {
        if(!CanCompareDiagnostic) return;
        try
        {
            var baseline=SelectedDiagnosticBaseline!; var current=diagnosticSession!;
            var previous=await diagnosticRepository!.FindDiagnosticSessionAsync(baseline.SessionId) ?? throw new IOException("Baseline session is unavailable.");
            if(IsDiagnosticRunning || diagnosticSession?.Request.Id!=current.Request.Id || diagnosticsClosing) return;
            diagnosticComparison=DiagnosticEvidence.Compare(previous,current,baseline.Id);
            await diagnosticRepository.SaveDiagnosticComparisonAsync(diagnosticComparison);
            DiagnosticComparisonText=string.Join("\n",diagnosticComparison.Differences.Select(o=>$"[{o.Certainty}] {o.Name}: {o.Value?.ToString("G",CultureInfo.InvariantCulture)} {o.Unit} {o.Text}"));
            diagnosticEvidence=null; Changed(nameof(HasDiagnosticEvidencePreview));
        } catch(Exception ex) { DiagnosticComparisonText=ex.Message; }
    }
    public async Task PreviewDiagnosticEvidenceAsync()
    {
        if(!CanUseDiagnosticEvidence) return;
        diagnosticEvidence=null; Changed(nameof(HasDiagnosticEvidencePreview));
        try
        {
            var current=diagnosticSession!; var redact=DiagnosticRedact; var comparison=diagnosticComparison;
            var incident=current.Request.IncidentId is { } id ? await service.FindIncidentAsync(id) : null;
            if(IsDiagnosticRunning || diagnosticsClosing || diagnosticSession?.Request.Id!=current.Request.Id || redact!=DiagnosticRedact) return;
            diagnosticEvidence=DiagnosticEvidence.Preview(current,incident,redact,comparison);
            DiagnosticEvidencePreview=DiagnosticEvidence.ToMarkdown(diagnosticEvidence); Changed(nameof(HasDiagnosticEvidencePreview));
        } catch(Exception ex) { DiagnosticEvidencePreview="Preview failed: "+ex.Message; }
    }
    public async Task ExportDiagnosticEvidenceAsync(string directory)
    {
        if(diagnosticEvidence is null) { DiagnosticStatus="Preview evidence before exporting."; return; }
        try { var result=await DiagnosticEvidence.ExportAsync(directory,diagnosticEvidence); DiagnosticStatus="Evidence exported: "+string.Join(", ",result.Files)+". Manifest: "+result.ManifestPath; }
        catch(Exception ex) { DiagnosticStatus="Evidence export failed: "+ex.Message; }
    }
    private bool diagnosticProfilesLoaded;
    private bool diagnosticProfilesInvalid;
    private string DiagnosticProfilePath => store.FileName+".diagnostic-profiles.json";
    private async Task LoadDiagnosticProfilesAsync()
    {
        if(diagnosticProfilesLoaded) return;
        diagnosticProfilesLoaded=true;
        if(!File.Exists(DiagnosticProfilePath)) return;
        try
        {
            var info=new FileInfo(DiagnosticProfilePath); if(info.Length>262144) throw new IOException("Profile file exceeds 256 KiB.");
            var profiles=JsonSerializer.Deserialize<DiagnosticProfile[]>(await File.ReadAllTextAsync(DiagnosticProfilePath)) ?? [];
            foreach(var p in profiles.Take(50))
            {
                new DiagnosticRequest(Guid.NewGuid(),"127.0.0.1",p.Tests,p.Parameters,new(true,p.Parameters.ResolverAddress is not null),ProfileId:p.Id,ProfileVersion:p.Version).ValidatedCopy();
                if(!DiagnosticProfiles.Any(x=>x.Id==p.Id)) DiagnosticProfiles.Add(p);
            }
        } catch(Exception ex) { DiagnosticStatus="Custom profiles unavailable; existing file preserved: "+ex.Message; diagnosticProfilesInvalid=true; }
    }
    public async Task SaveDiagnosticProfileAsync()
    {
        if(!CanRunDiagnostic) return;
        try
        {
            var request=PrepareDiagnosticRequest(); var label=DiagnosticProfileLabel.Trim(); if(label.Length is <1 or >80) throw new ArgumentException("Profile label must be 1–80 characters.");
            await LoadDiagnosticProfilesAsync();
            if(diagnosticProfilesInvalid) throw new IOException("Existing custom profile file is unreadable. Preserve or repair it before saving profiles.");
            var existing=DiagnosticProfiles.Where(p=>!DiagnosticProfile.BuiltIn.Any(b=>b.Id==p.Id)).ToList();
            if(existing.Count>=50) throw new ArgumentException("At most 50 saved custom profiles are supported.");
            var profile=new DiagnosticProfile("custom-"+Guid.NewGuid().ToString("N"),label,1,request.Tests,request.Parameters); existing.Add(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(DiagnosticProfilePath))!);
            var temporary=DiagnosticProfilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(existing)); File.Move(temporary,DiagnosticProfilePath,overwrite:true); }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
            DiagnosticProfiles.Add(profile); SelectedDiagnosticProfile=profile; DiagnosticStatus="Custom profile saved locally. No probes run.";
        } catch(Exception ex) { DiagnosticStatus=ex.Message; }
    }
    private async Task DisposeDiagnosticsAsync()
    {
        diagnosticsClosing=true; CancelDiagnostic();
        try
        {
            await diagnosticTask.WaitAsync(TimeSpan.FromSeconds(7));
            if(ownsDiagnosticRunner && diagnosticRunner is not null) await diagnosticRunner.DisposeAsync();
        }
        catch { diagnosticsClosing=false; DiagnosticControlsChanged(); throw; }
    }
}

public sealed class DiagnosticResultRow(DiagnosticTestResult result)
{
    public string Title => $"{result.Kind} · {result.Outcome} · {result.ElapsedMilliseconds:F1} ms";
    public string Details
    {
        get
        {
            var b=new StringBuilder($"Method: {result.Method}\nUTC {result.StartedUtc:O} → {result.CompletedUtc:O}\n");
            if(result.Error is { } e) b.AppendLine("Error: "+e);
            if(result.Statistics is { } s) b.AppendLine($"Samples attempted {s.Attempted}, completed {s.Completed}, successful {s.Successful}, unanswered {s.Unanswered}; loss {s.LossPercent:F1}%; min/avg/max {s.MinimumMilliseconds:F2}/{s.AverageMilliseconds:F2}/{s.MaximumMilliseconds:F2} ms; population stddev {s.StandardDeviationMilliseconds:F2} ms; adjacent-success jitter {s.JitterMilliseconds:F2} ms; partial {s.Partial}.");
            if(result.Dns is { } d) { b.AppendLine($"Resolver {d.Resolver} · {d.Transport} · {d.QueryType} · RCODE {d.ResponseCode}\n{d.CachePolicy}"); foreach(var a in d.Answers) b.AppendLine($"{a.Name} {a.Type} {a.Address} TTL {a.TtlSeconds} s"); }
            if(result.Hops is { } hops) foreach(var h in hops) b.AppendLine($"Hop {h.Hop}: {(h.Addresses.Count==0?"*":string.Join(", ",h.Addresses))} · RTT {string.Join(", ",h.RttMilliseconds.Select(t=>$"{t:F2}"))} ms · unanswered {h.Unanswered} {h.Hostname}");
            if(result.Network is { } n) { b.AppendLine($"Snapshot provider: {n.Provider}"); foreach(var i in n.Interfaces) b.AppendLine($"{i.Name} · {i.Type} · {i.Status} · addresses {string.Join(", ",i.Addresses)} · gateways {string.Join(", ",i.Gateways)} · DNS {string.Join(", ",i.DnsServers)}"); foreach(var r in n.Routes) b.AppendLine(r); foreach(var l in n.Limitations) b.AppendLine("Limitation: "+l); }
            foreach(var o in result.Observations) b.AppendLine($"[{o.Certainty}] {o.Name}: {o.Value?.ToString("G",CultureInfo.InvariantCulture)} {o.Unit} {o.Text}");
            return b.ToString();
        }
    }
}
