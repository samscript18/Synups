# Synups — judge's guide

**Know when your network drops. Know how long it lasts. Keep the evidence.**

Synups helps individuals, developers and IT professionals diagnose intermittent peer reachability and keep useful incident evidence locally. It probes explicitly configured literal-IP peers with real ICMP, applies failure grace, records confirmed incidents and observed recoveries, and preserves comments/history in SQLite. CSV exports support troubleshooting and ISP/IT conversations; they do not certify whole-Internet availability.

## Evaluate it quickly

1. Download the current public [Synups v0.1.1 release](https://github.com/samscript18/Synups/releases/tag/v0.1.1). [Windows x64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip), [macOS arm64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz), [Linux x64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz), [checksums](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS). No account/SDK/backend is needed.
2. Follow [platform installation guidance](../release/INSTALLATION.md). Windows is unsigned; Mac is ad-hoc only and Gatekeeper rejects trusted delivery. No security bypass is recommended.
3. Configure a named literal IP and enabled peer group, then choose Start. Stop before editing. Opening the app does not automatically probe unless explicitly configured to do so.
4. Inspect real target status, freshness and RTT, then Activity/outages and CSV if actual incidents exist. Unknown is not reported as zero latency or uptime.
5. For observed failure/grace/recovery without disrupting your network, inspect the [genuine isolated incident sequence](INCIDENT_DEMO.md), its timestamps/CSV and reproduction protocol.

**Released version:** public **v0.1.1** contains the unchanged verified **0.1.1-rc.4** application at6c812fde9a29eeb8cfd6399a2f1a75e333e81e03, with **91.7% inventory parity**. [All15 anonymous public download/hash checks](../evidence/verification/synups-0.1.1-publication.json) · [Exact source, hashes and evidence](../release/DESKTOP_COMPLETION_CANDIDATE.md). Help/About and manifests retain the internal rc.4 version. [v0.1.0 is preserved as rollback](https://github.com/samscript18/Synups/releases/tag/v0.1.0).

## Original application and the migration

Original: [Bill Menees' WirePeep](https://github.com/menees/WirePeep), upstream **61ae14391151ca9f209223e62c98fae3df5482b8**,1June2024. Its WPF/.NET Framework and Windows-targeted .NET implementation used synchronous Ping, Menees settings/window helpers, WinForms tray/balloons, WPF taskbar/media and Registry startup preferences. The WPF window coordinated background monitoring, state rows, outage logs, alerts and lifetime.

| Preserved | Rewritten | Improved |
|---|---|---|
| Literal-IP targets, identities, groups and timings | Avalonia C#/XAML desktop, observable presentation and platform boundaries | Windows/macOS/Linux actual extracted-apphost execution |
| Sorted round robin, stop on first success, failure grace | Cancellable asynchronous orchestration with bounded concurrency | Monotonic duration/scheduling and honest indeterminate states |
| Outage/comment/session-log/export intent | Local settings, platform integration and lifetime | Durable SQLite history/comments, interrupted sessions, bounded pages/streaming CSV |
| Original XML resources, MIT licence and ancestry | Safe explicit discovery instead of startup TTL/candidate probes | Guided setup, confirmation/duplicate guards, clipboard/help/shortcuts |

No monitoring generator, WebView, cloud/account or fabricated uptime was introduced. Probe errors do not recover an incident; stop ends observation without inventing recovery. [Exact deviations](../migration/LEGACY_SEMANTICS.md) · [Architecture](../migration/ARCHITECTURE.md).

## Before and after

[Before](../evidence/before/2026-10-22-wirepeep-windows.png): actual untouched upstream WirePeep on Windows, [run39841203591](https://github.com/samscript18/Synups/actions/runs/39841203591). Disposable application-scoped outbound blocking caused the failed rows; this is not a real ISP outage. The credited upstream illustration remains available. [After Windows](../evidence/after/2026-10-22-desktop-completion/win-x64/exercise-overview.png), [Mac](../evidence/after/2026-10-22-desktop-completion/osx-arm64/exercise-overview.png), [Linux](../evidence/after/2026-10-22-desktop-completion/linux-x64/exercise-overview.png): genuine rc.4 actual packaged client-area captures using real loopback. [Earlier rc.3 normal Mac OS-window capture](../evidence/after/2026-10-22-legacy-completion/macos-normal-launch.png). [Provenance](../evidence/README.md) distinguishes component incident/UI renders/OS captures and actual source revisions.

## Evidence against the judging dimensions

- **Migration difficulty/completeness:** WPF scheduling/logging/lifetime and Windows-only dependencies required service/presentation separation. Frozen rc.4 reaches **(15 complete+3 partial×0.5)/18 =91.7%** using the unchanged inventory. Alerts/preferences/physical placement/tray remain partial. Genuine profile import from an actual Menees5.1.2 fixture is verified with preview, confirmation, stable IDs and backup. [Inventory](../migration/FEATURE_PARITY.md).
- **Application quality:** clear first-run setup and unknown/stale/error states, usable real incident/comment/export workflow and durable evidence. [Real demonstration](INCIDENT_DEMO.md). No speculative analytics or inflated uptime.
- **Cross-platform implementation:** [exact rc.4 three-OS CI](https://github.com/samscript18/Synups/actions/runs/39850198899), application **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**. Windows/Mac159 default tests, Linux160; each3 separate real/native+1 audio request/fallback+8 Python cases, zero-warning Release and six extracted executable stages. Actual scopes are Windows Server2025, macOS15, Ubuntu24.04/X11/Xvfb+Openbox; local macOS26.3.1 also passed. [Full matrix](../migration/CROSS_PLATFORM.md).
- **Engineering quality:** linked-original characterization, deterministic clocks/fake probes only in tests, real opt-in ICMP, owned shutdown and storage error handling, exact artifact/file hashes, retained failure logs, [CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) zero SARIF results and scoped coverage79.83% lines/66.89% branches. [Raw evidence](../evidence/verification/2026-10-22-desktop-completion.json).
- **Migration write-up:** [README](../../README.md) answers effort/reuse/rethinking/surprises; [migration log](../migration/MIGRATION_LOG.md) preserves failures/fixes. Work dates16–22October2026 and reproducible39 commits after upstream are recorded for the frozen application. AI assistance is disclosed; no total labor hours were measured or invented.

## Limits and category fit

Best IT Pro Tool is appropriate because this is practical network troubleshooting/evidence tooling, operated locally without cloud infrastructure. Best Cross-Platform Port/Legacy Revival are secondary strengths, not claimed awards or superiority to other entries.

Signing/trusted Mac delivery, actual native notification delivery/login execution, physical tray/keyboard/native Save-panel/multiple-display acceptance and broader IPv6/VPN/suspend/Wayland/endurance remain explicitly limited. macOS Accessibility/event-post permission is denied; method calls are not substituted for physical acceptance. The genuine settings-profile importer preserves IDs/timings after preview and backup, rejects unsupported formats and never modifies the WirePeep source file or starts probing. [Release checklist](../migration/RELEASE_READINESS.md), [physical Mac checklist](../release/MACOS_ACCEPTANCE.md), [signing findings](../release/DISTRIBUTION_TRUST.md).

Public0.1.0 and rc.1/rc.2/rc.3 are rollback baselines, not overwritten. Source changes after a frozen revision require independent verification. This guide prepares evaluation; **no official competition submission or acceptance is claimed**.


**Publication status:** v0.1.1 is public and anonymously verified. The approved release body and bundled handoff documents preserve their prepublication wording verbatim; this current guide, the release page and verification receipt establish publication status. Actual alert delivery/audibility, next-login execution and physical tray/multiple-display acceptance remain limited. No competition form was submitted.
