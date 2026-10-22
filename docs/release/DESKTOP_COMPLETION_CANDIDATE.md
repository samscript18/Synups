# Reviewed unpublished candidate — 0.1.1-rc.4

Application source: **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, branch `feat/avalonia-migration`. [Successful exact-source native CI](https://github.com/samscript18/Synups/actions/runs/39850198899). This is a separate candidate, not a replacement for public0.1.0 or frozen rc.1/rc.2/rc.3. The owner now selects these exact **rc.4** archives as the proposed public **v0.1.1** payload, superseding the prior rc.3 proposal. Help/About and manifests retain internal version0.1.1-rc.4; no archive is rebuilt or relabelled. Publication still needs separate authorization.

## Implemented and tested

- **Genuine WirePeep profile import:** the preserved original Windows executable and Menees5.1.2 produced a real `.stgx` fixture in [run39840176062](https://github.com/samscript18/Synups/actions/runs/39840176062). Read-only parsing, size/DTD limits, full preview, explicit replacement confirmation, byte-preserving destination backup, stable IDs/timings and invalid-input refusal are verified. The source file, SQLite history, desktop preferences and OS registrations remain untouched. Import never probes or starts monitoring. Compatibility covers this actual schema and positive invariant whole-millisecond timings; unsupported variants are rejected rather than guessed. Windows-specific options are deliberately not imported wholesale.
- **Optional local WAV sounds:** outage/recovery category preferences, existing transition deduplication/cooldown, finite worker ownership and safe cancellation/fallback. No bundled synthetic demonstration measurements. Windows/macOS playback processes completed; actual audible output was not observed. Linux CI lacks `paplay` and correctly reports an unavailable helper. In-app alerts remain available.
- **Log naming:** stable session IDs, UTC/local start prefixes and fixed archive copies. Canonical per-session records are retained; fixed snapshots are derived JSON/CSV, limited to16MiB and the existing log-folder quota. The pair is not a database transaction; SQLite remains the source of truth. No arbitrary legacy filename template or fabricated uptime percentage is claimed.
- **Pane preferences:** group/target/incident maximum heights persist with validated bounds. These are independent pane sizes, not a claim of identical WPF splitter behavior. Real packaged controls apply/reload these preferences on all three OS runners.

No package dependency, monitoring algorithm or history schema was changed. Closed rc.3 configuration/preferences and SQLite sessions/events/incidents reload with stable logical records, including a genuine incident/comment; original files were not modified. [Upgrade results](../evidence/verification/2026-10-22-desktop-completion-upgrade.json).

## Verification

| Native target | Default .NET | Separate real/native | Audio request | Actual extracted apphost |
|---|---:|---:|---|---|
| Windows Server2025 x64 |159 passed,5 intentional skips|3 passed|1 passed; request accepted, audibility unknown|Six stages passed|
| macOS15 arm64 |159 passed,5 intentional skips|3 passed|1 passed; request accepted, audibility unknown|Six stages passed|
| Ubuntu24.04 x64 / Xvfb+Openbox |160 passed,4 intentional skips|3 passed|1 passed; missing-helper fallback|Six stages passed|

All164 unique .NET cases are exercised across the separate native invocations and OS-specific Gio case. Each OS also passed8 Python safety cases, audited locked restore and a zero-warning Release build. The importer uses the external test fixture only during explicit installation verification; it is not included in application packages and its public target addresses are never probed by that verifier.

Six stages verify actual apphost/PID/hash, configuration and import/backup, real loopback, SQLite restart/interruption, read-only failure/repair and shutdown. Five normal exits are0; deliberate termination is Windows1/Unix-9. The same downloaded Mac executable passed locally on macOS26.3.1. Programmatic real controls are distinct from physical tray/menu/keyboard and native Save-panel acceptance.

[Exact reports, archive hashes, audio outcomes and source qualifications](../evidence/verification/2026-10-22-desktop-completion.json) · [Retained reports](../evidence/verification/2026-10-22-desktop-completion-reports.zip) · [CI logs](../evidence/verification/2026-10-22-desktop-completion-ci-logs.zip) · [Local Mac apphost](../evidence/verification/2026-10-22-desktop-completion-local-mac.json).

[Exact-source CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) retains0 SARIF results. Scoped optimized coverage is **2593/3248 lines=79.83%**, **1881/2812 branches=66.89%**, lower than rc.3's80.81%/67.34% after the added integrations. No coverage or security finding was suppressed.

## Immutable candidate archives

| Archive | SHA-256 |
|---|---|
|Synups-win-x64.zip|`cc02117abc393cc0e6baf39e75aceebf529c8d695e5d61e93a18262b36a9d570`|
|Synups-osx-arm64.tar.gz|`fa00e6a06255b4cd116ecb985806a05a02fb4e93db6977e34420a63f0cc31200`|
|Synups-linux-x64.tar.gz|`49387bf6358b8d06478a64091f8780a64f41861f820df8f0ee1b04e17117c757`|

All three downloaded CI containers match GitHub's SHA-256 metadata. Every package member matches its embedded manifest; RID/CPU, native SQLite, runtime/dependency notices, original MIT bytes and Unix executable modes were independently checked. No test assembly, fixture or debug symbols are packaged. Local frozen preparation: `artifacts/presentation/0.1.1-rc.4/` (ignored, not a permanent public download).

## Failures retained and remaining limits

The attempted Windows native notification adapter failed independent on-screen Shell observation in [run39848205141](https://github.com/samscript18/Synups/actions/runs/39848205141). It was withdrawn before this candidate; in-app fallback is retained. [Failed logs](../evidence/verification/2026-10-22-desktop-integration-failed-ci-logs.zip) and [delivery report](../evidence/verification/2026-10-22-windows-notification-failed-reports.zip) are preserved. Passing process exit would not have proved delivery.

The first import test helper mishandled the fixture's actual BOM; production stream parsing worked. Test setup now loads the original bytes without normalization. An Avalonia obsolete-watermark warning was corrected using PlaceholderText; warnings remain errors. The first local upgrade-harness invocation used the wrong working directory and failed before reading any data; corrected closed-copy checks passed.

**Parity: (15 complete+3 partial×0.5)/18=91.7%.** The settings-persistence row is now complete for the recorded profile-import intent. Alerts remain partial for actual OS delivery/sound/attention verification; preferences for actual next-login execution; placement/tray for physical menu and multiple-display acceptance. The owner is unavailable for manual checks and explicitly requested retaining these blockers. Accessibility/event posting remain denied; no permission or real-user startup setting was changed. Windows remains unsigned; macOS remains ad-hoc/not notarized. See [physical checklist](MACOS_ACCEPTANCE.md) and [distribution trust](DISTRIBUTION_TRUST.md).

This candidate adds verified migration functionality and retains the monitoring/storage regressions, but does not establish universal desktop reliability or100% parity. Readiness remains **8.5/10**: publication choice, trusted distribution and physical acceptance still matter more than an inventory score. No Release/tag or competition form was created.
