# Synups 0.1.1 — verified rc.4 build

**Published and available for download.** Public tag `v0.1.1` points to **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**. The downloads contain the unchanged, verified **0.1.1-rc.4** application. Help/About, assembly identification and embedded manifests retain that internal version; the public release name does not patch or relabel the executable.

Know when your network drops. Know how long it lasts. Keep the evidence.

Synups is a local-first Avalonia network reliability monitor migrated from Bill Menees' WirePeep. Record confirmed ICMP incidents, observed recoveries, comments and durable local evidence. Reachability to configured peers does not prove whole-Internet availability.

The README inside each frozen archive is the original build-time documentation snapshot and still discusses the older rc.3 proposal (88.9%). It is intentionally preserved byte-for-byte; use the accompanying current RELEASE_NOTES.md, INSTALLATION.md and VERIFICATION.json for this rc.4 payload (91.7%). Application identity is established by Help/About and ARTIFACT_MANIFEST.json, both identifying rc.4 and its exact source.

## Download and run

| Platform | Complete portable archive | Launch / important limit |
|---|---|---|
| Windows x64 | [Synups-win-x64.zip](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip) | Extract all files, run `Synups.exe`. Unsigned; Windows/organizational policy may block it. |
| macOS arm64 | [Synups-osx-arm64.tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz) | Extract `Synups.app`. Ad-hoc integrity only; not notarized, and Gatekeeper rejects trusted distribution. Do not bypass OS protections. |
| Linux x64 | [Synups-linux-x64.tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz) | Retain executable permissions and run `./Synups` in a supported graphical session with required native libraries. |

[SHA256SUMS](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS) · [installation guidance](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/release/INSTALLATION.md) · [judge’s quick tour](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/competition/JUDGES_GUIDE.md). No account, backend or .NET SDK is required; the runtime, SQLite and licences/notices are bundled. Keep the complete extracted directory together.

For a quick check, create a named peer group and a literal IP you are authorized to monitor, choose **Start monitoring**, review real results/freshness, then inspect **Activity and outages** and CSV if an actual incident exists. Stop before editing. Restart to confirm saved settings/history. An empty history is valid; missing replies do not prove a whole-Internet outage.

## Highlights since v0.1.0

- Guided first-target setup with clear Start, unknown, stale and error states.
- Genuine WirePeep profile import from the actual Menees 5.1.2 `.stgx` schema: read-only preview, explicit replacement confirmation, destination backup and preserved GUIDs/timings. Unsupported formats are refused; original settings/history/OS preferences remain untouched. Import never starts probing.
- Native gateway preview and confirmed, bounded original modem/transceiver candidate checks; no subnet scan or startup discovery.
- Paginated durable incident timeline, comments and streaming chronological CSV export.
- Clipboard/value/row actions, Help/About, shortcuts, minimized launch and same-workspace restoration.
- Optional bounded local WAV requests, explicit reversible login registration controls, log-naming modes and persistent pane sizes. Actual audible output and next-login execution remain unverified.
- Original peer selection/failure-grace intent, real ICMP, safe cancellation, SQLite durability and MIT provenance preserved.

**Legacy inventory: (15 complete + 3 partial × 0.5) / 18 = 91.7%.** Alerts, preferences and placement/tray remain partial for the specific delivery/login/physical-interaction gaps below. This is not a 100% parity or universal platform-certification claim.

## Packages and integrity

Extract the complete platform archive and keep its files together. .NET, Avalonia, native SQLite, original MIT licence and dependency notices are included; no account, backend or SDK is required. Verify every asset using SHA256SUMS. These three hashes are identical to the frozen rc.4 candidate:

| Archive | SHA-256 |
|---|---|
| Synups-win-x64.zip | `cc02117abc393cc0e6baf39e75aceebf529c8d695e5d61e93a18262b36a9d570` |
| Synups-osx-arm64.tar.gz | `fa00e6a06255b4cd116ecb985806a05a02fb4e93db6977e34420a63f0cc31200` |
| Synups-linux-x64.tar.gz | `49387bf6358b8d06478a64091f8780a64f41861f820df8f0ee1b04e17117c757` |

[Exact upload manifest](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/release/v0.1.1/ASSET_MANIFEST.json). Supplemental installation, submission and publication documents are newer than the application; they identify this exact payload and do not modify it. VERIFICATION.json and retained reports identify the actual application source.

## Actual verification

[Exact-source CI run 39850198899](https://github.com/samscript18/Synups/actions/runs/39850198899): audited locked restore, zero-warning Release builds, eight Python safety tests per OS and:

| Native environment | Default .NET tests | Separate real/native | Audio request/fallback | Actual extracted apphost |
|---|---:|---:|---:|---|
| Windows Server 2025 x64 | 159 passed | 3 passed | 1 passed | Six stages passed |
| macOS 15 arm64 | 159 passed | 3 passed | 1 passed | Six stages passed |
| Ubuntu 24.04 x64 / Xvfb + Openbox | 160 passed | 3 passed | 1 passed | Six stages passed |

There are 164 unique .NET cases across invocations/platforms; four opt-in cases and one Linux-only case explain intentional default skips. Actual packaged executable checks cover windows/restoration, clipboard, genuine profile preview/import/backup, real loopback, preferences, SQLite restart/interruption and read-only failure/repair. Five normal exits are zero; deliberate termination is recorded separately. The same downloaded Mac apphost also passed locally. Programmatic real controls are not physical tray, keyboard or native Save-panel certification.

[Exact-source CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) retains zero SARIF results. Scoped coverage is 79.83% lines / 66.89% branches. Original Windows screenshot and genuine emitted settings fixture have separate upstream-source provenance. The controlled incident demonstration retains its original rc.3 source qualification; it is not relabelled as a new rc.4 outage.

[Migration README](https://github.com/samscript18/Synups#readme) · [Candidate and limitations](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/release/DESKTOP_COMPLETION_CANDIDATE.md) · [Before/after provenance](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/evidence/README.md) · [Real incident sequence](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/competition/INCIDENT_DEMO.md)

## Installation and remaining limits

- **Windows:** run `Synups.exe`. Unsigned portable ZIP, no installer; SmartScreen or organizational policy may block it.
- **macOS:** open `Synups.app`. Ad-hoc integrity only, **not Developer ID signed/notarized; Gatekeeper rejects trusted distribution**. Follow INSTALLATION.md and organizational policy. Do not disable OS security.
- **Linux:** run `./Synups` in a graphical session; retain executable permissions and supply the documented desktop libraries. Verification is Ubuntu/X11/Xvfb/Openbox, not every Wayland desktop.
- **Alerts:** Windows native delivery failed independent on-screen observation and that adapter was withdrawn. Accessible in-app alerts remain. macOS/Linux native requests do not establish delivery. Windows/macOS sound processes completed without verified audible output; Linux reported the missing-helper fallback.
- **Preferences:** explicit login enable/remove and log/pane settings are implemented; actual next-login execution/removal remains unverified. No startup registration is changed automatically.
- **Placement/tray:** physical Show/Exit, multiple-display restoration, native Save-panel and keyboard acceptance remain unverified. macOS automation permissions are denied and the owner was unavailable for manual checks.

Broader IPv6/VPN/suspend/accessibility/endurance remain limited. No simulated monitoring or invented recovery is introduced. Back up history through Synups; CSV is not a full database backup. Repair pending storage errors before clean close rather than force quitting.

## Separate diagnostics development

Network diagnostics and incident investigation capabilities are under development separately in v0.2.0-alpha.1. These features are not included in the v0.1.1 downloads.

[Diagnostics branch](https://github.com/samscript18/Synups/tree/feat/network-diagnostics) · [source-specific alpha hardening report](https://github.com/samscript18/Synups/blob/feat/network-diagnostics/docs/diagnostics/HARDENING_REPORT.md). Alpha features, screenshots, test counts and hashes do not change this release’s 91.7% legacy score or its verification results.

Original WirePeep is by **Bill Menees**, retained under the [MIT licence](https://github.com/samscript18/Synups/blob/6c812fde9a29eeb8cfd6399a2f1a75e333e81e03/LICENSE). [Original project](https://github.com/menees/WirePeep) and source ancestry remain preserved. Public v0.1.0 and all historical candidates remain unchanged rollback baselines. Publishing or editing this release description does not submit the competition entry.
