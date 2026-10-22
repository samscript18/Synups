# Synups

**Know when your network drops. Know how long it lasts. Keep the evidence.**

A local-first network reliability monitor for Windows, macOS and Linux, built with **Avalonia, C# and XAML**. Synups is a migration of [Bill Menees’ WirePeep](https://github.com/menees/WirePeep), preserving its Git history, MIT licence and peer-group monitoring intent.

Built for the [2026 Avalonia Port Challenge](https://avaloniaui.net/blog/avalonia-port-challenge), targeting Best IT Pro Tool. Submission or acceptance is not claimed. ICMP reachability is evidence about configured peers, not proof that every Internet service works.

**Early release, not a production-readiness claim.** Actual self-contained executables are exercised on native Windows/macOS runners and Linux under Xvfb. Signing and everyday desktop/accessibility coverage still have explicit limits. There is no WebView, backend, account or cloud service, and no fabricated monitoring data.

## Why this migration matters

WirePeep was a Windows WPF utility for rotating peer-group ICMP checks, failure grace, outage comments and session logs. Its Common project inherited Windows targets and Menees settings; the WPF window also owned scheduling, logging and alerts. Replacing XAML alone could not make that architecture portable.

| Original WirePeep | Synups |
|---|---|
| WPF/.NET Framework and Windows-targeted .NET; Windows shell integrations | Avalonia C#/XAML, UI-free Core and explicit desktop/platform boundaries |
| Synchronous probes, coarse timer, wall-clock thresholds | Bounded async groups, finite cancellation/drain and monotonic elapsed timing |
| Named IPs, sorted rotating peers, stop after success, failure grace and comments | Those monitoring intents preserved and characterized against linked original source |
| Session-memory outage list and text exports | Durable SQLite sessions/incidents/comments, restart/interruption handling, CSV/JSON logs and verified backups |
| Unavailable errors could end a failure; shutdown could race callbacks | Probe errors reduce confidence, never manufacture recovery; stop/close owns pending work |
| No automated tests found in baseline | Exact-source three-OS **extracted executable** verification and regression tests |

**Published 0.1.0 evidence:** per OS, 105 deterministic/headless cases plus 1 separately enabled real-loopback case and 4 Python safety tests; locked restore and zero-warning Release. [Exact verified run 39682197118](https://github.com/samscript18/Synups/actions/runs/39682197118), [platform limits](docs/migration/CROSS_PLATFORM.md), [publication/hash evidence](docs/evidence/verification/synups-0.1.0-publication.json). Native Windows Server/macOS runners and Ubuntu/Xvfb are the actual scope, not every desktop. MIT attribution, original source and upstream ancestry remain intact.

| Before: Bill Menees’ upstream illustration | After: actual Synups macOS published-apphost client render |
|---|---|
| ![Original WirePeep illustration](docs/evidence/before/wirepeep-upstream.png) | ![Synups real loopback observation](docs/evidence/after/2026-10-21-apphost/osx-arm64/exercise-overview.png) |

The before image was not freshly captured here; the after image contains genuine explicit loopback measurements, not Internet-health statistics. [Image provenance](docs/evidence/README.md). **Public 0.1.0 inventory parity is 72.2%; the separately verified rc.2 reaches 75.0%=(9+9×0.5)/18** through partial gateway discovery. Modem/transceiver discovery, validated import, login/sounds and some taskbar/menu details remain missing. Windows is unsigned; Mac ad-hoc integrity is not notarization and Gatekeeper rejects trusted delivery; Linux physical/Wayland acceptance remains limited. [Judge’s guide](docs/competition/JUDGES_GUIDE.md), [exact legacy semantics](docs/migration/LEGACY_SEMANTICS.md), [architecture](docs/migration/ARCHITECTURE.md), [feature inventory](docs/migration/FEATURE_PARITY.md) and [honest migration log](docs/migration/MIGRATION_LOG.md).

**Unpublished candidate 0.1.1-rc.1:** guided first-target setup and clearer start/status guidance passed [three-OS run 39761184906](https://github.com/samscript18/Synups/actions/runs/39761184906), source `65d917ac645139836cf97e48c48c137a09825942`: per OS **107 deterministic/headless + 1 separate real-loopback + 8 Python tests**, zero-warning Release and six actual extracted executable stages. [Independent audit](docs/evidence/verification/2026-10-22-competitive-candidate-final.json). These changes are separate from the unchanged public 0.1.0 packages. The final candidate also fixes fresh-window clipping confirmed on Windows; [candidate notes and hashes](docs/release/COMPETITIVE_CANDIDATE.md).

**Real incident demonstration:** an isolated Linux namespace dropped actual loopback ICMP replies, then restored them. The production probe/engine recorded grace, one confirmed incident, one recovery, a persisted comment and the same incident after process restart. [Genuine screenshots, exact timestamps, exported CSV and reproduction](docs/competition/REAL_INCIDENT.md). This is controlled kernel-level packet loss, not injected probe data or an Internet/provider outage. No user network was altered.

**Final hardening work (unpublished 0.1.1-rc.2):** explicit read-only gateway preview/confirmation, a paged real-incident timeline, complete streaming CSV, clipboard actions, native Help/About, shortcuts and opt-in event focus are implemented. Exact application source `6446990571c285b5c2e2a4bb293cbd8e3af40171` passed [three-OS run 39782799326](https://github.com/samscript18/Synups/actions/runs/39782799326): **126 deterministic/headless + 2 separately enabled loopback/native-route + 8 Python cases per OS**, zero-warning Release and six extracted-executable stages. [Reviewed candidate, checksums and limits](docs/release/FINAL_HARDENING_CANDIDATE.md). [CodeQL](https://github.com/samscript18/Synups/actions/runs/39782799390) retained zero findings; scoped coverage is 80.63% lines / 67.82% branches. Physical desktop acceptance remains separate. Public 0.1.0 and frozen 0.1.1-rc.1 remain rollback baselines.

**Legacy-completion rc.3 is under separate verification:** launch-minimized compatibility, status/row copy menus and keyboard focus, explicit original candidate checks/guided defaults, opt-in login registration and dated log names. Existing release/rc.2 results do not certify these new binaries; final source-specific evidence will be linked after its own gates.

## Download and run

Download **[Synups 0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0)** using the matching platform link below. These are public Release assets: no GitHub account or SDK is required. Verify the archive against [SHA256SUMS](https://github.com/samscript18/Synups/releases/download/v0.1.0/SHA256SUMS), then extract the complete archive. The original MIT licence, dependency notices, runtime/native SQLite and source/file manifest are included.

| Download | Start the extracted application | Actual verification scope / signing limits |
|---|---|---|
| [Windows x64 ZIP](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-win-x64.zip) | `Synups.exe` | Windows Server 2025 CI; ordinary Windows desktops need further acceptance review. **Unsigned**; SmartScreen/organization policy may block. |
| [macOS arm64 tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-osx-arm64.tar.gz) | `Synups.app`, or `./Synups.app/Contents/MacOS/Synups` | macOS 15 CI and local macOS 26.3.1. **Ad-hoc integrity only; no Developer ID/notarization. Gatekeeper rejects trusted delivery.** |
| [Linux x64 tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-linux-x64.tar.gz) | `./Synups` | Ubuntu 24.04/X11 under Xvfb; real desktops/Wayland need separate checks. Native display/font/graphics libraries required. |

Exact release/tag/application source: **2973d3349fcb5af5639597920842288e6549bbd0**. All 15 release assets were downloaded anonymously and checked against the reviewed hashes; archives were not rebuilt or replaced. [Publication verification](docs/evidence/verification/synups-0.1.0-publication.json), [artifact hashes](docs/release/VERIFIED_ARTIFACTS.md), [installation](docs/release/INSTALLATION.md) and [release notes](docs/release/RELEASE_BODY.md). Later documentation commits do not change the release binaries. Bundled handoff documents retain their pre-publication wording; this README and the release page record current status. [Competition information](docs/release/SUBMISSION.md) remains unsubmitted.

The .NET runtime, Avalonia native assets and SQLite are included. Keep the entire extracted folder/bundle together. An SDK is unnecessary for these packages. Installers, Intel Mac/ARM Windows/Linux builds and trusted publisher signatures are not provided. Do not disable OS security to run a candidate.

## What it does

- Real ICMP checks with finite timeouts, actual response latency and typed errors.
- Editable literal-IP targets, peer groups, intervals, failure grace and enablement; atomically saved profiles.
- Live status that distinguishes connected, disconnected, unavailable, stale and not checked.
- Confirmed incidents, observed recoveries, comments and sessions retained in local SQLite; quoted UTC CSV export.
- Automatic JSON/CSV session logs and observed-duration summaries, configurable log location, database capacity, backups and conservative retention.
- Desktop preferences, transition alerts, tray policy and per-workspace single-instance restoration. Windows uses a clearly labelled in-app notification fallback; optional macOS/Linux native requests do not guarantee OS delivery.

Opening the app stays stopped unless you explicitly save monitoring-on-launch. Add a group and a named literal IP, enable/apply it, then Start. Stop before editing. Restoring WirePeep defaults reuses the original XML addresses/timings; discovery-only groups remain disabled. No startup discovery or subnet scanning runs. Unpublished rc.2 adds explicit read-only gateway preview and confirmation; public 0.1.0 does not include that feature.

A group stops probing after its first responding peer. Other peers can be stale/not checked; one response does not prove every target or Internet service is online. Probe errors reduce confidence and cannot recover an outage. Stop/close ends observation without inventing recovery; a new session never retroactively recovers an older incident.

Data stays under the per-user `.NET LocalApplicationData/Synups` folder; paths are shown in-app. `--data-directory ABSOLUTE_PATH` selects an independent local workspace. Original WirePeep settings are not modified. Pending write failures retain unsaved evidence in memory and require repair/Retry. A forced quit can lose uncommitted batches. Use the full SQLite backup action; outage CSV is not a database backup. Storage/retention and recovery details: [architecture](docs/migration/ARCHITECTURE.md).

## Genuine application captures

These are actual published-apphost client-area renders from explicit real loopback observations, not mock dashboards or OS desktop screenshots. The measurements are not an Internet benchmark. Exact source, hashes and limits are in the [evidence records](docs/evidence/README.md).

| Windows | macOS | Linux / Xvfb |
|---|---|---|
| ![Synups Windows](docs/evidence/after/2026-10-21-apphost/win-x64/exercise-overview.png) | ![Synups macOS](docs/evidence/after/2026-10-21-apphost/osx-arm64/exercise-overview.png) | ![Synups Linux](docs/evidence/after/2026-10-21-apphost/linux-x64/exercise-overview.png) |

[Before: original WirePeep illustration](docs/evidence/before/wirepeep-upstream.png), published by Bill Menees and retained with attribution. It is an upstream illustration, not a freshly captured Windows baseline run. [Before/after provenance](docs/evidence/README.md) preserves that distinction.

## Verification and remaining gates

[Cross-platform evidence](docs/migration/CROSS_PLATFORM.md) records exact commits, actual run/job links and downloaded artifact hashes. For the frozen public 0.1.0 source, the workflow performed locked restore/audit, zero-warning Synups Release build, 105 deterministic/headless cases, the separately enabled real-loopback case, four archive/licence checks, self-contained packaging, **extracted apphost execution**, and the older component-native harness. The default suite intentionally skips its one opt-in network case; all 106 unique .NET cases are exercised across the two invocations.

The actual apphost check drives the normal app’s controls and real services in isolated data: configuration CRUD/validation, real loopback, Stop, preferences, second-process restoration, clean restart, explicit forced termination, interrupted-history reload without recovery, real read-only SQLite error and repair. It checks the executing PID/path/hash, bundled runtime location, native window handle, SQLite integrity and exit codes. Test harness assemblies are excluded from packages. This is programmatic control automation, not physical keyboard/mouse, native save-picker, tray-menu, screen-reader or notification-delivery certification.

A [two-hour real-Core observation](docs/evidence/verification/2026-10-21-engine-endurance-macos-arm64.json) passed with 28,792 loopback probes, no failures/errors, 1.07% mean CPU on one core, 85.5 MiB peak RSS and clean stop/restart/reopen. Source/build qualification is explicit; this is not multi-hour GUI or leak-free certification. Two separate five-minute published-GUI observations are recorded independently.

The [release checklist](docs/migration/RELEASE_READINESS.md) is authoritative for current readiness. The [desktop acceptance protocol](docs/release/DESKTOP_VALIDATION.md) lists remaining human checks. Known gaps include trusted macOS delivery, physical tray/notification/accessibility review, broader IPv6/VPN/suspend/permissions coverage, representative large-history/long-running GUI workloads, validated legacy settings import, modem/transceiver discovery, login registration, sound/taskbar and remaining menu details. The unpublished rc.2 adds partial gateway discovery, copy/help and opt-in event focus. Windows native notifications use the documented fallback. No multi-platform production-support or full legacy-parity claim is made.

## Build from source

Pinned SDK: .NET 10.0.402; Avalonia 12.1.4. From the repository root:

```sh
cd synups
dotnet restore ../Synups.slnx --locked-mode
dotnet build ../Synups.slnx -c Release --no-restore
dotnet test ../Synups.slnx -c Release --no-build
dotnet run --project Synups.Desktop -c Release --no-build
```

The SDK pin is scoped to `synups/`; the original `WirePeep.sln` remains separate. The test suite does not depend on a public IP. Explicit opt-in networking: set `SYNUPS_REAL_NETWORK_TESTS=1` and filter `FullyQualifiedName~RealLoopback`. Reproducible package/apphost/resource checks are in `eng/`; platform prerequisites and usage are documented with the evidence.

## Migration write-up

Original baseline: [61ae14391151ca9f209223e62c98fae3df5482b8](https://github.com/menees/WirePeep/commit/61ae14391151ca9f209223e62c98fae3df5482b8), June 2024, before this challenge. Original source, WPF solution/build scripts, assets, licence and commit ancestry remain. [Original README](https://github.com/menees/WirePeep/blob/61ae14391151ca9f209223e62c98fae3df5482b8/README.md).

WirePeep.Common separated useful monitoring logic, but inherited Windows frameworks and coupled configuration to Menees settings/discovery. The WPF window also owned scheduling, logging, outages and alerts. Reusing Common unchanged would not produce an independent cross-platform engine. The migration therefore reuses its default XML and characterized algorithms, ports monitoring intent, and rewrites presentation/lifetime and platform boundaries.

The solution has three projects: **Core** for configuration, monitoring and persistence; **Desktop** for Avalonia MVVM and OS integrations; **Tests** for linked-original characterization, deterministic regressions and headless UI. Manual composition suffices; no DI framework, backend or extra production project was introduced. Independent async groups preserve sorted round-robin selection and stop-on-success with bounded concurrency. Monotonic timing governs intervals/durations, with UTC evidence and explicit uncertainty/freshness.

Intentional corrections are tested and documented: unavailable/error results cannot falsely recover a confirmed outage, mixed failure/error groups become indeterminate, large observation gaps reset unconfirmed failure grace, and ordinary shutdown cannot manufacture recovery. Durable SQLite reload, interrupted-session records, backups/capacity safeguards and group enablement are Synups enhancements. [Exact legacy semantics](docs/migration/LEGACY_SEMANTICS.md) and [parity inventory](docs/migration/FEATURE_PARITY.md) distinguish them from retained functionality; the public baseline scores **72.2%** and the unpublished rc.2 scores **75.0%** under the same inventory rubric, not an effort estimate.

Actual difficulties include Windows-only baseline execution, first-time SDK/NuGet restrictions, xUnit version compatibility, real storage/deadline contention, truthful unavailable-state migration, safe pending-write shutdown, failed screenshot capture, an action-readiness race, Windows licence newline conversion, macOS archive signature loss and native minimize/restoration behavior. These failures and tested corrections remain in the [migration log](docs/migration/MIGRATION_LOG.md). Native component execution was explicitly kept separate from later published-executable proof. Code-authored icons and a macOS bundle now establish identity without claiming publisher trust.

This is AI-assisted repository work across interrupted sessions. No total development hours were instrumented or invented. Command timings and measured observations are evidence, not labor hours. The [baseline](docs/migration/BASELINE.md), [architecture](docs/migration/ARCHITECTURE.md), [platform matrix](docs/migration/CROSS_PLATFORM.md), [migration log](docs/migration/MIGRATION_LOG.md), [roadmap](docs/migration/ROADMAP.md) and [release checklist](docs/migration/RELEASE_READINESS.md) retain actual scope and outstanding work. No public release or competition entry is submitted automatically.

## Direct answers to the migration write-up questions

The [official challenge](https://avaloniaui.net/blog/avalonia-port-challenge) asks for an honest account of effort, reuse, redesign and surprises.

**How much work did it take?** Session records begin on **16 October 2026**; the frozen application checkpoint `65d917ac645139836cf97e48c48c137a09825942` was committed **22 October 2026**. It contains **19 commits after upstream `61ae14391151ca9f209223e62c98fae3df5482b8`**, whose commit date is **1 June 2024**. Reproduce that count with `git rev-list --count 61ae14391151ca9f209223e62c98fae3df5482b8..65d917ac645139836cf97e48c48c137a09825942`. These are calendar/source facts, not developer-hours estimates. The final rc.2 source `6446990571c285b5c2e2a4bb293cbd8e3af40171` contains **23 commits** after the same upstream (`git rev-list --count 61ae14391151ca9f209223e62c98fae3df5482b8..6446990571c285b5c2e2a4bb293cbd8e3af40171`), also dated 22 October. These counts pin application checkpoints and exclude later evidence-only commits. No total labor hours were measured.

**What translated cleanly?** Literal-IP configuration, peer names/IDs, sorted round-robin selection, stop-after-success, failure grace and outage/comment intent. The original XML is reused directly, and five linked-original characterization tests protect state/rotation behavior. Bill Menees’ MIT attribution and Git ancestry remain.

**What needed rethinking?** Common inherited Windows targets and Menees settings/discovery; WPF owned timer, logging and UI callbacks. Avalonia presentation and desktop lifetime were rewritten, probes became cancellable bounded tasks, immutable session configuration replaced live collection mutation, and SQLite became the durable evidence source. Error uncertainty, monotonic time and shutdown ownership needed explicit rules. New paginated history/streaming exports keep the timeline independent of an ever-growing snapshot cache. Discovery reads OS routes only after an explicit request; it preserves the useful gateway intent without TTL probing or startup scans.

**What caught you by surprise?** The legacy unavailable result could falsely end an outage. Real Linux OUTPUT filtering returned PermissionDenied rather than a timeout, so the isolated test had to drop incoming replies. Windows clamped native geometry while retaining larger requested layout dimensions; actual screenshot review caught clipping that green component tests missed. A missing IPv6 default route also caused Windows’ combined route query to discard IPv4 output. macOS execution and ad-hoc integrity did not imply Gatekeeper trust, while archive layout could invalidate a signature. Native UI, physical interaction, permission denial and signed distribution require separate evidence.

**AI assistance and limits:** AI assisted source analysis, C#/XAML/scripts, tests and documentation across interrupted sessions. Compiler/test outputs, native process records, hashes and screenshots are real; generated code was exercised and failures were retained. Fakes and synthetic storage fixtures are confined to tests/explicit engineering tools, never production measurements. OS notification delivery, trusted signing, physical desktop/accessibility acceptance and missing legacy integrations are not claimed from passing unit tests.

## Licence and attribution

The original [MIT licence](LICENSE), Bill Menees’ copyright/attribution and upstream Git history are preserved. Distributed candidates include runtime/native/font dependency notices and their publisher texts. Synups targets the Avalonia Port Challenge’s Best IT Pro Tool category, with Cross-Platform Port and Legacy Revival as secondary aims. It does not imply endorsement by WirePeep’s author or Avalonia.
