// Engineering tool only; never included in a Synups release. All probes delegate to the real IcmpProbe.
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;

internal static class EngineEndurance
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out var seconds) || seconds is < 10 or > 28800)
            throw new ArgumentException("Expected a new evidence directory and 10–28800 seconds.");
        var folder = Path.GetFullPath(args[0]);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()) throw new IOException("Evidence directory must be empty.");
        Directory.CreateDirectory(folder);
        var historyPath = Path.Combine(folder, "history.v1.db");
        var probe = new CountingProbe();
        var config = new MonitorConfiguration(1, Enumerable.Range(1, 4).Select(i => new GroupConfiguration(Guid.NewGuid(), $"Explicit loopback {i}", true,
            1000, 2000, 500, [new TargetConfiguration(Guid.NewGuid(), "Loopback", "127.0.0.1")])).ToArray());
        var began = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        await using var samples = new StreamWriter(Path.Combine(folder, "samples.jsonl")) { AutoFlush = true };
        var measurements = new List<ResourceSample>();
        await using (var service = new MonitoringService(probe, history: new SqliteHistoryStore(historyPath, 8 * 1024 * 1024)))
        {
            await service.StartAsync(config);
            while (timer.Elapsed.TotalSeconds < seconds)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, seconds - timer.Elapsed.TotalSeconds)));
                var snapshot = service.Capture();
                process.Refresh();
                var latestAge = snapshot.Groups.Where(g => g.LastMeasurementUtc.HasValue)
                    .Select(g => (DateTimeOffset.UtcNow - g.LastMeasurementUtc!.Value).TotalSeconds).DefaultIfEmpty(double.NaN).Max();
                var sample = new ResourceSample(DateTimeOffset.UtcNow, timer.Elapsed.TotalSeconds, process.WorkingSet64,
                    GC.GetTotalMemory(false), process.TotalProcessorTime.TotalSeconds, DescriptorCount(process), process.Threads.Count,
                    service.Diagnostics.ActiveGroupWorkers, service.Diagnostics.CompletedPolls, service.Diagnostics.PendingEvidenceBatches,
                    probe.Active, probe.Total, probe.Failures, probe.Errors, latestAge, new FileInfo(historyPath).Length);
                measurements.Add(sample); await samples.WriteLineAsync(JsonSerializer.Serialize(sample));
                if (snapshot.State != SessionState.Running || snapshot.Error is not null)
                    throw new InvalidOperationException("Endurance monitoring stopped or reported an error: " + snapshot.Error);
                if (measurements.Count % 6 == 0) Console.WriteLine(JsonSerializer.Serialize(sample));
            }
            var stop = Stopwatch.StartNew(); await service.StopAsync(); stop.Stop();
            if (service.Diagnostics.ActiveGroupWorkers != 0 || probe.Active != 0 || service.HasUnsavedEvidence) throw new InvalidOperationException("Owned work did not drain.");
            // Restart against the same real SQLite connection and then stop again.
            await service.StartAsync(config); await Task.Delay(1500); await service.StopAsync();
            var archive = await service.ReadArchiveAsync();
            if (archive.Sessions.Count != 2 || archive.Sessions.Any(s => s.EndedUtc is null || s.Interrupted)) throw new InvalidOperationException("Clean restart sessions missing.");
            var summary = new
            {
                started_utc = began, finished_utc = DateTimeOffset.UtcNow, requested_seconds = seconds, actual_seconds = timer.Elapsed.TotalSeconds,
                real_target = "127.0.0.1", group_count = 4, poll_milliseconds = 1000, database_limit_bytes = 8 * 1024 * 1024,
                engine_sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(MonitoringService).Assembly.Location))).ToLowerInvariant(),
                platform = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = Environment.Version.ToString(),
                samples = measurements.Count, working_set_first = measurements.First().WorkingSetBytes, working_set_last = measurements.Last().WorkingSetBytes,
                working_set_peak = measurements.Max(m => m.WorkingSetBytes), managed_first = measurements.First().ManagedBytes, managed_last = measurements.Last().ManagedBytes,
                cpu_seconds = process.TotalProcessorTime.TotalSeconds, cpu_average_percent_of_one_core = 100 * process.TotalProcessorTime.TotalSeconds / timer.Elapsed.TotalSeconds,
                descriptors_first = measurements.First().Descriptors, descriptors_last = measurements.Last().Descriptors, descriptors_peak = measurements.Max(m => m.Descriptors),
                maximum_sampled_measurement_age_seconds = measurements.Max(m => m.MaximumMeasurementAgeSeconds), completed_polls = service.Diagnostics.CompletedPolls,
                real_probe_count = probe.Total, real_probe_failures = probe.Failures, real_probe_errors = probe.Errors,
                stop_milliseconds = stop.Elapsed.TotalMilliseconds, drained_workers = service.Diagnostics.ActiveGroupWorkers, active_probes = probe.Active,
                durable_sessions = archive.Sessions.Count, actual_outages = archive.Outages.Count, database_bytes = new FileInfo(historyPath).Length,
                limits = "Core-engine console tool, not GUI endurance. Healthy loopback only; no outage or physical disk fault induced. Sampled resource trends are not a proof of no leak."
            };
            await File.WriteAllTextAsync(Path.Combine(folder, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine("ENGINE_ENDURANCE_PASS=" + JsonSerializer.Serialize(summary));
        }
        await using var reopened = new SqliteHistoryStore(historyPath);
        var reloaded = await reopened.LoadAsync(includeAllEvents: true);
        if (reloaded.Sessions.Count != 2 || reloaded.Sessions.Any(s => s.Interrupted || s.EndedUtc is null)) return 1;
        return 0;
    }
    private static int? DescriptorCount(Process process)
    {
        if (OperatingSystem.IsLinux()) return Directory.EnumerateFileSystemEntries("/proc/self/fd").Count();
        if (OperatingSystem.IsMacOS())
        {
            var size = proc_pidinfo(Environment.ProcessId, 1, 0, IntPtr.Zero, 0);
            if (size <= 0) return null;
            var buffer = Marshal.AllocHGlobal(size);
            try { var bytes = proc_pidinfo(Environment.ProcessId, 1, 0, buffer, size); return bytes > 0 ? bytes / 8 : null; }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        try { return process.HandleCount; } catch (PlatformNotSupportedException) { return null; }
    }
    [DllImport("/usr/lib/libproc.dylib", SetLastError = true)]
    private static extern int proc_pidinfo(int pid, int flavor, ulong arg, IntPtr buffer, int size);
    private sealed record ResourceSample(DateTimeOffset Utc, double ElapsedSeconds, long WorkingSetBytes, long ManagedBytes,
        double CpuSeconds, int? Descriptors, int Threads, int ActiveGroupWorkers, long CompletedPolls, int PendingBatches,
        int ActiveProbes, long RealProbes, long FailedProbes, long ProbeErrors, double MaximumMeasurementAgeSeconds, long DatabaseBytes);
    private sealed class CountingProbe : IConnectivityProbe
    {
        private readonly IcmpProbe actual = new();
        private int active; private long total, failures, errors;
        public int Active => Volatile.Read(ref active);
        public long Total => Interlocked.Read(ref total);
        public long Failures => Interlocked.Read(ref failures);
        public long Errors => Interlocked.Read(ref errors);
        public async Task<ProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token)
        {
            Interlocked.Increment(ref active);
            try
            {
                var result = await actual.ProbeAsync(address, timeout, token); Interlocked.Increment(ref total);
                if (result.Outcome == ProbeOutcome.Failure) Interlocked.Increment(ref failures);
                if (result.Outcome == ProbeOutcome.Unavailable) Interlocked.Increment(ref errors);
                return result;
            }
            finally { Interlocked.Decrement(ref active); }
        }
    }
}
