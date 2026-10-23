# Diagnostics alpha final hardening

The previous frozen application is `e16e3cd4209ec12bf2150a194373c30efcdf744f`, verified by [run 39869158253](https://github.com/samscript18/Synups/actions/runs/39869158253). Its [original final report](FINAL_REPORT.md) remains historical evidence. This follow-up reviews the actual adapters, SQLite ownership/transactions, desktop handlers and assertions rather than treating every partial feature as absent functionality.

**CONDITIONAL GO for the isolated core diagnostics alpha**, frozen candidate source `3c4462a71cbb90032603cd3eaf4366c2561474d7`. All six jobs in [final run 39896104073](https://github.com/samscript18/Synups/actions/runs/39896104073) passed. This approves the verified core candidate scope, not all 31 broader features, public publication or a competition submission. Native IPv6/physical-network path coverage, physical tray/picker delivery, trusted signing/notarization and the explicitly deferred capabilities remain limitations.

Application changes were introduced in `15f4a05`; `40e2bf0` corrected only engineering fixture return routing; `3c4462a` corrected the notes embedded in candidate archives. The final source has the same application code as `15f4a05`. The docs/evidence commit after the freeze does not change shipped application or packaging inputs.

## Independent review of the 19 partial features

“Verification” means implemented behavior needing a broader native/physical acceptance case. “Functionality” means code is absent or intentionally incomplete. Neither category is silently counted as a complete feature.

| Feature | Missing functionality in frozen alpha | Missing verification in frozen alpha / hardening action |
|---|---|---|
| F01 Traceroute | No new provider required for bounded ICMP trace; raw SYN/UDP intentionally unavailable | Only native loopback; add actual two-router, three-hop isolated Linux kernel path. Public/physical paths and native IPv6 remain unverified. |
| F02 Quality | No missing core math; null RTTs and interrupted windows already explicit | Real loss and loopback already passed; add concurrent monitoring observation. Physical-link representativeness remains a limitation. |
| F03 DNS | TCP fallback absent | Add one same-resolver framed TCP retry after validated TC, retaining total deadline and probe cap; test fragmentation, invalid frames, truncation, refusal, timeout and cancellation. Split-DNS/DoH/DNSSEC remain outside scope. |
| F04 TCP | Single-address selection; no address racing/fallback | Loopback connect/refusal already native-tested; exhaustive external timeout/firewall behavior remains unverified. No application-layer health claim. |
| F05 Configuration | Full routing table omitted | Default-route/provider fields tested natively; VPN/split-route field completeness remains unverified. |
| F06 Protocol awareness | Raw SYN and UDP adapters unavailable | Actual ICMP and TCP-connect remain distinct. No permission bypass or substitute measurements. |
| F07 Rate-limit heuristics | Proof of rate limiting is unavailable by design | Deterministic interpretations and controlled protocol contrast exist; cannot infer remote fault ownership. |
| F08 Reverse DNS | Representative-hop PTR only; bounded cache, no provider expansion | Cache/concurrency/timeout cases covered; physical PTR interaction not certified. PTR remains UDP with raw-IP fallback. |
| F09 Incident links | Cannot attach completed standalone evidence to another incident | Creation-time foreign key, retention pinning and genuine incident restart already passed; add safe diagnostic deletion without deleting the incident. |
| F10 Incident snapshot | Core creation-time workflow complete | Controlled Linux incident already verified; physical input and broader failure environments remain unverified. |
| F11 Evidence | PDF shows visible Unicode escapes rather than shaped native glyphs | Retain explicit portability limitation. Audit large fixtures, actual byte hashes, cancellation cleanup, redaction and rendered page bounds. JSON/Markdown retain Unicode text. |
| F12 Baselines | No deletion; no history paging/management | Add confirmation, FK-safe baseline/comparison deletion, pinned-session refusal, tie-safe session paging and retention tests. Baseline selector currently shows latest 100. |
| F17 Native providers | Full OS-specific routing/provider coverage omitted | Existing bounded read-only OS route commands retained. More network configurations require hardware/platform evidence. |
| F20 Platforms/AOT | Trimming/AOT not enabled | Extra architectures remain build-only; running on matching hardware is still required. |
| F24 Tray/shortcuts | Dynamic health tray and configurable/global shortcuts absent | Existing navigation shortcuts/tray callbacks implemented; physical tray delivery remains unverified. |
| F25 Profiles | Advanced unavailable protocols omitted intentionally | Existing built-in/custom profile persistence tested; extend actual button coverage. Corrupt file remains preserved. |
| F29 Metadata | Public/package-manager submission absent intentionally | Source-specific portable manifests/hash verification rerun; no publication. |
| F30 Tests | Fault coverage incomplete | Add real SQLite busy/full rollback, repeated reopens/process restarts, large/interrupted exports and real multihop. Sudden physical power loss and all OS failure permutations remain unverified. |
| F31 CI | No missing three-OS native pipeline | Rerun full suites, opt-in probes, extracted application stages and native desktop checks for the exact hardening source. Physical desktop input remains separate. |

## Changes and local evidence

DNS retries only a validated truncated UDP response, once, to the same approved endpoint, over TCP. A shared cancellation deadline covers both transports; declared TCP bodies must be 12–4096 bytes. Exact stream reads handle fragmented framing and early EOF. If the approved session probe cap has no spare probe, truncation remains explicitly Unsupported. Reverse PTR behavior is unchanged. Previously saved cap-one DNS requests remain readable.

History loads in pages of 50 using UTC plus ID as a stable cursor, including equal timestamps. Terminal session deletion removes normalized results/observations through existing foreign keys; active checkpoints and baseline/comparison references block it. Baseline deletion atomically removes its saved comparisons and preserves source sessions. Dialogs snapshot the exact ID and describe irreversible effects. Monitoring incidents and existing export folders survive deletion. No schema bump or destructive migration is needed.

Existing opt-in retention also removes old terminal standalone diagnostics with known completion times. Incident links, baseline/comparison references and unfinished checkpoints are protected. Retention reuses pages rather than attempting VACUUM; this is not secure erasure. Keep-all remains the default. Retention confirmation now names diagnostic deletion explicitly.

Completed test checkpoints now appear during a running session, so users can see partial results before cancelling the next test. Comparison presentation is applied only after its SQLite write succeeds and the viewed session/baseline still match. Export cancellation is checked before expensive rendering and between PDF pages; limits are checked before rendering subsequent formats.

Local Release suite: **243 passed, 0 failed, 8 gated skips / 251 total**. Python archive/namespace safety tests: **9 passed**. New DNS subset: 32 passed, 1 opt-in skip. Management/partial-state tests use actual Avalonia controls and modal handlers; injected results are explicitly test fixtures, never production measurements. An initial test compile error from calling a Desktop-internal method was corrected to assert public persisted history instead; no production visibility was widened.

Actual local loopback observation: 12 monitoring polls in 3.0016 seconds alone; 12 in 3.0067 seconds alongside two diagnostic sessions sharing SQLite; sampled maximum one group worker, no unsaved evidence and no incidents. This is a short liveness/sample-rate observation, not a throughput benchmark. Three separate diagnostic reload processes passed. SQLite busy returns code 5 without a terminal checkpoint; retry succeeds. Real bounded SQLite full returns code 13, rolls back child rows and retains the unfinished checkpoint over five reopens. Existing extracted-apphost process-kill/read-only/restart gates remain mandatory in CI.

All 15 public v0.1.1 protected assets were reverified unchanged. No release, package submission, competition form, raw SYN, UDP reachability, packet capture or external integration is activated.

## Diagnostics control and state audit

| Controls | Advertised action checked | Evidence / remaining boundary |
|---|---|---|
| Target, port, samples, interval, timeout, deadline, probe cap, hop limit and probes/hop | Two-way draft bindings feed the validated request; unsafe values prevent execution | Source/compiled binding review; draft-validation tests and exact Run-modal snapshot test |
| Profile selector, custom label, Save custom profile | Populate tests/parameters, atomically save a bounded local profile; never probe on selection/save | Profile restart/corruption tests and actual Save button test |
| TCP/DNS/ping/trace/config checkboxes, A/AAAA, resolver, PTR opt-in/timeout | Select the advertised protocol/parameters and disclose approved resolver/PTR traffic | Source binding/request review; consent/bounds and independent adapter tests; unavailable raw protocols have no controls |
| Clear incident link / incident Diagnose | Clear or snapshot the actual selected incident ID; require a new approved target | Handler/source audit and actual-incident UI test; genuine controlled Linux incident workflow |
| Review and run / modal Continue or Cancel | Execute only the exact confirmed request, or send nothing | Actual button/modal tests and all three native extracted-apphost Run exercises |
| Cancel diagnostics | Independent cancellation while monitoring controls remain usable; retain returned samples/checkpoints | Actual Cancel button, runner ownership and partial-result tests |
| Refresh / Load older / saved-session selector / Open | Read local history without probes; stable 50-row paging and explicit loading/empty/error status | Actual button tests with 103 tied timestamps; durable SQLite reopen tests |
| Delete selected session | Confirm exact selected ID, protect active/referenced records, preserve incidents/exports | Actual Cancel/Continue and changed-selection snapshot tests; normalized FK/cascade checks |
| Baseline label / selector / Save / Compare / Delete | Save successful evidence; compare compatible facts; delete baseline plus comparisons after confirmation, preserving sources | Actual buttons/modal tests and SQLite protection tests; selector currently limited to latest 100 baselines |
| Redact / Preview / read-only preview text | Regenerate a detached preview; invalidate preview when redaction changes | UI preview/redaction tests; large fixture sentinel check across JSON/Markdown/PDF |
| Export / confirmation / native folder picker | Confirm, choose local parent, create unique JSON/Markdown/PDF/hash bundle | Actual Export button with cancelled confirmation; three native programmatic folder/export exercises. Physical OS picker selection is **not certified**. |
| Phase, elapsed, facts/results, history status and error text | Show running/loading/empty/error/cancelled/partial evidence without inventing values | Partial saved result visible before next test finishes; empty-history, unsafe input, corrupt-profile and cancellation tests; source-reviewed loading/finally guards |

The panel's compiled bindings and handlers were audited for advertised behavior. This does not certify every control through physical mouse/keyboard/accessibility delivery on every desktop. Existing tray callbacks and Ctrl/Cmd+6 remain implemented; physical tray, notification, login/startup and native folder-picker delivery remain acceptance limitations. Test fixtures are labelled and confined to tests/engineering evidence, never surfaced as production measurements.

## First native run and correction

[Run 39879173890](https://github.com/samscript18/Synups/actions/runs/39879173890), source `15f4a05`, passed Windows/macOS, coverage and extra builds. Linux passed its suite and extracted executable, but the new path fixture rejected missing TTL-2 observations even though the destination answered at TTL 3. The original trace is retained in evidence. The fixture lacked a source route to the second router's reply subnet. `40e2bf0` adds that route exclusively inside the isolated namespace; it does not change production traceroute, weaken responder assertions or disable kernel source validation. Subsequent Linux native verification passed the actual three-hop path. Intermediate missing responses were preserved honestly, with no inferred forwarding failure.


## Final native release gates

Frozen candidate: `3c4462a71cbb90032603cd3eaf4366c2561474d7`, [CI run 39896104073](https://github.com/samscript18/Synups/actions/runs/39896104073). Independent checks verify the GitHub artifact download digests, ZIP integrity/path safety, inner archive SHA-256, every packaged manifest file, embedded source/RID/version, corrected release-note content (Windows CRLF normalized for text comparison only), and actual extracted-apphost source identity. Byte hashing always uses original bytes.

| Native platform | Deterministic/headless suite | Separately enabled network checks | Extracted executable |
|---|---|---|---|
| Windows x64, Windows build 26100 | 243 passed, 0 failed, 8 gated skips / 251 | 6/6 passed | All six stages passed |
| Linux x64, Ubuntu 24.04.5 | 244 passed, 0 failed, 7 gated skips / 251 | 6/6 passed | All six stages passed |
| macOS arm64, macOS 15.7.9 | 243 passed, 0 failed, 8 gated skips / 251 | 6/6 passed | All six stages passed |

The six executable stages are exercise, restart, externally terminated interruption, recovery, real read-only storage failure and repaired restart. The externally killed process is expected to terminate nonzero; the launcher verifies its readiness and later recovery rather than declaring that exit a successful normal shutdown. Each native platform also passed its separate audio request/safe fallback check; audible playback is not certified. Nine Python archive/namespace tests passed. Coverage: **911/1049 newly added diagnostics Core lines, 86.84%**. Linux-arm64 and macOS-x64 publish jobs passed as build-only; neither runtime is certified. Runtime for native candidates: .NET 10.0.13; SDK 10.0.402. The 86.84% figure is the Linux instrumented coverage job for `Synups.Core/Diagnostics/*.cs` plus `Persistence/DiagnosticStore.cs`; it is not a Windows/macOS coverage union and does not claim coverage of every modified shared persistence or desktop line.

The real isolated IPv4 path returned `10.77.1.2` at TTL 1, `10.77.2.2` at TTL 2 and destination `10.77.3.2` at TTL 3. The actual trace completed in 120.9421 ms. This is kernel-produced ICMP evidence through two separate Linux router namespaces, not injected replies, a public network or native IPv6 certification. Namespace guards reject the host network and require loopback-only/no-default-route initial state before any mutation; child namespaces are independently verified and disposed. OS source-validation policy stays enabled. Linux also passed genuine ICMP disruption → incident Diagnose → typed partial evidence → real recovery → restart/export, preserving the original incident.

| Actual monitoring observation | Alone: polls / seconds | With two diagnostic sessions: polls / seconds | Evidence |
|---|---|---|---|
| win-x64 | 12 / 3.0084 | 12 / 3.0094 | sampled maximum 1 group worker; no unsaved evidence or incidents |
| linux-x64 | 12 / 2.9997 | 13 / 3.0054 | sampled maximum 1 group worker; no unsaved evidence or incidents |
| osx-arm64 | 10 / 3.1459 | 12 / 3.0125 | sampled maximum 1 group worker; no unsaved evidence or incidents |

These short loopback observations verify progress and worker/persistence bounds while diagnostics execute; they do not establish a general performance percentile or physical-network throughput. Three separate reload processes passed on each native platform.

A final local test copied genuine closed v0.1.1 incident history, preserved the existing incident and comment payloads, created the verified schema-1 backup, completed two real diagnostic sessions, baseline/comparison/export, and passed three separate reload processes. Before/after hashes confirm the original database/configuration/preferences remain unchanged. The working tree contained documentation changes only; application source matched the frozen candidate.

## Final evidence export audit

Independent verification checked all **17 native-job export bundles**: 11 redacted real diagnostic exports and six explicitly labelled large test-fixture exports (redacted/unredacted pairs on each OS). Every JSON, Markdown and PDF file matched the original-byte SHA-256 and byte count in its manifest. The fixtures use artificial test text only; they are not production measurements.

All **687 PDF pages** passed parsing, page-footer and character-bounds checks: 27 pages from real diagnostics and 660 from large fixtures. Representative first/middle/final large-fixture pages and one real-export page per OS were rendered and visually inspected; no clipping was found. JSON/Markdown preserve Unicode text; portable PDF deliberately displays Unicode escapes, including surrogate pairs for supplementary characters. This is an explicit rendering limitation, not native glyph coverage. Sentinel tests confirm sensitive fixture text is absent from all redacted artifacts. Cancellation after temporary-directory creation removes the incomplete export; existing bundles remain unchanged. Process-killed export staging/physical power-loss cleanup is not certified as secure erasure or automatic orphan cleanup.

The audit is recorded in [3c4462a-pdf-audit.json](../evidence/verification/diagnostics-hardening/3c4462a-pdf-audit.json); rendered contact sheets are retained alongside it. PDF checks complement, rather than replace, actual-byte manifests and explicit redaction review.

## Exact final package identity

| Package | Bytes | SHA-256 |
|---|---:|---|
| Synups-win-x64.zip | 49,432,018 | `13d62339917301a0e1e79d9aaf47401c986d34ca4fb913afdf0819ce32c639b9` |
| Synups-linux-x64.tar.gz | 46,537,675 | `f5f290c3f1e404dc1e594fc6d198471bc89ffed7e5a1d731046d19acdf10520e` |
| Synups-osx-arm64.tar.gz | 46,548,195 | `541f8e08cd79a1ab5632e8d9280e81b4eb759a0ee1bbac0bac2fb82998a2d926` |

These are **inner portable package hashes**, not the surrounding GitHub Actions artifact ZIP hashes. Every archive identifies source `3c4462a71cbb90032603cd3eaf4366c2561474d7`, version `0.2.0-alpha.1`. Windows/Linux have no publisher signature; macOS is ad-hoc signed for bundle integrity only, without Developer ID or notarization. No package is represented as a trusted signed installer.

Machine-readable source-specific results, exact package hashes, actual trace, native screenshots, upgrade preservation and PDF audit are in [diagnostics-hardening evidence](../evidence/verification/diagnostics-hardening/3c4462a-ci-summary.json) and [PACKAGE_SHA256SUMS](../evidence/verification/diagnostics-hardening/PACKAGE_SHA256SUMS). The earlier fixture failure is retained alongside successful evidence.

## Release decision and remaining limits

**CONDITIONAL GO for isolated evaluation of the implemented core diagnostics alpha.** The strengthened DNS, history/baseline management, FK-safe retention, partial-results UI, persistence faults, large exports and native package workflows have direct evidence. The 31-feature matrix remains conservative: **4 VERIFIED documentation features, 19 PARTIALLY_VERIFIED, 1 BLOCKED, 7 DEFERRED**. Partial status distinguishes missing functionality from broader platform/hardware verification; it does not mean the implemented tested core is absent.

Conditions: use an isolated data directory or a copy of existing history with the verified migration backup retained; respect displayed ICMP/IPv6/OS permission limits; review exports before sharing; accept PDF Unicode escapes, latest-100 baseline selection and explicit build-only/physical-input limits. Full routing-table inspection, attaching completed standalone evidence to a different incident, dynamic tray health/configurable global shortcuts, native IPv6/physical-path acceptance, physical native picker/tray delivery and physical power-loss fault permutations remain unverified or incomplete. Hashes provide byte integrity, not authorship.

Raw SYN, UDP reachability, capture, ASN/remote integrations, scheduling, new threshold alerts/charts/compact window and package-manager distribution remain unavailable. No public release, competition form or external integration was submitted. Public v0.1.1 and all 15 protected assets remain unchanged. Publication requires a separate user instruction and a concrete reviewed payload; this decision itself authorizes no publication.
