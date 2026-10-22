# Synups

**Know when your network drops. Know how long it lasts. Keep the evidence.**

Synups is a local-first network reliability monitor for Windows, macOS and Linux. Monitor the peers you choose, record confirmed ICMP outages and observed recoveries, and keep incident timelines, comments and troubleshooting evidence in local SQLite.

Use it to investigate intermittent connectivity, maintain a local incident history, and export records when discussing a problem with your IT team or ISP. A failed ICMP check describes reachability to configured peers; it does not establish that the entire Internet is down.

Built with **Avalonia, C# and XAML**, migrating [Bill Menees’ WirePeep](https://github.com/menees/WirePeep) for the [2026 Avalonia Port Challenge](https://avaloniaui.net/blog/avalonia-port-challenge), targeting **Best IT Pro Tool**. No account, backend or cloud service is required.

## Download

**Recommended public release: [v0.1.1](https://github.com/samscript18/Synups/releases/tag/v0.1.1), 91.7% legacy parity.** These permanent downloads require neither a GitHub account nor a .NET SDK. Extract the complete archive and keep its files together.

| Platform | Download | Launch / installation note |
|---|---|---|
| Windows x64 | [ZIP](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip) | `Synups.exe` — unsigned; Windows or organizational policy may warn/block. |
| macOS arm64 | [tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz) | `Synups.app` — ad-hoc integrity only, **not notarized; Gatekeeper rejects trusted distribution**. Read the [installation guidance](docs/release/INSTALLATION.md). |
| Linux x64 | [tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz) | `./Synups` — retain executable permissions; native desktop libraries are required. |

Verify downloads against [SHA256SUMS](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS). The runtime, Avalonia, SQLite, licences and dependency notices are bundled. Do not disable operating-system security protections.

**Released v0.1.1 uses the verified, unchanged 0.1.1-rc.4 binaries**, application source `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`. Help/About and manifests retain **0.1.1-rc.4**; no binary was rebuilt or relabelled. [Anonymous verification of all 15 public assets](docs/evidence/verification/synups-0.1.1-publication.json) · [Release notes](https://github.com/samscript18/Synups/releases/tag/v0.1.1) · [Exact source and evidence](docs/release/DESKTOP_COMPLETION_CANDIDATE.md). [v0.1.0 remains an unchanged rollback release](https://github.com/samscript18/Synups/releases/tag/v0.1.0).

The frozen archive’s bundled README and approved release handoff documents retain their historical build/prepublication wording. Use this current README, the repository installation guide and publication receipt for current status and links. All approved asset bytes and the verbatim release body remain unchanged.

## See Synups

These genuine overview captures show the exact **v0.1.1 / internal rc.4 binaries**, using real loopback ICMP. The separately linked incident sequence retains its original rc.3 source qualification: controlled packet filtering in an isolated Linux namespace, not an ISP outage.

![Synups rc.4 monitoring a real loopback target on Windows](docs/evidence/after/2026-10-22-desktop-completion/win-x64/exercise-overview.png)

| Windows | macOS | Linux / X11 |
|---|---|---|
| ![Windows overview](docs/evidence/after/2026-10-22-desktop-completion/win-x64/exercise-overview.png) | ![macOS overview](docs/evidence/after/2026-10-22-desktop-completion/osx-arm64/exercise-overview.png) | ![Linux overview](docs/evidence/after/2026-10-22-desktop-completion/linux-x64/exercise-overview.png) |

[Fresh first run](docs/evidence/after/2026-10-22-desktop-completion/osx-arm64/exercise-first-run.png) · [Actual Mac OS window](docs/evidence/after/2026-10-22-legacy-completion/macos-normal-launch.png) · [Real incident demonstration](docs/competition/INCIDENT_DEMO.md) · [Screenshot provenance](docs/evidence/README.md)

## Features and practical uses

- **Diagnose intermittent reachability:** real finite-timeout ICMP, configurable peer groups, polling intervals and failure grace.
- **Keep useful evidence:** confirmed incidents, observed recoveries, comments, interrupted sessions and local SQLite history across restarts.
- **Understand uncertainty:** connected, disconnected, unavailable, stale and not-checked states; actual RTT only after a successful probe.
- **Share troubleshooting records:** chronological UTC CSV, automatic JSON/CSV session logs and observed-duration summaries.
- **Configure your own monitoring:** named IPv4/IPv6 targets, group membership, persistent settings and guided first-target setup.
- **Migrate actual WirePeep profiles in rc.4:** preview a genuine Menees `.stgx` profile, confirm replacement and keep a backup; original IDs/timings are preserved and nothing starts automatically. Unsupported formats are refused.
- **Use desktop controls:** Start/Stop, window preferences, clipboard/context actions, Help, transition alerts and safe tray/restoration behavior.
- **Discover deliberately:** inspect native gateway candidates or explicitly check the original modem/transceiver addresses, then confirm before adding. No subnet scan, network change or startup discovery.

Some features are newer than v0.1.0; the [version-specific inventory](docs/migration/FEATURE_PARITY.md) distinguishes them from public v0.1.0.

## Quick start

1. Extract and launch the platform package, following its installation guidance.
2. In v0.1.1 (internal rc.4), enter a group name, target name and a literal IP you are authorized to monitor, then choose **Add target and create group**. In v0.1.0, use **Targets and groups** to create the group and target.
3. Choose **Start monitoring**. Opening Synups does not probe automatically unless you deliberately enable monitoring on launch.
4. Review live results, then **Activity and outages** for confirmed incidents. Add comments and export CSV when records exist.
5. Stop before editing configuration. Settings and recorded evidence survive a clean restart.

A peer group contains alternative targets: probing stops after its first success. Other members may remain stale or not checked. If ICMP is filtered or permission is denied, inspect the error and try an authorized responsive peer; missing replies alone do not identify the cause.

## From WirePeep to Synups

| Original WirePeep | Synups |
|---|---|
| Windows WPF, .NET Framework/Windows-targeted .NET and Menees desktop integrations | Avalonia desktop UI on Windows, macOS and Linux |
| WPF window owns scheduling, state presentation, logging and alerts | UI-free Core services, observable presentation and explicit OS boundaries |
| Synchronous probes and wall-clock timing | Bounded asynchronous groups, finite cancellation and monotonic elapsed timing |
| Sorted rotating peers, stop on success, failure grace and outage comments | Those intents preserved and characterized against original source |
| Session-memory outage list and text logs | Durable SQLite incidents/comments, bounded history pages, streaming CSV and backups |
| Unavailable results could clear failures | Errors remain indeterminate; only observed success recovers an incident |

| Before: original WirePeep on Windows | After: actual rc.4 Avalonia client |
|---|---|
| ![Original WirePeep, actual preserved WPF application on Windows](docs/evidence/before/2026-10-22-wirepeep-windows.png) | ![Synups actual Windows client](docs/evidence/after/2026-10-22-desktop-completion/win-x64/exercise-overview.png) |

The original was freshly built from preserved upstream `61ae143` and captured on a disposable Windows runner with outbound traffic blocked for that executable. Its failed replies are controlled test results, not an ISP outage. [Original run](https://github.com/samscript18/Synups/actions/runs/39841203591) · [Before/after provenance](docs/evidence/README.md). The attributed upstream illustration is also retained.

## Migration challenges and accomplishments

WirePeep.Common inherited Windows targets and depended on Menees configuration/discovery. Its WPF window also coordinated monitoring, logging, alerts and lifetime. Replacing the UI alone would not make the application portable.

**What translated cleanly?** Literal-IP models, peer/group identity, sorted round-robin selection, stop-after-success, failure grace and outage/comment intent. The original default XML is reused; five linked-original characterization tests protect its state/rotation behavior.

**What needed rethinking?** Presentation and platform integration were rewritten in Avalonia. Monitoring gained owned cancellation and bounded concurrency; UTC timestamps remain evidence while monotonic time governs durations. Probe errors and shutdown cannot invent recovery. SQLite reload, interruption handling, storage safeguards, paginated incidents and streaming export extend the original session-only evidence.

**What surprised us?** Native minimize/restoration required a real Linux window manager, Mac archive handling affected signature integrity, Windows cold route queries needed bounded startup allowance, async UI actions could lose focus, and SQLite write failures required retaining evidence through shutdown. Actual failures and fixes are retained in the [migration log](docs/migration/MIGRATION_LOG.md).

**What did it cost?** Repository sessions began **16 October 2026**; the frozen rc.4 application was committed **22 October**. It contains **39 commits** after upstream `61ae14391151ca9f209223e62c98fae3df5482b8` (1 June 2024): reproduce with `git rev-list --count 61ae14391151ca9f209223e62c98fae3df5482b8..6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`. Work was AI-assisted. No total developer hours were measured or invented; command timings are not labor hours.

[Judge’s guide](docs/competition/JUDGES_GUIDE.md) · [Baseline](docs/migration/BASELINE.md) · [Exact legacy semantics](docs/migration/LEGACY_SEMANTICS.md) · [Migration log](docs/migration/MIGRATION_LOG.md)

## Platform verification

Frozen released application: **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, internal version **0.1.1-rc.4**. [Successful exact-source three-OS run](https://github.com/samscript18/Synups/actions/runs/39850198899).

| Actual environment | Default tests | Separate real/native | Audio request/fallback | Extracted executable |
|---|---:|---:|---:|---|
| Windows Server 2025 x64 |159 passed|3 passed|1 passed|Six stages passed|
| macOS 15 arm64 |159 passed|3 passed|1 passed|Six stages passed|
| Ubuntu 24.04 x64, Xvfb + Openbox |160 passed|3 passed|1 passed|Six stages passed|

Each OS also passed eight Python safety tests, locked restore/auditing and zero-warning Release builds. There are **164 unique .NET cases** across invocations and platforms: four opt-in cases and one Linux-only Gio case explain intentional skips. Actual packaged apphosts verify native windows, minimized/restored state, clipboard, genuine profile preview/import/backup, real loopback, pane/log preferences, configuration, SQLite restart/interruption and read-only error/repair. Five normal exits are zero; deliberate termination is separate. The downloaded Mac apphost repeated all six stages locally.

[CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) retained zero SARIF results; scoped coverage is **79.83% lines / 66.89% branches**. [Reports and hashes](docs/evidence/verification/2026-10-22-desktop-completion.json). These tests drive real controls programmatically; they are not physical tray/keyboard/native Save-panel certification. Windows/macOS audio requests completed without audible-delivery verification; Linux's missing helper produced a safe fallback. Windows native notification delivery failed independent observation and that adapter was withdrawn; in-app alerts remain. Earlier endurance and controlled-incident evidence retain their original source qualification.

Public v0.1.0 remains pinned to `2973d3349fcb5af5639597920842288e6549bbd0`: [105 default +1 opt-in per OS and packaged verification](https://github.com/samscript18/Synups/actions/runs/39682197118), [anonymous release verification](docs/evidence/verification/synups-0.1.0-publication.json). [Full platform matrix](docs/migration/CROSS_PLATFORM.md).

## Legacy parity

**Released v0.1.1 / verified rc.4: (15 complete + 3 partial × 0.5) / 18 = 91.7%.** Public v0.1.0 remains 72.2%; frozen rc.3 remains 88.9%. Existing binaries are never upgraded by changing a document.

The remaining partial areas are **alerts** (actual OS delivery/audibility/attention), **preferences** (actual next-login execution/removal) and **placement/tray** (physical menu and multiple-display acceptance). Genuine settings-profile import is now tested using a fixture emitted by the untouched original Windows executable. This is a coarse functional inventory, not percent effort or universal platform certification. [Unchanged 18-item rubric](docs/migration/FEATURE_PARITY.md).

## Known limitations

- **Distribution trust:** Windows is unsigned; Mac ad-hoc integrity is not Developer ID signing/notarization and Gatekeeper rejects it. Linux proof is X11/Xvfb, not every Wayland desktop. [Installation](docs/release/INSTALLATION.md), [signing findings](docs/release/DISTRIBUTION_TRUST.md).
- **Desktop acceptance:** macOS Accessibility/event-post permission prevents physical tray, keyboard and native Save-panel automation. [Exact human acceptance checklist](docs/release/MACOS_ACCEPTANCE.md). Native notification request acceptance does not establish delivery.
- **Evidence semantics:** ICMP is not universal Internet health. Unobserved intervals stay uncertain; stopping never recovers an incident. No fabricated uptime or historical latency series exists.
- **Data safety:** forced quit can lose uncommitted batches. Pending write failures require repair/Retry. CSV is not a full database backup; use the SQLite backup action. [Storage design](docs/migration/ARCHITECTURE.md).
- **Scope:** broader IPv6/VPN/suspend/multi-display/long-running GUI acceptance, broader settings-format compatibility and remaining OS integrations need work. This is not a production-readiness or100%-parity claim.

## Architecture and development

Three projects: **Synups.Core** (configuration, monitoring, persistence), **Synups.Desktop** (Avalonia/MVVM and OS integration), **Synups.Tests** (characterization, deterministic and headless tests). No WebView, backend or production mock probe. [Architecture](docs/migration/ARCHITECTURE.md) · [Release checklist](docs/migration/RELEASE_READINESS.md).

With the pinned .NET10.0.402 SDK and Avalonia12.1.4:

```sh
cd synups
dotnet restore ../Synups.slnx --locked-mode
dotnet build ../Synups.slnx -c Release --no-restore
dotnet test ../Synups.slnx -c Release --no-build
dotnet run --project Synups.Desktop -c Release --no-build
```

The default tests do not require a public endpoint. Explicit native/network checks: set `SYNUPS_REAL_NETWORK_TESTS=1` and filter `FullyQualifiedName~RealLoopback|FullyQualifiedName~RealDefaultGateway`. Original `WirePeep.sln` and source remain separate. Local data paths and workspace selection are described in [installation](docs/release/INSTALLATION.md).

## Licence, attribution and acknowledgements

[MIT licence](LICENSE). Original WirePeep and resources: **Bill Menees**. The original source, copyright notices and [upstream ancestry](https://github.com/menees/WirePeep/commit/61ae14391151ca9f209223e62c98fae3df5482b8) are preserved. Bundled third-party licences/notices accompany every package. Thanks to Avalonia and the original library maintainers.

[Prepared competition information](docs/release/SUBMISSION.md) answers the submission questions. Synups is built for the challenge; no official entry or acceptance is claimed. Publishing a newer release and submitting the form require separate authorization.
