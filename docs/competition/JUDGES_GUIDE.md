# Synups — judge's guide

**Know when your network drops. Know how long it lasts. Keep the evidence.**

Synups helps individuals, developers and IT professionals diagnose intermittent peer reachability and keep useful incident evidence locally. It probes explicitly configured literal-IP peers with real ICMP, applies failure grace, records confirmed incidents and observed recoveries, and preserves comments/history in SQLite. CSV exports support troubleshooting and ISP/IT conversations; they do not certify whole-Internet availability.

## Evaluate it quickly

1. Download the current public [Synups0.1.0 release](https://github.com/samscript18/Synups/releases/tag/v0.1.0). [Windows x64](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-win-x64.zip), [macOS arm64](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-osx-arm64.tar.gz), [Linux x64](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-linux-x64.tar.gz), [checksums](https://github.com/samscript18/Synups/releases/download/v0.1.0/SHA256SUMS). No account/SDK/backend is needed.
2. Follow [platform installation guidance](../release/INSTALLATION.md). Windows is unsigned; Mac is ad-hoc only and Gatekeeper rejects trusted delivery. No security bypass is recommended.
3. Configure a named literal IP and enabled peer group, then choose Start. Stop before editing. Opening the app does not automatically probe unless explicitly configured to do so.
4. Inspect real target status, freshness and RTT, then Activity/outages and CSV if actual incidents exist. Unknown is not reported as zero latency or uptime.
5. For observed failure/grace/recovery without disrupting your network, inspect the [genuine isolated incident sequence](INCIDENT_DEMO.md), its timestamps/CSV and reproduction protocol.

**Version distinction:** public0.1.0 is the permanent judge-accessible download above. The separately reviewed, unpublished **0.1.1-rc.3** adds first-run guidance, discovery, paginated incidents and desktop conveniences and reaches88.9% inventory parity. [Exact source, hashes and evidence](../release/LEGACY_COMPLETION_CANDIDATE.md). [Proposed v0.1.1 release materials](../release/v0.1.1/RELEASE_BODY.md) await publication authorization; do not treat proposed links as downloads. A later integration worktree is not part of the frozen candidate.

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

[Before](../evidence/before/2026-10-22-wirepeep-windows.png): actual untouched upstream WirePeep on Windows, [run39841203591](https://github.com/samscript18/Synups/actions/runs/39841203591). Disposable application-scoped outbound blocking caused the failed rows; this is not a real ISP outage. The credited upstream illustration remains available. [After Windows](../evidence/after/2026-10-22-legacy-completion/win-x64/overview.png), [Mac](../evidence/after/2026-10-22-legacy-completion/osx-arm64/overview.png), [Linux](../evidence/after/2026-10-22-legacy-completion/linux-x64/overview.png): genuine rc.3 actual packaged client-area captures using real loopback. [Normal Mac OS-window capture](../evidence/after/2026-10-22-legacy-completion/macos-normal-launch.png). [Provenance](../evidence/README.md) distinguishes component incident/UI renders/OS captures and actual source revisions.

## Evidence against the judging dimensions

- **Migration difficulty/completeness:** WPF scheduling/logging/lifetime and Windows-only dependencies required service/presentation separation. Frozen rc.3 reaches **(14 complete+4 partial×0.5)/18 =88.9%** using the unchanged inventory. Alerts/preferences/physical placement/tray and genuine original settings import stay partial. [Inventory](../migration/FEATURE_PARITY.md).
- **Application quality:** clear first-run setup and unknown/stale/error states, usable real incident/comment/export workflow and durable evidence. [Real demonstration](INCIDENT_DEMO.md). No speculative analytics or inflated uptime.
- **Cross-platform implementation:** [exact rc.3 three-OS CI](https://github.com/samscript18/Synups/actions/runs/39821153798), application **7ca85cccce6e3609d08d64e4f23172fa14fffb25**. Windows/Mac143 default tests, Linux144; each3 separate real/native+8 Python cases, zero-warning Release and six extracted executable stages. Actual scopes are Windows Server2025, macOS15, Ubuntu24.04/X11/Xvfb+Openbox; local macOS26.3.1 also passed. [Full matrix](../migration/CROSS_PLATFORM.md).
- **Engineering quality:** linked-original characterization, deterministic clocks/fake probes only in tests, real opt-in ICMP, owned shutdown and storage error handling, exact artifact/file hashes, retained failure logs, [CodeQL](https://github.com/samscript18/Synups/actions/runs/39821182932) zero SARIF results and scoped coverage80.81% lines/67.34% branches. [Raw evidence](../evidence/verification/2026-10-22-legacy-completion-final.json).
- **Migration write-up:** [README](../../README.md) answers effort/reuse/rethinking/surprises; [migration log](../migration/MIGRATION_LOG.md) preserves failures/fixes. Work dates16–22October2026 and reproducible29 commits after upstream are recorded for the frozen application. AI assistance is disclosed; no total labor hours were measured or invented.

## Limits and category fit

Best IT Pro Tool is appropriate because this is practical network troubleshooting/evidence tooling, operated locally without cloud infrastructure. Best Cross-Platform Port/Legacy Revival are secondary strengths, not claimed awards or superiority to other entries.

Signing/trusted Mac delivery, actual native notification delivery/login execution, physical tray/keyboard/native Save-panel/multiple-display acceptance and broader IPv6/VPN/suspend/Wayland/endurance remain explicitly limited. macOS Accessibility/event-post permission is denied; method calls are not substituted for physical acceptance. A genuine settings importer is not claimed without actual-version fixture validation. [Release checklist](../migration/RELEASE_READINESS.md), [physical Mac checklist](../release/MACOS_ACCEPTANCE.md), [signing findings](../release/DISTRIBUTION_TRUST.md).

Public0.1.0 and rc.1/rc.2/rc.3 are rollback baselines, not overwritten. Source changes after a frozen revision require independent verification. This guide prepares evaluation; **no official competition submission or acceptance is claimed**.


### Separate rc.4 integration evidence

[Reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md) (application6c812fde9a29eeb8cfd6399a2f1a75e333e81e03) passes genuine settings-profile import/backup/reload on all three extracted apphosts, optional bounded WAV request/fallback and log/pane preferences. Inventory91.7%=(15+3×0.5)/18. [Exact native run39850198899](https://github.com/samscript18/Synups/actions/runs/39850198899):159 default cases Windows/Mac,160 Linux, each3 real/native+1 audio request/fallback+8 Python.164 unique .NET cases; physical desktop/login remains blocked. Windows native delivery was not observed and the adapter was withdrawn. This newer candidate is not the frozen rc.3 proposed v0.1.1 payload and has no public download/release. Do not copy its claims into rc.3 release notes.
