# Synups

Synups is a migration of [WirePeep](https://github.com/menees/WirePeep) into a modern cross-platform network reliability monitor using **real Avalonia, C# and XAML**. Primary competition category: Best IT Pro Tool.

**Current status: Phase 2 functional monitoring.** The Avalonia application performs real ICMP checks, evaluates peer groups, records outages and recoveries, edits and persists profiles, and exports session outage history with comments. OS alerts, saved-WirePeep settings import and several legacy desktop preferences remain unported. No production measurements or statistics are fabricated. No backend, cloud service, paid component or WebView is used.

## Build and run

Install the .NET SDK specified in [synups/global.json](synups/global.json) (10.0.402). Avalonia packages are pinned to 12.1.4. From the repository root:

```sh
cd synups
dotnet restore ../Synups.slnx --locked-mode
dotnet build ../Synups.slnx -c Release --no-restore
dotnet test ../Synups.slnx -c Release --no-build
dotnet run --project Synups.Desktop -c Release --no-build
```

Verified on macOS 26.3.1 arm64: deterministic and headless tests, real loopback ICMP, and a native Avalonia start/measurement/stop/close flow. Detailed counts and final build results are in the migration log. Visual screenshot capture remains unverified.

The SDK pin is scoped to synups/ so original WirePeep builds keep their existing SDK selection. Native desktop prerequisites vary by OS; see [platform evidence and risks](docs/migration/CROSS_PLATFORM.md). Windows and Linux runtime support is a target, not a tested claim. No downloadable release builds exist yet.

## Use Synups

Monitoring stays stopped when the application opens. In **Targets and groups**, add a disabled group, add named literal IPv4/IPv6 targets, then enable the group. Alternatively restore the original WirePeep defaults. Its 15 literal Internet targets and timing values come from the actual upstream XML; three discovery-only local-device groups remain disabled until you add targets. No discovery or network scanning runs.

**Apply group/target** saves each edit immediately. Configuration uses versioned JSON under the OS per-user LocalApplicationData directory in `Synups/configuration.v1.json`; the exact path is shown in the app. Names, IDs, addresses and timing limits are validated. Corrupt files are preserved: retry after fixing them or explicitly recover with defaults, which creates a backup. Original WirePeep settings are never modified. Stop monitoring before editing; moving targets preserves their IDs. Removing the last target disables its group.

Select **Start monitoring**. Overview shows actual group counts, uncertainty and the latest measurement time. A group stops checking peers after the first response; other targets may be stale or not checked. Target rows show last check, last success, real RTT where available and useful errors. ICMP reachability does not establish that all network services work.

**Activity and outages** records confirmed outages, recoveries and probe errors. A probe error makes confidence indeterminate and cannot recover a confirmed outage; only a successful probe does that. Outage duration begins at confirmation, with earlier failed observation recorded separately. Large observation gaps are marked. Stop ends observation without fabricating recovery. History/comments survive Start/Stop for this process only: **export CSV before closing** to retain them. There is no durable history reload or automatic legacy text log yet.

## Verification

The normal test suite does not require a public IP or run real ICMP. From `synups/`, explicitly run the real loopback probe/engine test:

```sh
SYNUPS_REAL_NETWORK_TESTS=1 dotnet test ../Synups.slnx -c Release --no-build --filter FullyQualifiedName~RealLoopback
```

From the repository root, `python3 eng/native-smoke.py --dotnet /path/to/dotnet` verifies the native application and real loopback UI flow using temporary configuration. This is a verification harness, not a production simulator. Native Windows/Linux runs and external outage/recovery verification are pending; three-OS CI is configured but has not run against these unpublished changes.

## Migration write-up — work completed so far

Original baseline: [`61ae14391151ca9f209223e62c98fae3df5482b8`](https://github.com/menees/WirePeep/commit/61ae14391151ca9f209223e62c98fae3df5482b8), authored by Bill Menees in June 2024, before the challenge. This repository retains that Git history and the original WPF implementation, solution, build scripts, assets and MIT licence. The original project description is available [at the baseline commit](https://github.com/menees/WirePeep/blob/61ae14391151ca9f209223e62c98fae3df5482b8/README.md).

WirePeep already separates its synchronous ICMP engine into WirePeep.Common. That separation helps, but Common inherits Windows framework targets and couples configuration to Menees settings APIs. Its Profile constructor can perform device discovery. The WPF MainWindow also owns scheduling, outage history, logging and alert orchestration. This migration therefore requires service extraction as well as a presentation rewrite.

Phase 1 kept legacy sources unchanged and created a separate solution with Core, Desktop and Tests. It reuses the original default XML directly as an embedded resource and preserves its address naming and timing values. A new side-effect-free reader replaces Profile construction for configuration inspection. An observable presentation model drives a native resizable Avalonia window with Fluent controls, shared dark-theme tokens, keyboard-selectable navigation and an explicit setup/error state. There is no DI container because manual composition is sufficient for the current scope. Core now contains separate monitoring and persistence services; Desktop contains only orchestration and presentation.

Phase 2 ports name-sorted round-robin polling, stop-on-success, stale samples, fail thresholds, recovery, outage comments, CSV export and profile persistence. Deterministic tests also link original state machines directly. Deliberate changes use monotonic elapsed time, retain outages across probe errors, make mixed-error groups order-independent and drain background work on shutdown. The UI pulls snapshots without a dispatcher backlog. No production simulated measurements are present. Windows-specific tray, sound, login launch and taskbar behavior need capability-specific replacements. Durable history reload is an enhancement, not a feature already delivered. The coarse legacy inventory parity is **61.1%**; its explicit rubric and remaining gaps are in FEATURE_PARITY.md.

Engineering issues and actual verification results, including failed attempts and fixes, are recorded in the log below. Effort is described through performed work and measured command timings; no development-hour estimate is invented.

- [Original baseline and build evidence](docs/migration/BASELINE.md)
- [Feature implementation inventory and parity](docs/migration/FEATURE_PARITY.md)
- [Legacy state-transition characterization](docs/migration/LEGACY_SEMANTICS.md)
- [Architecture and design decisions](docs/migration/ARCHITECTURE.md)
- [Actual migration log and verification](docs/migration/MIGRATION_LOG.md)
- [Cross-platform evidence and risks](docs/migration/CROSS_PLATFORM.md)
- [Roadmap and remaining submission gates](docs/migration/ROADMAP.md)

## Screenshots and challenge readiness

No before/after screenshots have been fabricated. The [original upstream README screenshot](http://www.menees.com/Images/WirePeep.png) is an upstream illustration, not a capture from this session. A fresh original WPF capture requires Windows. Native Synups captures and runnable verified release artifacts for each claimed platform are still submission gates.

The [Avalonia Port Challenge](https://avaloniaui.net/blog/avalonia-port-challenge) closes October 23, 2026 at 23:59 UTC. Synups is not a finished competition entry. No submission or publication was performed.

## Licence and attribution

[MIT](LICENSE). Original WirePeep copyright (c) 2020 Bill Menees; the original licence remains verbatim. Original source/resource notices remain intact. Synups is a derivative migration and does not claim authorship of WirePeep. Avalonia and bundled font dependencies retain their respective package licences.
