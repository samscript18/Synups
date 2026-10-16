# Actual migration log — Phase 1

Work date: 2026-10-16. This is an AI-assisted repository session, with an interruption and later continuation. No total development hours were instrumented; elapsed session time must not be interpreted as labor. Only command timings emitted by tools are reported below.

## Audit and baseline before implementation
Inspected all solution/project definitions, common source classes, WPF views/dialogs, presentation state, application lifecycle, Windows integration, settings/logging, publish profiles and build automation. No AGENTS.md was present on disk despite the IDE tab context. No existing tests were found. Recorded the clean starting commit and independently matched upstream master SHA with GitHub. Preserved Git history, LICENSE and all original source/build files.

Initially no .NET SDK was on PATH. Downloaded Microsoft's install script and installed SDK 10.0.402 osx-arm64 under /tmp, keeping package/template caches temporary. Verified stable Avalonia 12.1.4 against the official release and NuGet package metadata; its package has net10.0 assets and official 12.1.4 template targets net10.0.

Original restore failed NETSDK1100 on macOS. Retried original build with EnableWindowsTargeting=true, restoring reference packs and cross-compiling both frameworks successfully (26.89s, 0 errors, 2 .NET 6 end-of-support warnings). Original test command exited 0 but executed no tests. WPF launch cannot be performed on macOS. Full evidence: BASELINE.md.

## Architecture decision
Wrote baseline, feature inventory and architecture before adding the new application. Common is useful migration source but cannot be referenced unchanged across platforms: inherited Windows frameworks, Menees configuration coupling, blocking discovery and non-cancellable probing need work. Deferred the monitoring engine port. Chose three projects, no DI container, host, cloud or Infrastructure project until those boundaries are needed. Retained separate legacy solution; new .slnx avoids changing legacy build script solution discovery.

## Implemented foundation
Generated Synups.Desktop using official Avalonia.Templates 12.1.4 (avalonia.app), then replaced welcome content with the Synups shell. Removed the template diagnostic package/dev-tools hook to keep the foundation minimal. Added native desktop lifetime with shutdown on main window close, dark Fluent styling and reusable typography/spacing/color/radius tokens. Standard navigation and scrollable content adapt to desktop resizing. No background work exists yet, so there is no monitoring cancellation task to drain on exit.

Added an observable MainWindowViewModel, read-only configuration rows and an honest unconfigured overview. Display formatting stays in Desktop. Load action reads the actual original XML embedded by link from src/WirePeep.Common/DefaultProfile.xml; it preserves literal addresses, name suffixes and timing values, counts but never executes device discovery rules. Core uses typed records, validates names/IPs/positive timings, limits XML size and prohibits DTD resolution. Expected profile read errors appear in the UI with retry available. Reading a tiny bundled resource is synchronous; no network or long operation occurs.

Reused: original configuration artifact, MIT attribution, source history and analyzed semantics. Rewritten: presentation/lifetime and a minimal side-effect-free configuration reader. Not yet ported: real monitoring, outage state, persistence, history, alerts and editors. No production mock data, simulator, generated statistics, network calls or WebView exists in the new projects. Original simulation code remains only in the retained legacy project.

Added profile contracts/invalid input/DTD/error handling tests and an Avalonia headless integration test for opening, two-way navigation, loading real configuration and closing. Added three-OS CI restore/build/test matrix, scoped SDK pin and package lock files. Original workflow and build scripts are unchanged. CI configuration is not evidence that hosted jobs passed.

## Issues encountered and fixes
- Sandbox DNS/network restrictions blocked initial requests; retried required downloads/restores with network access.
- Concurrent first-time CLI initialization encountered a NuGet-Migrations lock-directory error; completed CLI/template setup outside the sandbox.
- First new solution restore lost MSBuild child nodes (MSB4166) and canceled package downloads. Retried with one worker, node reuse disabled and sequential NuGet restore; succeeded (test project restore reported 1.47 minutes).
- Initial test references used xUnit 2, but Avalonia 12 Headless.XUnit requires xUnit v3. Compiler reported duplicate Fact/Theory attributes. Replaced xunit 2 with xunit.v3 3.2.2, set test OutputType=Exe, restored and built successfully. No compatibility warnings were suppressed.
- Sandboxed VSTest could not bind its local communication socket (SocketException 13). Network-enabled execution passed the test suite.
- A final locked-mode restore in the sandbox could not fetch vulnerability metadata (NU1900, warnings treated as errors). Retried with network access; no NuGet audit suppression was added.
- Native screenshot command failed with “could not create image from window”. No screenshot asset was created or represented as verified visual evidence.

## Native startup evidence
Launched the Release DLL through /tmp/synups-dotnet/dotnet. A CoreGraphics query identified an on-screen normal-level window owned by the running process (PID 40454, window ID 3739). Process was alive and emitted no error output. Attempted screencapture only for that exact window; capture returned exit 1. Smoke harness then terminated its child process. This verifies native startup/window creation, not visual quality, keyboard/accessibility, normal user-driven shutdown or release packaging. Headless test separately verifies window close. WPF native startup and Windows/Linux native Synups startup remain untested.

## Final quality gates
Executed from synups/ using SDK 10.0.402 and the temporary CLI/package cache. The final commands preserve warnings-as-errors and NuGet auditing.

| Gate | Final result |
|---|---|
| `dotnet restore ../Synups.slnx --locked-mode --disable-parallel -m:1 -nr:false` | Passed with network access; Desktop restore 3.19s, Tests 27ms, Core up to date. Lock files unchanged. |
| `dotnet build ../Synups.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | Passed, all three projects, 0 warnings, 0 errors; emitted elapsed time 3.49s. |
| `dotnet test ../Synups.slnx -c Release --no-build --logger trx --results-directory /tmp/synups-test-results-final -m:1 -nr:false` | Passed: 10 total, 10 passed, 0 failed, 0 skipped; reported test duration 318ms. Network-enabled execution permits the runner's local socket. |
| Native startup | macOS process and owned native OS window verified; screenshot failed. See evidence above. |
| Original source/licence | Byte-for-byte LICENSE match against HEAD; git diff confirms no changes in src/, WirePeep.sln, Build.cmd, eng/ or original workflow. |
| Source review | New production source has no measurement generator, production simulation, network probe or WebView. Configuration addresses are linked from the original resource. Core has no presentation dependency; display formatting is in Desktop. |
| Documentation | All local Markdown links resolve; six requested migration documents exist; README records actual scope/provenance and remaining gates. |
| Whitespace | `git diff --check` passed. |

Prior successful test pass before the final presentation cleanup: 10/10, reported duration 829ms. Initial dependency/build failures are retained above rather than omitted. TRX files are temporary local evidence, not committed machine-specific artifacts. No hosted CI result is claimed.

Phase 1 foundation is complete within the recorded environment limits. Overall product/submission readiness assessment: **3/10**, because monitoring and key parity features are not yet ported. No Phase 2 work was started. All changes remain in the working tree; no commit, push, release or competition submission was performed.

## Remaining blockers
Continuous real monitoring and legacy feature parity are not delivered. No original Windows screenshot, verified Synups visual capture, native Windows/Linux run or runnable published release artifacts. No native accessibility or distribution/signing validation. Saved WirePeep settings import and full Menees store behavior need Windows investigation. New workflow has not run remotely. Phase 2 is a separate request; do not start it automatically.


# Phase 2 — real monitoring implementation

Work date: 2026-10-16. Phase 1 above is retained as historical evidence; this section supersedes its current-state limitations where explicitly implemented. No total development hours were measured or estimated. No commit/push/release/submission or Phase 3 work was performed.

## Starting evidence and characterization
Re-read README and all six migration records, existing Core/Desktop/Tests, and the original Pinger, Location/LocationState, PeerGroup/PeerGroupState, StateManager/StateSnapshot, Profile, Logger, StatusRow, LogRow and WPF orchestration. Added LEGACY_SEMANTICS.md before engine implementation. Linked the original state-machine source into tests with test-only collaborators, leaving production legacy files untouched. Five original-state tests passed before implementing the new engine (22ms reported): threshold equality, early recovery, error falsely clearing an outage, rotation/freshness and strict 20ms poll tolerance. Mixed-result aggregation and WPF shutdown/logging findings are source-based, not fabricated original runtime runs.

## Incremental implementation
1. Added validated versioned configuration and stable GUIDs. Group enablement is new. Sessions copy configuration deeply and edit only while stopped. Default restoration reuses the real XML literal addresses and timings; no gateway/modem/SFP discovery runs. Empty discovery-only groups remain disabled. Original user settings are never read or modified.
2. Added the real .NET ICMP probe, typed outcomes and permission/network/unsupported/timeout classification. Empty payload supports Linux's documented unprivileged fallback. Cancellation is control flow; finite timeouts and awaited disposal prevent fire-and-forget native work. See CROSS_PLATFORM.md for actual API/source investigation and its limits.
3. Ported name-sorted round robin and stop-on-success, independent target freshness, continuous failure grace/confirmation and observed recovery. Monotonic time drives intervals/durations; UTC records evidence. Deliberately corrected error handling: errors cannot recover an outage, mixed error/failure groups become indeterminate, and large observation gaps reset unconfirmed failure streaks. Outage duration begins at confirmation as in WirePeep, not at the first missed ping.
4. Added session ownership, serialized start/stop/restart/disposal, per-group async loops and a shared concurrency bound of four. Stop waits for owned tasks; a 7s deadline failure retains ownership and blocks restart until drained. Ordinary stop marks observation ended without emitting recovery. No configuration mutation can race an existing session's immutable copy.
5. Added atomic JSON configuration saves, version/input validation and explicit corruption recovery with backup. Implemented session outage/activity records, comments, since-previous durations and proper CSV escaping/invariant UTC. History survives Start/Stop but is process-memory only. Durable reload and legacy automatic text logging/statistical summaries remain unimplemented; the UI explicitly asks users to export before closing.
6. Connected Overview, Targets/groups and Activity/outages to actual engine state. Added working CRUD/membership/timing/enable/default controls, errors and native CSV picker. A single 250ms UI timer runs only while monitoring is active and pulls snapshots; existing row instances update in place. No engine-to-dispatcher event backlog or subscription leak. Close awaits pending file operations, cancels/drains probes and stops the UI timer. Edited addresses cannot inherit earlier measurements through preserved target IDs.
7. Expanded three-OS CI with manual dispatch and separate opt-in loopback test. It cannot verify the unpublished working tree remotely without a commit/push, which was forbidden. No hosted build/runtime result is claimed.

## Actual difficulties and fixes
- xUnit analyzers rejected uncancellable test calls and imprecise collection assertions. Passed the test cancellation token and used the appropriate assertions; warnings-as-errors stayed enabled.
- First native harness restore used a /var temporary alias while build resolved /private/var; transitive project references then failed MSB3202. Canonicalized the temporary path and used consistent working directories.
- Next native harness started its verifier before Avalonia setup, initializing the dispatcher on the wrong thread. Moved verifier startup to AfterSetup; this was a harness error, not an application crash during normal Program startup.
- Reviewed and fixed a disposal/start race by holding the same lifecycle gate through drain and marking disposal complete. Added a queued-restart regression test.
- Reviewed and fixed stale-result attribution after address edits: preserved IDs now clear displayed measurements until the next session. Added UI regression assertions.
- Reviewed async window operations so handled export errors cannot leave a faulted pending task that prevents later close. Stop-timeout feedback clears when the worker finally drains.
- Final sandboxed build was denied access to Avalonia BuildServices/buildtasks.log outside the workspace (MSB4018); reran with approved execution access. No warning suppression or dependency change was used.

## Verification coverage
Tests cover actual API status/error mapping, invalid/finite timeout and pre-cancellation, thresholds, early and confirmed recovery, unavailable errors, mixed peers, ordering/rotation/freshness, monotonic time under UTC jumps, observation gaps, concurrent groups and no overlaps, stop/restart, cancellation during probes, bounded stop retaining ownership, disposal versus queued restart, duplicate event prevention, validation, configuration IDs/roundtrip/corruption/recovery, comments, CSV quoting/UTC/unknown values, UI row identity and state updates, CRUD/moves/defaults, and closing while probing. All fake probes and controllable clocks are test-only. No public IP is required by the default suite.

Intermediate integration runs passed 47 deterministic/headless tests (555ms) and then 50 (508ms), with the real-network test intentionally skipped until explicitly enabled. The opt-in loopback test passed separately (42ms test duration). Final outcomes follow below.

## Real native macOS evidence
The reproducible eng/native-smoke.py harness builds a temporary verifier against the production App/MainWindow/ViewModel/MonitoringService/IcmpProbe, saves an explicit 127.0.0.1 profile in a temporary directory, opens the real native window, invokes Start, reads bound live results, invokes Stop and closes. It never alters the user's real profile or injects fabricated measurements.

Successful native output included:

```text
NATIVE_VISIBLE=True
UI_SUMMARY=Session: 1 active groups · 1 connected · 0 confirmed unresolved outages · 0 unavailable · 0 not checked.
UI_MEASUREMENT=2026-10-16 16:20:37 +01:00
REAL_TARGET=127.0.0.1; Responded · latest cycle; Checked 2026-10-16 16:20:37 +01:00 · last success 2026-10-16 16:20:37 +01:00 · response 0 ms
NATIVE_START_MEASURE_STOP=PASS
```

This is real loopback ICMP and native UI state, not a visual screenshot or proof of external Internet health. Reported RTT 0ms is the integer PingReply value for the successful loopback response. No actual system outage was induced; outage/recovery paths were exercised deterministically. Windows/Linux native runs, IPv6, native permission failures, accessibility and distribution packaging remain unverified.

## Final Phase 2 gates
All runtime work uses the existing three projects and framework APIs; no new dependency was added.

| Gate | Actual final outcome |
|---|---|
| Locked dependency restore | `dotnet restore Synups.slnx --locked-mode --disable-parallel -m:1 -nr:false` passed; no dependency updates. Desktop restore 241ms, Core 3ms, Tests already current. |
| Release build | `dotnet build Synups.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` passed for all three projects, 0 warnings, 0 errors; emitted elapsed time 6.37s. |
| Deterministic/headless suite | `dotnet test Synups.slnx -c Release --no-build --logger trx --results-directory /tmp/synups-phase2-final -m:1 -nr:false`: 51 passed, 0 failed, 1 opt-in real-network case skipped; reported duration 964ms. Includes five linked-original tests and four Avalonia headless integration cases. |
| Explicit real-network suite | Same final binaries, `SYNUPS_REAL_NETWORK_TESTS=1`, filter `FullyQualifiedName~RealLoopback`: 1 passed, 0 failed/skipped; reported duration 66ms. Production IcmpProbe and engine use 127.0.0.1. |
| Total exercised cases | All 52 cases passed across the separate deterministic and opt-in network invocations. The default suite intentionally performs no real ICMP. |
| Native macOS | Production UI start/measurement/stop/close harness passed; real loopback response and bound controls verified. See native evidence above; final rerun also passed. |
| Configuration survives restart | ConfigurationStore roundtrip via a new instance, stable IDs and editor-persisted membership/rename tested; native harness loads its saved temporary profile before starting. |
| Outages/recovery/history/CSV | Deterministic group/engine/UI tests verify thresholds, exact transitions, no duplicates, unresolved stop, comments and CSV. No fabricated real-world outage was staged. |
| Background/UI safety | Bounded stop, non-overlap, queued restart versus disposal, close while probing, UI row identity and changed-address sample reset all tested. |
| Provenance/licence/source | Original LICENSE matches HEAD byte for byte; original src/, WirePeep.sln, Build.cmd, eng/build.ps1 and original workflow unchanged. No history rewrite. |
| Production data review | 17 production C#/AXAML files scanned/reviewed: no fake probes, Random metrics, simulator or WebView. Only real IcmpProbe is instantiated by normal application composition. |
| Documentation/whitespace | Local Markdown links checked; all requested records updated; git diff --check passed. |
| Hosted/platform claims | No remote CI, native Windows or native Linux result claimed. CI cannot run these uncommitted/unpushed changes under the current instruction. |

## Phase 2 completion and remaining limits
The mandatory local monitoring flow is implemented and verified on this Mac: explicit start/stop/restart, real ICMP, peer-group evaluation, confirmed outages/recovery, editable persistent profiles, live Avalonia results, session history/comments and CSV. Cross-platform source targets remain Windows/macOS/Linux, with actual native execution limited to macOS. Full legacy parity is not complete.

Inventory parity: 8 complete + 6 partial + 4 pending over 18 scored legacy inventory items: (8 + 6×0.5)/18 = 61.1%. Simulator and engineering-test rows are excluded; this is not an effort or lines-of-code estimate. Product readiness assessment: **6/10**. The monitoring flow is useful, but platform/runtime/distribution proof and important legacy integrations remain.

Remaining work: durable history reload/retention, legacy automatic text logs and statistical summaries, saved-WirePeep profile import, discovery, alerts/sounds/tray/login preferences/window placement/single instance, native Windows/Linux/IPv6/permission/suspend testing, screenshots/accessibility and runnable signed/packaged release artifacts. History/comments are lost at process exit unless exported, explicitly stated in the UI. No external system outage was induced; deterministic tests establish outage/recovery behavior. Phase 3 is recommended in ROADMAP.md and was not started. No commit or push was performed.


Final native rerun against the completed implementation: 2026-10-16 16:28:51 +01:00, NATIVE_VISIBLE=True, one connected group, real 127.0.0.1 response 0ms, NATIVE_START_MEASURE_STOP=PASS, process exit 0. Temporary harness build: 0 warnings/errors, 2.37s reported. This rerun includes the final lifecycle and stale-result fixes.

# Phase 3 — durable evidence, desktop integration and platform preparation

Work date: 2026-10-16. Phase 1/2 above remain historical; this section describes the current application. No development hours were instrumented or estimated. Command timings below are emitted build/test timings or measured command wall time, not labor. No agent-issued commit, push, history rewrite, release or submission occurred. HEAD observed during the task was `144ad224cc27b858a60f713c69c47b266305d09e`; this contains intermediate files and is not claimed as the final tested working-tree revision. Original upstream provenance remains `61ae14391151ca9f209223e62c98fae3df5482b8`.

## Baseline and preserved ownership
Read all migration records and source, including the original logger/discovery/settings and the current group engine, session ownership, configuration writes, CSV and UI timer. Initial Phase 2 suite passed 51 deterministic/headless cases, with its one real-network case intentionally skipped; reported duration 887ms. No monitoring algorithm replacement or visual redesign was undertaken. GroupMonitor's characterized transitions, round-robin/stop-on-success, stale samples, monotonic timing and bounded cancellation remain intact.

## P0 implementation
Added SqliteHistoryStore, stable event GUIDs, sessions and transactional evidence batches. Evaluated JSON snapshots/custom journals versus SQLite; chose a single serial connection with rollback journal and full synchronous commits. One new direct MIT dependency: Microsoft.Data.Sqlite 10.0.13, bringing SQLitePCLRaw 2.1.13 and native SQLite. There is no extra solution project or backend. See ARCHITECTURE.md for the tradeoff and deployment risk.

MonitoringService now serializes evidence independently of lifecycle, commits heartbeat/outage/events/cursor atomically, retains failed batches and stops instead of dropping evidence. Interrupted sessions reload once without false recovery; duration ends at the last persisted observation. Clean shutdown saves session/observation end. Comments and event IDs survive a new store/service. Pending comments must save before retention. Corruption/version/permission/competing-writer failures preserve files. An idle corrupt archive does not prevent closing; it does block starting measurements.

AutomaticLogExporter derives session JSON and outage CSV from committed SQLite evidence. Records include start/end/interruption, outage/recovery/error/comment text, observed elapsed time and group summaries. It replaces the original mixed text-log format, uses stable GUID filenames and atomic replace, and avoids duplicate appends. SQLite contains active-session evidence immediately; automatic copies are written at Stop/Close or comment changes. No Internet uptime percentage is inferred. Default keep-all retention is bounded by a 64MiB main DB limit; explicit positive retention removes eligible recovered sessions only. Logs have a separate 256MiB cap and manual archival policy. Errors stay visible; no silent evidence deletion or corrupt-file reset.

## P1 implementation
Added validated asynchronous preferences with explicit corrupt-file backup/recovery, always-on-top, exit confirmation, safe saved geometry/maximized state, launch minimized, opt-in monitoring-on-launch, log location/retention and independent outage/recovery/error alert preferences. No OS login setting is altered. Added native TrayIcon Show/Exit using the attributed original icon. macOS/Windows hide on minimize only with an initialized exporter; Linux keeps taskbar access. Restoring cannot re-run initialization or start duplicate sessions.

AlertPolicy consumes transition sequence once, seeds history without replay, rate limits errors monotonically and respects preferences. In-app alerts are clearly labelled. Optional native worker has a bounded queue and owns cancellation/child process streams: macOS osascript and Linux notify-send adapters, explicit Windows in-app fallback. Native delivery/permission is not claimed verified. No alert sounds, taskbar tint, focus-on-outage or login registration is implemented.

Close awaits pending UI actions, async geometry save, stop, log copies, evidence flush and disposal; declined confirmation leaves monitoring running. Failed log-copy shutdown immediately refreshes stopped state so Preferences can be used to recover. The single existing snapshot timer and row identity are preserved.

Prepared CI with matching x64 Windows/Linux and arm64 macOS runners, locked restore, Release tests, explicit loopback, self-contained publishing, native UI/clean close/restart, Linux Xvfb dependencies, captures and always-uploaded logs/TRX/artifacts. Remote jobs cannot test the final uncommitted tree without an authorized commit/push. None was initiated or claimed.

## Difficulties, failures and corrections
- Initial project-file edit for the SQLite reference was malformed; corrected before restore. Actual restore and later locked restore passed, with no audit suppression.
- Early storage integration over-broad Start failure handling changed state on duplicate Start. Existing stop-timeout regression caught it; only a session that actually began now faults.
- Moved preferences file metadata/copy/rename operations entirely off the dispatcher. Target-typed lambda return inference initially failed CS8030/CS8031; added the explicit Task.Run result type and rebuilt.
- First client-capture harness compiled with one obsolete Bitmap.Save warning. Used the supported PngBitmapEncoderOptions overload and enabled warnings-as-errors for the harness; final harness has zero warnings/errors.
- Async placement persistence made an older shell test assert invisibility too early. It now awaits actual close completion.
- New log-failure test exposed stale IsRunning presentation after stop succeeded but export failed. Refreshed the snapshot before reporting failed shutdown; user can immediately change log preferences and retry. A failing test cleanup also hid the original timeout; cleanup now awaits close.
- Reviewed corrupt-history feedback so a no-op pending flush cannot erase an initialization error. Skip derived exports when history is not available, while preserving close refusal for actual pending evidence.
- Final review reproduced a retained-history viewport defect: a test expected event sequences [1,3] after retention but saw [1,2,3]. Reconciled removals anywhere in the list and ordered backfill; added the passing regression. Also restore a hidden owner before exit confirmation/error feedback; the existing confirmation test now verifies this path.
- Reviewed retention to flush pending evidence before deleting/reloading records; a queued comment cannot revive a deleted incident without its original session.
- Sandbox DNS blocked original screenshot download; approved network retry succeeded. Earlier OS screenshot failure is not retrospectively attributed to a proven permission cause.

## Tests and real runtime evidence
Added eight history tests and eleven alert/preferences/headless tests (19 cases beyond Phase 2). Tests cover restart/IDs/comments, unresolved interruption, no false recovery, corruption preservation, transactional rollback/idempotent retry, retention, capacity, writer lease, failed-write ownership/retry, summaries, log errors/rebuild, alert dedup/preferences/cooldown/cancellation, safe tray policy, screen clamping, preferences corruption, auto-start/topmost/confirmation and close fallback. Existing engine/real-probe/UI regressions remain. Fakes/clocks exist only in tests. Uncommitted-transaction rollback is tested; no physical power-failure claim is made.

Intermediate suites: 58 deterministic passes plus one opt-in skip (966ms), then 67 plus skip (968ms). Two later regression runs failed the async-close/log-failure issues above before correction. Final results supersede them, without omitting those failures.

Two separate one-minute native monitoring processes passed with actual loopback; their last persisted session observation durations were 60.643s and 60.086s. This is a short stability smoke only. No external network or real outage was manipulated. Final rerun against completed source: native window visible, one actual connected group, last loopback response 0ms at 17:17:52 +01:00, native tray initialized, `MINIMIZED_VISIBLE=False`, restore/Start/Stop/Close passed. Reload counted one then two clean sessions and two then four events across separate process launches. No fabricated measurements were injected. Native requests for OS notification delivery were not exercised.

## Final local gates

SDK 10.0.402/runtime 10.0.13 on macOS 26.3.1 arm64. Commands preserve warnings-as-errors and security auditing.

| Gate | Actual result |
|---|---|
| `dotnet restore Synups.slnx --locked-mode --disable-parallel -m:1 -nr:false` | Passed; Desktop 258ms, Core 4ms, Tests up to date. RID lock targets retained. |
| Release solution build, no restore, one MSBuild worker | Passed, 0 warnings/errors; emitted build time 1.09s (1.280s measured command wall). |
| Deterministic/headless suite, Release/no-build | 70 passed, 0 failed, 1 explicit real-network skip, total71; reported duration 1s. Includes nine Avalonia headless cases. |
| Explicit `SYNUPS_REAL_NETWORK_TESTS=1`, RealLoopback filter | 1 passed, 0 failed/skipped; reported duration64ms. Across both runs all71 unique cases exercised successfully. |
| Self-contained publish, no restore | osx-arm64, win-x64 and linux-x64 passed on this Mac; command wall3.441s/3.556s/3.486s. File headers confirm Mach-O arm64 / PE x64 / ELF x64. This is not Windows/Linux execution. |
| Final native harness | Two process launches passed real Start→measure→minimize/hide→restore→Stop→Close and durable reload. Harness build0 warnings/errors, emitted2.38s; total command wall13.372s. |
| Authentic captures | Actual visible native client-area overview/targets saved and reviewed. Original author illustration downloaded unchanged and attributed; no fresh WPF Windows run. |
| Licence/provenance | LICENSE byte matches upstream. Original src/, WirePeep.sln, Build.cmd, eng/build.ps1 and original workflow unchanged relative to upstream. |
| Production source | 23 C#/AXAML files scanned/reviewed; no fake probes, Random measurements, simulator or WebView. Only production IcmpProbe provides measurements. |
| Hosted runtime evidence | None for final tree. Windows/Linux native execution, permissions and direct published apphost launch remain unverified. |

Actual logs/TRX/command wall timings are retained locally in ignored `artifacts/phase3-final/`; local self-contained outputs in `artifacts/publish/`. [Verification manifest](../evidence/verification/2026-10-16-macos-arm64.json) records counts, commands and source/capture hashes. CI is configured to retain equivalent logs for 30 days when run; no CI success is invented.

## Scope and readiness
Durability is a Synups enhancement. Meaningful legacy logging, preferences and alerts are partially/fully restored as documented in FEATURE_PARITY.md. Same coarse rubric: **(9 complete +8 partial×0.5+1 pending)/18 =72.2%** (Phase2 61.1%). Product readiness **6.5/10**; local P0/P1 functional gates pass, but Phase3 is **not declared fully complete** because cross-OS runtime and release gates remain.

P2 discovery/import were investigated without shipping speculative implementations. See LEGACY_INTEGRATIONS.md: original TTL/candidate discovery is explicit future work; no startup scans occur. Upstream Menees source documents file-store patterns, but no actual Windows baseline settings fixture or exact-version import validation exists here. Manual profile editing works. No automatic commit/push, release, competition submission or later-phase work was performed by this agent. Remaining platform proof, native alerts/sounds/login, importer/discovery, single-instance activation, accessibility/endurance, published executable launch, packaging/signing and complete notices are in RELEASE_READINESS.md.


Phase 3 implementation files include Core `Persistence/HistoryStore.cs`, `AppPreferences.cs`, `AutomaticLogExporter.cs`, `Alerts/AlertPolicy.cs`, `Monitoring/MonitoringState.cs` and `MonitoringService.cs`; Desktop native adapters/tray/icon attribution, MainWindow/ViewModel/AXAML and project resources; Tests `HistoryTests.cs`, `DesktopPreferencesTests.cs` and existing comment/close callers; package references/locks/RIDs; CI and native harness; README and migration/evidence records. Some initial Phase 3 files were included in the user-owned observed commit; final working-tree changes were not committed by the agent.


CI packaging follow-up: raw artifact uploads do not retain Unix executable bits, so added `eng/package-artifacts.py`: Windows ZIP, macOS/Linux tar.gz, executable-mode/integrity verification, run instructions and SHA-256 sidecars. First local attempts failed because Python 3.9.6 lacks hashlib.file_digest; replaced it with streaming SHA-256 (no dependency). This packaging fix does not change production application source. Final archive verification results are recorded in the evidence manifest. Source archives remain unsigned verification builds, not a release or proof of apphost execution.


Final tooling check: sandboxed Python syntax validation attempted to write the system bytecode cache and was denied. Approved execution retry passed for both helper scripts; no sandbox bypass or production change was used. Final Markdown links and git diff whitespace checks passed. Login launch is explicitly unimplemented on all three OS targets; per-OS evaluation is recorded in CROSS_PLATFORM.md.


# Configurable SQLite storage policy — 2026-10-16

Implemented on top of the existing, uncommitted Phase 3 working tree. Monitoring evaluation/probes, original source/history and dependencies are preserved. No commit, push, publish, release or submission was performed for this change. Earlier 64 MiB statements above describe the historical Phase 3 baseline; the policy below supersedes them.

## Actual storage behavior

The desktop default is **1 GiB**, adjustable in Preferences → History storage from **256 to 10240 MiB** (10 GiB). Capacity has a separate Apply action and an authoritative `storage_policy` row in the history database. The worker validates allocation and the public range, computes pages with the existing page size and 64-bit arithmetic, checks the effective `PRAGMA max_page_count` scalar, and commits the row transaction before acknowledging success. Reopen reapplies the committed policy; a failed update closes the connection so an uncommitted PRAGMA cannot survive as the active policy. Preference geometry/log saves do not rewrite capacity.

Legacy databases gain additive metadata in place, preserving sessions/outages/events/comments and IDs. An actual >256 MiB fixture is loaded under a 256 MiB configured value without truncation; effective growth is capped at current allocation. Legacy migration can need a small metadata allowance (up to 16 pages), immediately recapped after commit. Attempts to lower below allocated size fail unchanged. A successful increase leaves all existing data intact. The 10 GiB test verifies the actual effective setting/reopen, without allocating a 10 GiB archive.

Settings show main/WAL/SHM/rollback-journal lengths, managed backup and selected log-folder lengths, page allocation/reuse, growth headroom, available database bytes, shared database-volume free space, and 80%/95% allocation thresholds. File-length totals do not include filesystem compression/sparse allocation accounting, external backups or previous log folders. A serialized measurement refreshes every five seconds during monitoring, after actions and on manual Refresh. Warnings emit at most once at each severity per capacity per process, so oscillation does not create a notification storm; restart can warn again.

Retention stays off by default. Confirmed positive days persist in preferences and apply immediately, on launch and daily during monitoring under the evidence lock. Entire unresolved/interrupted sessions remain protected. Deletion returns pages for reuse without VACUUM or an implicit shrink. Confirmed manual cleanup first makes a full verified backup, then removes eligible ended sessions and their events/recovered incidents/comments; backup failure cancels deletion. Logs/backups are independent and never silently removed.

Backups use SQLite's online backup API, include committed WAL evidence, set the destination to DELETE journal mode before closing it, run quick_check and atomically publish a unique snapshot beside the history database. Independent snapshot reopen validates records and policy. No raw copy of an active main file is used. Offline restore instructions preserve the entire old recovery file set before substituting a standalone snapshot; cross-filesystem restore/repair UX remains unverified. CSV remains an outage export, not a complete backup.

Genuine writes still enqueue owned batches before commit; failure cancels monitoring and blocks clean disposal. A real SQLITE_FULL test proves no partial event commit, pending ownership, blocked backup/cleanup/close, stopped capacity increase, idempotent retry and successful subsequent outage/recovery monitoring. Increasing capacity deliberately does not flush first. Physical disk-full/permissions/corruption still require repair; pending memory is not force-quit/power-loss protection.

## Failures found and corrected during this change

- Initial compilation caught a primary-constructor parameter captured alongside its field initializer (CS9124); introduced an explicit initial-capacity field.
- Sandbox builds could not write Avalonia's Library build log; the approved build retry passed. Sandboxed tests also failed MSBuild local communication pipes; approved one-worker tests passed, and the stranded sandbox test process was terminated after cancellation failed.
- Sandboxed locked restore failed NU1900 because NuGet vulnerability data was inaccessible. Approved network retry passed with auditing still enabled; no warning/audit suppression was added.
- First storage suite: 14 passed, 3 failed. The oversized legacy fixture exposed policy-metadata allocation at an already-full page cap; bounded additive migration overhead fixes it. The WAL snapshot left a WAL-mode destination whose later read generated sidecars; destination normalization fixes it. The existing-page-size fixture had not materialized a schema before closing; corrected the fixture to preserve its intended page size.
- Corrected suite passed 22 storage/Settings cases. Added running-policy refusal and then a Stop/log-metric regression, bringing new coverage to 24 cases.
- A first complete gate run passed 93 deterministic cases plus the opt-in loopback case and two one-minute native launches (60.637s/60.597s persisted observation). Visual inspection of Settings showed log totals could lag after Stop. Completed actions now await a fresh serialized measurement; shutdown drains it and prevents new measurements. The final results below supersede that earlier source snapshot.

## Verification and limits

Final Release/headless results: **94 passed, 0 failed, 1 intentional real-network skip** (95 total), followed by the separately enabled real-loopback case **1 passed** (66ms). All **95 unique cases** were exercised. Solution build emitted **0 warnings/errors**, 5.42s (5.692s command wall). Locked restore passed with audit enabled; no dependency changes were necessary. Tests cover legacy page size/records, default and 256 MiB/2 GiB/10 GiB capacities, invalid/overflow inputs, failed atomic updates, restart, oversized/lowering behavior, allocation thresholds/deduplication, real WAL/SHM/log/backup accounting, snapshot integrity/reopen, retention and confirmation/backup-failure guards, daily active-session protection, live-policy refusal, SQLITE_FULL recovery and corrupt bytes. Existing monitoring/lifecycle/headless regressions remain green.

Logs/TRX/captures and exact final command timings are retained in ignored `artifacts/storage-verified/`; earlier storage failures remain in `artifacts/storage-initial/`. The new storage evidence manifest supplements, rather than rewrites, the historical Phase 3 manifest. Final native smoke: two one-minute process launches passed Start → real measurement → minimize/hide → restore → Stop → Close, preserving one then two clean sessions and two then four events. Persisted observation durations were **60.623s / 60.595s**. Both reopen checks returned configured/effective **1,073,741,824 bytes** with 4096-byte pages. Harness build emitted zero warnings/errors (2.56s); total native command wall was 131.469s. Final target measurement was 2026-10-16 17:57:31 +01:00 with an actual 0ms loopback reply. Overview/targets/Settings native client captures were inspected; post-Stop log bytes were present. Process inspection found no remaining Synups/test/harness processes. [Storage evidence manifest](../evidence/verification/2026-10-16-storage-policy-macos-arm64.json) records source/capture hashes, commands, counts and limits.

The 256 MiB automatic-log cap is unchanged. Backups require extra space in the managed sibling directory; no in-app restore/relocation wizard exists. Physical ENOSPC, genuine abrupt-process/power loss, full-sized multi-GiB endurance, bounded-memory behavior and Windows/Linux runtime remain unverified. Load/export/retention still materializes historical records. Existing published artifacts predate this change and were not regenerated. Overall readiness remains **6.5/10**, with a 10/10 aspirational target; local storage tests do not establish production or competition readiness.


# Phase 4A — reviewed checkpoint and cross-platform CI

Work date: 2026-10-16. This request explicitly authorizes reviewed commits/pushes, superseding the previous no-commit scope. Starting branch `feat/avalonia-migration`, HEAD `144ad224cc27b858a60f713c69c47b266305d09e`, origin `https://github.com/samscript18/Synups.git`. Read current readiness/platform/log records, workflow, project settings, packaging/native tools and all changed production/test areas, including the completed storage-policy update. The earlier Phase 3 counts do not describe the current storage-policy suite.

Review found only coherent Synups migration/storage/testing/documentation changes. Automated text screening reports no credential/private-key patterns, private absolute user paths, or proposed generated binaries/databases/artifacts. The two email-pattern hits were unchanged actions/version syntax in the original workflow. Reviewed screenshots contain only loopback verification state or the attributed author's upstream illustration. Original LICENSE matches upstream byte for byte, and original source/solution/build scripts/workflow remain unchanged. No AGENTS.md was found.

A sandboxed remote query failed DNS resolution. Approved network retry verified the original master ref and public GitHub repository identity. Git uses its existing osxkeychain credential helper; no token was printed or added to source. No alternate account, guessed credentials or authentication bypass is authorized.

Fresh locked restore passed (Core166ms/Desktop68ms); Release solution build passed with 0 warnings/errors (1.36s emitted). Default suite: 94 passed, 0 failed, 1 intentional opt-in skip (95 total), reported duration1s. Explicit real-loopback test: 1 passed, 0 failed/skipped,65ms. Native harness passed two real macOS process launches (18:02:29/18:02:32 +01:00), Start/measurement/minimize/restore/Stop/Close, sessions1→2 and events2→4, 1 GiB effective capacity and genuine loopback RTT2ms/0ms. Raw logs/TRX/captures are retained under ignored artifacts/phase4a-local. These checks do not claim a published executable was launched.

Reviewed the three-OS workflow against Synups.slnx, matching RIDs, locked dependencies, security auditing, explicit loopback, native X11 under Xvfb and failure-evidence uploads. Hardened packaging to require actual runtime/native SQLite dependencies; retain the restored .NET runtime licence/notices; record RID/source revision/runtime inventory/file SHA-256; and verify every archived file against the published bytes. No production dependency or monitoring algorithm changed in this checkpoint task. Complete third-party notice review, apphost execution and signed packaging remain separate gates. All local publish/archive results and subsequent actual push/CI outcomes will be appended below.


Local packaging completed successfully for the current storage-policy implementation: osx-arm64 and linux-x64 archives each verify 233 published files; win-x64 verifies236. Original licence bytes, restored runtime notices, every file hash, archive SHA-256 and Unix apphost mode were checked. These fresh build outputs stay under ignored artifacts/phase4a-publish and artifacts/phase4a-packages; they are not public releases. The proposed source set has no text-review credential/private-path findings. Workflow YAML syntax and git diff whitespace checks pass.


## Actual checkpoint, push and hosted evidence

Created **88e7ea507d82a39238ba48ac97936a2d2fa23c28**, `Complete durable monitoring and cross-platform CI checkpoint`, on **feat/avalonia-migration**. Push to the confirmed origin succeeded and established upstream tracking. Working tree was clean after the checkpoint. Used the existing Git credential helper for authenticated GitHub API inspection; no credentials were printed/stored, alternate identity guessed or authentication bypass attempted. Download redirects strip authentication outside api.github.com.

[Run38937720125, attempt1](https://github.com/samscript18/Synups/actions/runs/38937720125) completed successfully for that exact SHA. Actual runner images: Windows Server2025/10.0.26100 x64 (windows-2025-vs2026), macOS15.7.9 arm64 (macos-15-arm64), Ubuntu24.04.5 x64. Each locked restore/build passed, build logs report0 warnings/errors, TRX confirms94 deterministic/headless passes plus1 intended networking skip, and the separately enabled real ICMP case passed. Two native production-composition verifier processes per OS passed Start/measurement/Stop/Close and durable reload; final sessions2/events4 and configured/effective1 GiB were observed. Windows/macOS initialized tray exporters and hid/restored; Linux Xvfb kept the window visible because tray restoration was not established. No real permission problem or reproducible platform failure occurred. No warnings/audits were suppressed and no failed test was skipped.

Downloaded all platform artifacts (IDs12061018784 Windows,12061787596 macOS,12061338122 Linux) and the full workflow log ZIP. API artifact digests, package sidecars, source revision, original licence text, runtime notices and all236/233/233 published file hashes were verified, along with Unix0755 executable mode. Parsed TRX/native logs and reviewed real native client captures for all three OSes. Raw evidence stays ignored locally; sanitized metadata/hashes are committed in docs/evidence/verification/2026-10-16-ci-checkpoint.json. GitHub retention is30 days; no public release was created.

The first local artifact-inspection assertion failed because Windows checkout converted LICENSE from LF to CRLF. Confirmed identical licence text/copyright, with no missing content; the repo blob is unchanged. Added only `LICENSE text eol=lf` to .gitattributes to preserve exact bytes in future archives. No application/test source changed after the successful checkpoint. The follow-up commit records these actual results and the line-ending policy; its workflow must be checked independently.

Published executables were **not launched**: native evidence comes from separate framework-dependent verifier entry points referencing production composition. This distinction remains an explicit release blocker. Native notifications/physical tray clicks, installed desktop/Wayland, real external outages/IPv6/permission/cancellation, endurance/accessibility, full dependency notice audit and signing/notarization remain unverified. Readiness assessment rises to **7/10**, not production readiness. Source branch review/merge remains separate from the preserved original master. No history rewrite, force push, release or competition entry was performed.


## Follow-up run failure and reviewed correction

Pushed documentation/licence commit **43446f535b29d35701c723dfeedceae698ca8646**. [Run38939047265](https://github.com/samscript18/Synups/actions/runs/38939047265) passed macOS/Linux, including exact original licence bytes in their downloaded archives, but Windows failed its deterministic/headless step:93 passed/1 failed/1 intentional skip. Downloaded failed artifact12063140171 and the complete logs. HistoryTests.EngineHistoryCommentsAndIdsSurviveNewServiceAndStore exceeded the production seven-second drain attempt; TRX duration17.47s overlapped real WAL/backup/settings fixtures and the large legacy-database fixture (11.64s). This establishes concurrent I/O during the failure, not a proven OS-specific root cause. The Windows post-test publish/network/native steps were skipped due to the failure and are not claimed passed for this SHA.

Added a named nonparallel xUnit collection for the four disk-backed fixture classes. This isolates the large file tests from unrelated wall-clock shutdown assertions without changing production behavior or turning off suite-wide concurrency. Existing bounded-worker/deadline/ownership tests remain; added four concurrent groups committing to real SQLite, then Stop/reopen checks for stable IDs, unresolved incidents and exactly four outage events without false recovery. No failed case was removed, no warning/audit was disabled, and the seven-second application deadline is unchanged. Local full Release run passed95 cases with1 deliberate real-network skip (96 total), reported duration2s. Correction still requires actual hosted verification. Under real slow storage, the app can still report that it is waiting for owned work; this is not an endurance/performance fix.


## Corrected three-platform verification

Created and pushed **7b75296f97ea62d986e7d16b152dedaabcf999d6**, `Isolate durable storage fixtures and retain concurrent SQLite coverage`. [Run 38940757093](https://github.com/samscript18/Synups/actions/runs/38940757093), attempt 1, passed all three jobs. Each exact source build: locked restore and zero-warning/error Release, 95 deterministic/headless passed plus one intended opt-in skip, separately enabled real-loopback case passed, two native production-composition verifier processes passed, durable sessions 1→2/events 2→4, 1 GiB effective capacity, self-contained publish/archive passed. The Windows failure did not recur in this run. This is evidence for the correction, not an endurance claim or proof of its original I/O root cause.

Downloaded artifact IDs **12062264115 Windows, 12063031811 macOS, 12063491106 Linux** and complete workflow logs. Validated GitHub ZIP digests, all inner published hashes (236 Windows/233 each Unix), archive sidecars, exact original MIT bytes, runtime notices and Unix executable mode. Viewed all three real native overview client captures. Sanitized metadata/hashes are retained in [corrected CI manifest](../evidence/verification/2026-10-16-ci-corrected.json); raw logs/TRX/images/packages remain ignored under artifacts/phase4a-local/downloads and artifacts/phase4a-ci-corrected. All observed limits remain: separate verifier rather than published-apphost execution, Linux Xvfb rather than installed desktop, no physical tray/OS notification/endurance/signing validation. No release or entry was created.


## User-requested desktop visual refinement

The user then explicitly requested a more beautiful UI for Windows, macOS and Linux. Retained the working native Avalonia shell, engine, persistence, lifecycle and package versions. Refined shared dark ink/teal tokens, typography, primary/secondary controls, workspace icons/selection, honest status badges and overview counts. Reorganized configuration, history and Preferences into readable panels; collapsed edit forms while running, retained error/recovery controls and all existing actions, and reset scroll on navigation. Compact layout narrows the sidebar without hiding navigation or adding a WebView. No decorative measurements or new dependency was added.

Extended existing headless assertions for unknown counts, confirmed outage, connected colors, stopped/history state and address-edit reset. Native harness now captures a second compact window size before the existing real loopback/Stop/Close/restart checks. First UI build exposed four Avalonia12 obsolete Watermark warnings; changed those new inputs to PlaceholderText, without suppression. Intermediate suite passed95 deterministic/headless cases plus one deliberate opt-in skip. Native macOS runs passed two real loopback launches/reloads with inspected overview/compact/targets/storage captures. Final verification and exact pushed UI SHA will be recorded separately; previous successful CI does not certify these changes.


Final local UI gates: locked restore passed with NuGet auditing (Desktop3.39s/Core7ms), Release build0 warnings/errors (12.16s emitted), final suite95 passed/0 failed/1 intentional real-network skip, explicit loopback1 passed (135ms on unchanged probe/engine). Native final composition/harness build0 warnings/errors (4.89s), two real loopback processes passed at19:05:38/19:05:42 +01:00, observed2ms/0ms replies, Stop/Close/reload sessions1→2/events2→4 and1GiB limits. Normal and minimum820×560 captures were inspected; narrow content scrolls. Source/capture hashes are recorded in docs/evidence/verification/2026-10-16-ui-macos-arm64.json. Original src/ and LICENSE remain unchanged; no dependency/engine/storage change. No fabricated data. Final whitespace/local links/sensitive-text screening passed. Hosted verification of the new UI source remains pending until its own push/run.
