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
