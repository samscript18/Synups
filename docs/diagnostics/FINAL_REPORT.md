# Synups diagnostics final verification report

> Frozen e16e3cd baseline report. See [final hardening](HARDENING_REPORT.md) for follow-up changes, verification and the current release decision.

**CONDITIONAL GO for the isolated diagnostics alpha.** The complete31-feature expansion is not finished. Core diagnostics, incident evidence, safe migration, restart and exports have source-specific native evidence on Windows x64, Linux x64 and macOS arm64. Unfinished optional capabilities remain unavailable. Publicv0.1.1 and the competition entry were not changed.

## A. Implementation and source revisions

- Starting clean branch/HEAD: `feat/avalonia-migration`, `bbd92ef7395ae4cd20534920398b317353e41371`.
- Protected release application: `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`, publicv0.1.1, internal0.1.1-rc.4.
- Isolated branch: `feat/network-diagnostics`; candidate version0.2.0-alpha.1.
- Final frozen application: `e16e3cd4209ec12bf2150a194373c30efcdf744f`. Documentation/evidence commits after it do not rebuild or change the application.
- Earlier candidates0501106 and6ac4c9c are preserved with their failures/corrections, not rewritten.
- Feature totals: **4 VERIFIED documentation features,19 PARTIALLY_VERIFIED,1 BLOCKED,7 DEFERRED**. Conservative whole-feature status is separate from successful individual native scenarios. The fully detailed [31-row matrix](FEATURE_MATRIX.md) identifies production/UI/test paths, OS evidence, limitations and eligibility.

| ID | Feature | Status | Remaining boundary |
|---|---|---|---|
| F01 | Traceroute and Network Path Analysis | PARTIALLY_VERIFIED | Real loopback TTL and saved-hop fixtures; no multi-hop/public IPv6 native certification |
| F02 | Latency, Jitter and Packet Loss | PARTIALLY_VERIFIED | Native loopback and controlled Linux namespace loss; broader physical networks untested |
| F03 | DNS Diagnostics | PARTIALLY_VERIFIED | UDP only; truncated→Unsupported; no split DNS policy inference |
| F04 | TCP Connectivity | PARTIALLY_VERIFIED | Native connect/refusal; timeout/unreachable OS cases not exhaustively induced |
| F05 | Network Configuration Inspection | PARTIALLY_VERIFIED | Default routes only; full routing table omitted; partial OS fields explicit |
| F06 | Protocol-Aware Probing | PARTIALLY_VERIFIED | ICMP/TCP connect real; raw SYN/UDP return Unsupported and are not UI options |
| F07 | ICMP Rate-Limit Heuristics | PARTIALLY_VERIFIED | Heuristic interpretations only; time/protocol samples cannot prove rate limiting |
| F08 | DNS Timeout and Cache Control | PARTIALLY_VERIFIED | Off by default; representative/hop, capped64 queries/cache32/concurrency1; no PTR physical UI test |
| F09 | Diagnostic Reports Linked to Incidents | PARTIALLY_VERIFIED | Links at creation; attaching a previously standalone immutable session is not implemented |
| F10 | Incident Diagnostic Snapshot | PARTIALLY_VERIFIED | Controlled Linux incident, real TCP/ICMP, typed DNS Unsupported for literal target, recovery/restart/export passed at 6ac4c9c |
| F11 | Evidence Bundle Export | PARTIALLY_VERIFIED | Three-format exact-byte hashes/native exports and PDF parse/render/bounds QA pass; non-ASCII escaped |
| F12 | Saved Baselines and Comparisons | PARTIALLY_VERIFIED | No baseline deletion/management UI; incomplete measurements explicitly unknown |
| F13 | ASN and Network Ownership Enrichment | DEFERRED | Deferred behind core acceptance gates; no ASN provider enabled |
| F14 | Privacy-Bounded Packet Capture | BLOCKED | No verified payload-safe filtered native capture adapter; disabled on all platforms |
| F15 | Remote Probe Corroboration | DEFERRED | No remote provider configured or consent/external integration verified |
| F16 | Proactive Diagnostic Scheduling | DEFERRED | Dedicated scheduler/trends paused pending core acceptance; monitoring scheduler unchanged |
| F17 | Platform-Specific Network Providers | PARTIALLY_VERIFIED | Reuse .NET + bounded native read-only commands; full OS-specific behavior pending |
| F18 | Updated Parity Audit | VERIFIED | Unchanged91.7%; diagnostics separate |
| F19 | Stability Documentation | VERIFIED | Documents untested failure simulations honestly |
| F20 | AOT/Trimming and Additional Platforms | PARTIALLY_VERIFIED | Untrimmed/non-AOT; additional RIDs build-only until executed on matching hardware |
| F21 | Architecture Documentation | VERIFIED | No imaginary providers/scheduler described as implemented |
| F22 | Threshold Alerts | DEFERRED | No threshold alerts; legacy event alerts retained; suitable sampling required |
| F23 | Historical Charts | DEFERRED | No new historical charts/downsampling; current evidence/timeline retained |
| F24 | Tray Controls and Shortcuts | PARTIALLY_VERIFIED | Dynamic tray health indicator and configurable/global shortcuts incomplete; physical tray/native delivery unverified |
| F25 | Diagnostic Profiles | PARTIALLY_VERIFIED | Built-ins/custom settings and preview persist; targets remain explicitly user-selected; advanced capabilities unavailable |
| F26 | Compact/Expanded View | DEFERRED | Separate compact monitoring window not implemented |
| F27 | Package Manager Distribution | DEFERRED | No WinGet/Homebrew/.deb candidate submitted or advertised as installable |
| F28 | Security and Privacy Documentation | VERIFIED | No live capture/enrichment/remote transmission enabled |
| F29 | Release Metadata | PARTIALLY_VERIFIED | Candidate source manifest/checksums; no package-manager metadata/publication |
| F30 | Comprehensive Tests | PARTIALLY_VERIFIED | Not every mandatory fault simulation/native condition exercised |
| F31 | Cross-Platform CI | PARTIALLY_VERIFIED | Source-specific native pipeline executed; final job status in FINAL_REPORT; physical desktop input untested |

## B. Functional demonstration

The final CI run [39869158253](https://github.com/samscript18/Synups/actions/runs/39869158253) passed all six jobs. Downloaded artifact SHA-256 digests, archive CRCs, every package file manifest and source/RID/version identity were verified independently after download. Each native extracted executable performed real loopback TCP, ICMP quality, destination TTL trace and read-only configuration, persisted observations, exported exact redacted JSON/Markdown/PDF and reopened the session after process restart. These are real application controls and OS windows; physical input is explicitly not claimed.

The Linux namespace test produced a genuine ICMP incident after the configured grace period, selected that actual incident, ran linked diagnostics, preserved three unanswered ICMP probes while a TCP handshake succeeded, exported evidence, restored replies, observed recovery and reopened/exported the original session after process restart. The ICMP filter was restricted to a disposable loopback-only namespace; no host route/firewall/network setting was changed. Literal-IP forward DNS is recorded as Unsupported, never fabricated as a successful answer. Independent configured-resolver example.com A queries passed on all three native platforms; DNS A/AAAA, RCODE and malformed/oversized response fixtures use actual local UDP sockets and explicitly synthetic authoritative test data.

Two complete real loopback sessions on copies of genuinev0.1.1 data saved a successful baseline, compared a later compatible session, exported all formats and reopened exact results in a separate process. Source archive/config/preferences hashes and existing incident/comment payload hashes stayed unchanged. Final-source copied Linux CI incident data was executed on macOS; this is identified separately from the native Linux controlled incident. An independent copied macOS archive upgrade also passed.

Privacy checks confirmed redaction before rendering, exact preview/Markdown agreement, actual-byte manifests and no overwrite of prior bundles. Eleven final-source redacted export bundles were independently verified. All29 exported PDF pages reopened in pypdf and passed footer/character-bound checks in pdfplumber; representative final and earlier pages were rendered in pypdfium2 and visually inspected. No clipping was found. PDF deliberately escapes non-ASCII text; JSON/Markdown preserve UTF-8. SHA-256 establishes changed-byte detection, not origin/authenticity.

## C. Verification by platform

| Platform / exact native environment | Release build | Deterministic suite | Opt-in network | Extracted executable / native UI | Physical input |
|---|---|---|---|---|---|
| win-x64 / Microsoft Windows 10.0.26100; .NET 10.0.13 | Zero warnings/errors | 225 passed /0 failed /8 skipped /233 total |6 passed /0 failed /0 skipped | Six stages passed; genuine diagnostics/export/restart | Not tested |
| linux-x64 / Ubuntu 24.04.5 LTS; .NET 10.0.13 | Zero warnings/errors | 226 passed /0 failed /7 skipped /233 total |6 passed /0 failed /0 skipped | Six stages passed; genuine diagnostics/export/restart | Not tested |
| osx-arm64 / macOS 15.7.9; .NET 10.0.13 | Zero warnings/errors | 225 passed /0 failed /8 skipped /233 total |6 passed /0 failed /0 skipped | Six stages passed; genuine diagnostics/export/restart | Not tested |
| linux-arm64 | Build-only publish passed | Not executed | Not executed | Not executed | Not tested |
| osx-x64 | Build-only publish passed | Not executed | Not executed | Not executed | Not tested |

Linux has seven gated skips; Windows/macOS have eight because the Linux-only desktop launcher check cannot execute there. Six opt-in networking cases are run separately and pass. Optional native sound-request/fallback checks each passed, without claiming audible delivery. Existing monitoring/grace/recovery/import/durability regression tests remain in the suite.

**New Core source-line coverage:856/993 =86.20%.** This is a meaningful instrumented test result, not a claim about all application/UI lines. Exact commands, failures, skips and local/CI distinctions are in [TEST_REPORT.md](TEST_REPORT.md). Locked restore, dependency audit, zero-warning builds, all deterministic tests, native providers, upgrade/restart tests, extracted executables and package integrity gates passed. No vulnerable packages were reported by the NuGet audit at test time.

Candidate package hashes (development CI artifacts only):

| Asset | Bytes | SHA-256 |
|---|---|---|
| Synups-win-x64.zip | 49426819 | `384121cc60cee001fff645bffac93cc3a1b9563817047bf91902b5fd11cd3eee` |
| Synups-linux-x64.tar.gz | 46534551 | `6a8323c80a2e09b3fa4839ad3ed50c12b156280f92f644ea881976eff1d87b5f` |
| Synups-osx-arm64.tar.gz | 46552893 | `184975f778080c27e315fccea4fea9cddb5b1d445d3bf55faf17186abe9a9a84` |

Machine-readable [asset manifest](../evidence/verification/diagnostics/e16e3cd-ASSET_MANIFEST.json), [package checksums](../evidence/verification/diagnostics/e16e3cd-PACKAGE_SHA256SUMS), [native CI summary](../evidence/verification/diagnostics/e16e3cd-ci-summary.json) and [PDF verification](../evidence/verification/diagnostics/e16e3cd-pdf-verification.json) are retained. Raw copied archives/private topology stay under ignored local artifacts. These packages are unsigned (Windows/Linux) or locally ad-hoc signed (macOS), not trusted signed/notarized installers.

## D. Regression and rollback safety

The original GroupMonitor and monitoring probe algorithms were not changed. Diagnostics use separate bounded workers, per-target exclusion, explicit backpressure, finite deadlines and owned cancellation/shutdown. At most two native system resolver tasks are retained even when callers cancel. Monitor recovery still requires an observed successful monitoring probe; diagnostics never rewrite an incident state. Network calls never hold SQLite write transactions.

The existing SQLite owner, lease, capacity limit, serialized off-thread I/O, backups, opt-in retention, configuration/import paths and shutdown handling were reused. Schema2 adds indexed diagnostic evidence and real foreign keys. A version1 archive is backed up and quick-checked before migration; original incidents/comments/settings are preserved. Completed sessions and checkpointed child results are immutable and cannot be reassigned across sessions. Linked incidents are pinned against ordinary monitoring retention. Startup preserves partial checkpoints as Interrupted without invented completion/recovery.

All15 originalv0.1.1 release assets still match their frozen verified hashes. No tag/history rewrite, release asset replacement, main-branch merge, public candidate release or competition submission occurred. Roll back to publicv0.1.1 with a COPY of the automatically verified pre-migration version1 backup and original configuration/preferences in a separate workspace. The old application cannot open schema2; never overwrite the only data copy.

## E. Remaining risks and explicit exclusions

- Real multi-hop/IPv6 path visibility and diverse VPN/privilege configurations are not certified from loopback. Full route tables are not collected.
- DNS uses bounded UDP A/AAAA; truncated answers report Unsupported. No TCP retry, DNSSEC validation, DoH or split-DNS routing inference. Optional PTR names use bounded representative-hop lookup and raw-IP fallback.
- Raw SYN/UDP reachability, payload-safe filtered capture, ASN enrichment, remote corroboration, scheduling, threshold rules, charts, a separate compact window and package-manager submissions are unfinished/unavailable. No dead controls are exposed for them.
- Dynamic tray health indication and configurable/global shortcuts remain incomplete. Physical tray clicks, native folder-picker interaction, screen-reader behavior, OS notifications/login and audible sound are not universally verified.
- Native enumeration/resolution can ignore cancellation. Work remains bounded/owned; a seven-second shutdown target can fail with a visible retry requirement. Per-test checkpoints do not guarantee preservation of samples from a test interrupted before its save.
- Diagnostic history currently has no separate cleanup/deletion UI; it shares the configured SQLite capacity and fails visibly when full. The entire requested persistence/disk-full/power-fault matrix has not been executed for every new path.
- Runtime JSON/reflection, Avalonia/native dependencies and resource handling mean trimming/Native AOT remain unverified. Existing untrimmed self-contained packaging is preserved. Extra RIDs have build evidence only.

## F. Release decision

**CONDITIONAL GO: the tested core diagnostics alpha has passed the native monitoring→incident→diagnose→persist→restart→compare/export checks, with unfinished optional features isolated.** This verdict does not claim the complete31-item scope is production-ready. Keep publicv0.1.1 as the protected competition/release baseline unless the owner separately approves a reviewed candidate and the corresponding disclosures. Publication, package-repository submissions and competition submission still require separate authorization; none was performed.
