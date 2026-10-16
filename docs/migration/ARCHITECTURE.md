# Synups architecture — Phase 1 decision

Decision date: 2026-10-16. Audit precedes implementation. Original source and solution remain intact.

## Existing boundaries
`WirePeep.sln` contains `WirePeep` and `WirePeep.Common`. Common has no WPF reference, but inherits net48/net6.0-windows, signing, analyzers and resources from src/Directory.Build.props. Its domain objects mix Menees settings serialization with state; Profile construction can synchronously discover devices. StateManager owns observable collections and synchronous parallel ping updates without cancellation. WPF MainWindow owns scheduling, history, logging, alerts and settings, so replacing XAML alone cannot preserve the application.

## Chosen foundation
New code lives under `synups/`, outside legacy build props. `Synups.slnx` selects only the new projects and avoids the legacy PowerShell script's *.sln discovery. Target .NET 10 LTS; pin verified stable Avalonia packages. Use official Avalonia application template, standard desktop lifetime, native window chrome and Fluent controls.

- Synups.Core: small UI-free typed default-profile reader and models. Reuse the original DefaultProfile.xml as a linked embedded resource, with provenance. Read literal targets without running discovery. No Menees dependency or automatic network traffic.
- Synups.Desktop: C#/AXAML shell, explicit composition root, observable ViewModel, navigation and honest setup state. A DI container and generic host are unnecessary at this size.
- Synups.Tests: profile contract tests and headless Avalonia shell checks. Test data belongs only in tests.
- Defer Infrastructure until the monitoring/persistence implementation justifies it. Core must never reference Desktop. Presentation must not own future outage evaluation or persistence.

## Reuse strategy
WirePeep.Common cannot be reused unchanged as a cross-platform reference. Preserve the source now. Reuse the default configuration artifact in Phase 1; port/test its outage and round-robin semantics in Phase 2, then replace synchronous probes with cancellable .NET Ping. Preserve distinction between unavailable probe, disconnected target and confirmed group outage. A positive response from any peer keeps the group connected; individual target samples can be stale because polling stops early. Avoid interpreting ICMP failure as proof the Internet is unavailable.

## Planned service boundaries (not implemented)
A cancellable, non-overlapping monitoring loop takes immutable configuration and publishes timestamped snapshots. A UI-free evaluator tracks group fail thresholds and recoveries using monotonic elapsed time. Infrastructure implements real probes, versioned local configuration, atomic saves, event storage/export, and OS integrations. Dispatcher marshaling is restricted to presentation. Persist events separately from settings; never infer uptime across unobserved gaps, sleep, shutdown or failed probes. Explicitly choose how to treat legacy Unavailable resetting an active failure before porting it.

Platform capabilities (tray, notification, login launch, sound, window activation) need feature-specific implementations and fallback behavior. Add interfaces only when implementation begins. Legacy settings import must be explicit, validated and preserve the original file. No cloud services, WebView, paid UI controls, fabricated telemetry or production simulator.

## Design direction
Restrained dark Fluent desktop UI; high-contrast foreground/muted text, blue accent, shared spacing/radius/type tokens. Native title bar, resizable layouts with scrolling, standard tab focus and keyboard-selectable navigation. Status always includes text. Initial overview says monitoring is not connected; targets display configuration, not measured connectivity. Phase 1 exposes only overview and original target inspection. History, preferences and monitoring editors are future work, not decorative screens. Retain Fluent focus behavior; reduced motion by omission of nonessential animations. Screen-reader and keyboard validation on each OS remains a release gate.


## Phase 2 — implemented monitoring architecture

The original foundation, toolchain, dark tokens and native lifetime are retained. No production dependency or solution project was added.

### Domain and scheduling (Synups.Core/Monitoring)
- `Configuration.cs`: versioned profile, stable group/target GUIDs, literal unicast IP validation, timing bounds, 64 groups / 512 total targets, deep read-only session copy. Enabled groups require targets; removing/moving the last target disables its source group.
- `ConnectivityProbe.cs`: IConnectivityProbe and real IcmpProbe using .NET Ping.SendPingAsync(IPAddress, TimeSpan, empty payload, CancellationToken). Typed success/failure/unavailable results and error categories. Only successful replies carry real RTT. Probe cancellation propagates as control flow; all other probe exceptions are contained. No probes run on application launch.
- `GroupMonitor.cs`: name-sorted round robin, stop on first success, per-cycle freshness, last check and last successful response. Preserves the 20ms target eligibility tolerance. Uses IMonitorClock backed by Stopwatch for elapsed time and UTC for event evidence. Large scheduling gaps reset pending failure streaks and mark confirmed outages uncertain.
- `MonitoringService.cs`: serialized Start/Stop/Restart, independent asynchronous group loops, semaphore-limited concurrency (default four), no overlapping poll per group. Session configuration is frozen. Stop cancels and awaits all tasks. Per-probe timeout is capped at 5s; stop waits up to 7s. If the native operation exceeds that deadline it remains owned in Stopping, restart is blocked, and the window stays open with an error rather than abandoning tasks.
- `MonitoringState.cs`: immutable snapshots, outages and occurrence-sequenced activity. Outages are recorded once at confirmation, recovery only on an actual successful probe. Stop marks the end of observation without inventing a recovery. Sessions get new IDs on restart; process history and comments survive Start/Stop.

### Persistence (Synups.Core/Persistence)
ConfigurationStore uses System.Text.Json with version validation and same-directory atomic replacement. Expected read errors yield a recoverable load result; no destructive default reset. Explicit recovery first backs up the unreadable Synups file. The original WirePeep settings are never opened or modified. Files use Environment.SpecialFolder.LocalApplicationData/Synups/configuration.v1.json. Background async I/O is separate from presentation. An Infrastructure project remains unnecessary for this small local implementation.

CsvExporter writes outage rows in occurrence order with invariant round-trip UTC timestamps, nullable recovery, observation-end timestamps, monotonic elapsed seconds, gap indicator, since-previous duration and properly escaped comments. In-memory session history is the migrated baseline; durable history reload is deferred. Users must export to retain history after process exit. The legacy automatic text-log stream and close-time statistical summaries remain incomplete; no uptime or aggregate percentages were introduced.

### Avalonia synchronization and lifecycle
MainWindowViewModel owns configuration/editor presentation and composes a MonitoringService plus ConfigurationStore. It never schedules probes. The window uses one 250ms DispatcherTimer only while monitoring is active. It pulls the latest bounded-state snapshot; there is no dispatcher event backlog or engine subscription to leak. Existing GroupRow/TargetRow instances receive property updates rather than rebuilding the window. Outage rows are keyed by ID; activity is limited to the latest 500 events. The service retains all outage rows for the process lifetime (long-running retention limits remain future work).

All editing is disabled while Running/Stopping. Apply persists before committing the new presentation configuration. Editing clears displayed measurements until the next Start, so an old successful sample can never be attached to a changed address that retains the same target ID. Owned confirmation dialogs handle destructive edits/recovery; native storage picker selects CSV destinations. Async UI operations are serialized, errors appear in feedback, closing awaits an in-progress save then drains the monitoring service. Timer stops when the window closes. No UI thread blocks on network calls, Task.Wait or synchronous probing. The tiny bundled default XML reader remains synchronous and side-effect free.

### Deliberate semantic corrections
See [LEGACY_SEMANTICS.md](LEGACY_SEMANTICS.md) and deterministic tests. Unavailable probes no longer close an active outage. Any unavailable peer with no successful peer makes the group indeterminate, independent of rotation order. FirstFailureUtc is retained separately; the outage start remains the confirmation time. Duration is elapsed since confirmation through the last accepted poll, including any explicitly marked uncertainty; it is not claimed to be continuously observed downtime. Monotonic scheduling replaces the original coarse one-second wall-clock timer. No automatic local-device discovery or monitoring-on-launch preference was added.
