# Reviewed unpublished candidate — 0.1.1-rc.2

Frozen application source: **6446990571c285b5c2e2a4bb293cbd8e3af40171**, branch `feat/avalonia-migration`, committed **22 October 2026**. [Successful exact-source three-OS CI](https://github.com/samscript18/Synups/actions/runs/39782799326). Later documentation/evidence commits do not replace these binaries. Public [v0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0) and unpublished [0.1.1-rc.1](COMPETITIVE_CANDIDATE.md) remain unchanged rollback baselines. No new tag, Release or competition submission was created.

## Implemented and verified

- Explicit default-gateway discovery through read-only Windows ActiveStore/PowerShell, macOS route and Linux ip JSON facilities. Candidate address/interface preview, explicit confirmation, duplicate protection and preserved existing IDs. Finite cancellation owns/drains the child process; no scan, network change, automatic configuration overwrite or probe. VPN/multiple routes are shown as candidates, never a universal Internet-health conclusion. Deterministic platform fixtures, actual route queries and packaged preview checks passed on all three systems. Configuration-confirmation semantics are deterministic/UI-tested; physical native confirmation was not certified.
- Persisted incident timeline: 50 rows per UI page, keyset query maximum 100, stable incident IDs, group/comment, confirmed/recovered/unresolved/interrupted states, observed duration and exact UTC details. Normal startup caches 200 recent incidents plus active records. Interrupted-session repair and older-comment lookup do not load the entire incident archive. No schema replacement or migration of user data was needed.
- Complete CSV streams paged history, retaining proper quoting and unknown recovery semantics. Failed destinations explicitly report possible partial export while preserving source evidence. Full automatic-log/statistics/retention operations still have separate archive-size costs; this is not a claim that every database operation has constant memory. A CSV is not a full database backup.
- Copy target address and incident details through Avalonia's native clipboard; Help/About with real version/revision and setup/error explanations; F1, F5/Shift+F5, Ctrl/Cmd+1–5 and copy shortcuts. Escape cancels discovery; normal editing keys retain their purpose. Real clipboard and routed-shortcut checks pass in packaged/native verification; these are not physical key presses.
- Opt-in focus/restoration once per confirmed outage/recovery, independent of notification toggles and off by default. Linux real-incident verification hides the native window, confirms actual restoration and exactly two focus requests. OS foreground policy may still decline focus. In-app notification fallback remains; native OS delivery, sound and login registration are not newly claimed.
- Separate CodeQL/manual actual-solution analysis, retained SARIF with zero reported findings, SECURITY.md and isolated optimized Release coverage. Coverage: **2127/2638 lines (80.63%)**, **1435/2116 branches (67.82%)**; Core line 96.53%, Desktop 69.83%. Coverage and a clean scan are not desktop certification or a guarantee of security.

## Actual verification

| Native environment | Release / tests | Extracted executable | Additional evidence |
|---|---|---|---|
| Windows Server 2025 x64 | Locked restore/audit, 0 warnings/errors; 126 deterministic/headless + 2 separate opt-in native/network cases; 8 Python cases | Six stages; five normal exits 0, one intentional interruption exit 1 | Real loopback, native gateway query, SQLite restart, clipboard, preferences, second-process restoration, actual read-only error/repair |
| macOS 15.7.9 arm64 | Same gates/counts | Six stages; five normal exits 0, intentional interruption -9 | Same scope; repeated exact downloaded package locally on macOS 26.3.1 |
| Ubuntu 24.04.5 x64 / X11 Xvfb | Same gates/counts | Six stages; five normal exits 0, intentional interruption -9 | Same scope plus isolated genuine ICMP outage/recovery, persisted timeline/comment/CSV, clipboard and event restoration |

The default suite intentionally skips its two opt-in cases; all **128 unique .NET cases** are exercised across the separate invocations. Gateway query is read-only native routing inspection, not network probing. Local final restore/audit passed, Release 0 warnings/errors (6.92s), 126 deterministic/headless passed, separate 2 real/native cases passed (282ms), Python 8/8.

[Independent package/runtime/hash and evidence record](../evidence/verification/2026-10-22-hardening-final.json), [retained reports, coverage and SARIF](../evidence/verification/2026-10-22-hardening-reports.zip), [CI logs](../evidence/verification/2026-10-22-hardening-ci-logs.zip), [CodeQL run](https://github.com/samscript18/Synups/actions/runs/39782799390).

## Frozen archive SHA-256

| File | SHA-256 |
|---|---|
| Synups-win-x64.zip | `057690b16b6824221bb0cf1f81acd045cd8366da4280aedb095a1463e1266098` |
| Synups-osx-arm64.tar.gz | `11bec41ef9634f6eef9fb9114fa854afdb8a67c09c72b200859de306d8e39281` |
| Synups-linux-x64.tar.gz | `a459275ffd2dc9e4bacb19d989e42659e441d7e11477b39bc58199a2546845ad` |

Byte-identical reviewed CI archives, sidecars, CANDIDATE.json and SHA256SUMS are prepared under ignored `artifacts/hardening/0.1.1-rc.2/`. All outer API digests/ZIP integrity, inner sidecars/manifests/every file, expected RID/CPU/native SQLite/runtime, Unix executable modes and publisher licence/notices passed independent review. No PDB, tests, verifier executable or development diagnostics package appears in the archives. Archives are neither rebuilt nor re-signed locally. Actions assets expire and require GitHub access; these are **not new permanent public release links**. Use public 0.1.0 until a separate reviewed release is authorized.

## Acceptance and remaining limits

Exact Mac candidate normally launched through LaunchServices at 14:37:22Z on 22 October, displayed an owned OS window, created zero sessions on the passive fresh profile, and exited following native OS quit. Its OS-window capture is genuine; the application exit code is not observable through LaunchServices. Separate six-stage direct apphost checks establish normal exit codes. [Normal-launch record](../evidence/verification/2026-10-22-hardening-macos-desktop.json).

**Physical Mac acceptance remains blocked:** Accessibility and event-post permission are false; Screen Recording is available. No permissions or OS security settings were changed. Actual tray Show/Exit clicks, native CSV Save-panel completion, physical keyboard traversal/focus/Escape, minimize/restore, resizing and close during pending writes require a person following [the exact acceptance checklist](MACOS_ACCEPTANCE.md). Programmatic method/control checks are not substituted for those actions.

Windows remains unsigned. Mac strict/deep integrity passes, but **Gatekeeper rejects (exit 3)**; no Developer ID/notarization credential is available. Linux proof remains X11/Xvfb, not every Wayland desktop or tray host. Follow [standard installation guidance](INSTALLATION.md); do not disable security protections. Broad IPv6/VPN/suspend and actual notification delivery remain unverified. Source fixtures exercise errors, cancellation, missing/multiple gateways and partial histories; they are not proof of every real topology.

A separate synthetic storage experiment used fresh reader processes after seeding **2,000 and 20,000 test-only records**. Both returned cache 200/page 50 and streamed every record. Retained managed memory after startup was 534,552/534,472 bytes; databases 1,503,232/14,729,216 bytes. Startup was 82.49/104.61ms in those two local observations, not a comparative benchmark or endurance guarantee. [Measurements](../evidence/verification/2026-10-22-hardening-history-scale.json); reproduction source/project retained in the reports ZIP. The earlier two-hour engine observation remains earlier-source evidence, not an endurance rerun of this candidate.

Latency sparkline is deliberately deferred: only live successful RTT exists; no durable production latency sample series or retention/unknown-sample contract exists. Historical values are not inferred from incident data. No uptime denominator is invented. Modem/transceiver discovery, actual-fixture legacy import, login/sounds/taskbar and some menu details remain incomplete.

Parity is **75.0%=(9 complete+9 partial×0.5)/18**, up from 72.2% using the unchanged rubric: only discovery moves pending→partial. Clipboard/help/focus improve existing partial rows without claiming all legacy menu/taskbar behavior. Readiness remains **8.5/10 under the existing rubric** because trusted distribution and physical acceptance are unresolved. This candidate is functionally stronger and passes more regressions than rc.1; it is not certified safer for every desktop or a replacement for its rollback bytes.


## Impact on the five judging dimensions

| Dimension | Demonstrable improvement | Limit retained |
|---|---|---|
| Migration difficulty/completeness | Gateway intent safely reimplemented; clipboard/help/shortcuts/event focus add legacy desktop behavior |75% coarse inventory; modem/import/login/sounds/taskbar remain incomplete|
| Resulting application quality | Real bounded incident timeline, clear state/UTC/comment evidence, actionable errors and Help |Physical keyboard/tray/Save-panel acceptance blocked|
| Cross-platform implementation | Actual native default-route queries, clipboard and extracted apphosts pass3 OS jobs |Xvfb scope, unsigned Windows and unnotarized Mac|
| Engineering quality | Indexed keyset reads/cache bounds/streaming CSV, interruption/corruption regressions, CodeQL/SARIF and coverage |No universal security/endurance certification; full-archive operations remain separate|
| Migration write-up | README answers official questions with immutable dates/counts; real incident/images/failures and per-source hashes retained |No invented hours, accepted-entry or native-notification-delivery claim|

The unchanged group/probe algorithms and native dependencies were preserved. Reviewable source changes are in Core `Network/GatewayDiscovery.cs`, `Persistence/IncidentPage.cs`, `HistoryStore.cs`, `CsvExporter.cs`, `AppPreferences.cs`, `MonitoringService.cs`; Desktop `GatewaySetup.cs`, `IncidentTimeline.cs`, `DesktopActions.cs`, alert policy/rows/window/viewmodel and the installation-check actions; tests `GatewayTests.cs`, `IncidentPagingTests.cs`, `DesktopConvenienceTests.cs`; workflows/security/coverage and the separate incident verifier. No package dependency was added. Actual-source tests grew from107+1 opt-in at rc.1 to126+2 opt-in; both old archive sets remain untouched.
