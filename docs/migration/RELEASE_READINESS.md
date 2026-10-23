# Release readiness — Phase 5

**Current public release:** [v0.1.1](https://github.com/samscript18/Synups/releases/tag/v0.1.1), source6c812fde9a29eeb8cfd6399a2f1a75e333e81e03, internal0.1.1-rc.4,91.7% inventory. [Anonymous publication verification](../evidence/verification/synups-0.1.1-publication.json). Earlier unpublished/proposed-release statements below are historical. Remaining signing/physical/login/alert blockers are unchanged; competition submission is separate.

Historical 22 October 2026 prepublication checkpoint: [reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md), application6c812fde9a29eeb8cfd6399a2f1a75e333e81e03,91.7% inventory/8.5 readiness. Earlier phase sections are historical. The user-selected proposed v0.1.1 payload is now frozen rc.4 at91.7%, with internal0.1.1-rc.4 version disclosed; public0.1.0 remains unchanged. Earlier rc.3 payload decisions below are historical.

## Historical readiness assessments

The following assessments retain their original source/date. Proposed/unpublished wording below is historical; current public v0.1.1 is identified above.

Assessment updated for user-authorized **[Synups 0.1.0 publication](https://github.com/samscript18/Synups/releases/tag/v0.1.0)**. **8.5/10 submission readiness; not production-ready and not 10/10.** All 15 public assets were downloaded anonymously and hash-checked. Mandatory signing/ordinary-desktop acceptance gates remain open. Competition entry has not been submitted. Historical assessments remain in Git and MIGRATION_LOG.md.

Frozen release application source: **2973d3349fcb5af5639597920842288e6549bbd0**, `feat/avalonia-migration`, [successful three-OS run 39682197118](https://github.com/samscript18/Synups/actions/runs/39682197118). [Exact Phase 5 downloaded evidence](../evidence/verification/2026-10-21-release-candidate.json), [platform limits](CROSS_PLATFORM.md#phase-5-frozen-release-candidate--2973d33), [launch instructions](../release/INSTALLATION.md) and [candidate notes](../release/RELEASE_NOTES.md).

## Verified gates

- [x] Original MIT bytes/attribution, baseline provenance and Git ancestry preserved; original WPF source/build reference retained.
- [x] Locked restore/audit; Synups Release zero warnings/errors on all three native runners. Original WPF workflow separately retains its visible .NET 6 end-of-support warnings.
- [x] Per OS: 105 deterministic/headless cases, separate opt-in real-loopback case, four Python archive/licence safety cases. Default suite has one intentional network skip; all 106 unique .NET cases exercised across two invocations.
- [x] Real extracted self-contained apphosts run on Windows Server 2025 x64, macOS 15.7.9 arm64 and Ubuntu 24.04.5 x64/X11/Xvfb, not a source-recompiled verifier.
- [x] Native window handles and live real loopback, CRUD/validation, saved IDs/configuration, preferences, stop/drain, restart, second-process restoration and exit confirmation checked through actual controls.
- [x] Five normal stage exits 0; isolated forced termination recorded separately (Windows 1, Unix −9). Five durable sessions, exactly one interrupted; no invented recovery/outage.
- [x] Native SQLite loading/reload, real read-only database error preserving bytes, repair/restart, comments/CSV/backup/capacity regressions pass. Native save-picker remains manual.
- [x] All three complete artifacts/logs downloaded. API ZIP digest, inner checksums, every file hash, apphost/native SQLite architecture, exact licence, notices and Unix permissions independently inspected.
- [x] Own Synups icons and identity; Windows/Linux portable archives, Mac app bundle, complete retained dependency/runtime/native/font notices and checksums prepared. No new runtime/NuGet dependency.
- [x] Mac managed-resource layout fixes reproduced signature loss. Strict/deep validation passes **after archive extraction** on CI macOS 15 and local macOS 26.3.1.
- [x] Final downloaded Mac bundle launched through LaunchServices; real monitoring, restoration and clean close passed. This download was through the API, not an ordinary quarantining browser.
- [x] Two five-minute real GUI processes at preceding source `3cf2720` passed resource observation/restart. CPU/RSS/descriptors/DB samples retained; no multi-hour GUI or leak-free claim.
- [x] Actual published-window captures from all three OSes inspected; source/hashes/limits retained. Static token contrasts 8.26–17.02:1 for the six audited pairs; visible programmatic focus captured. This is not complete accessibility certification.
- [x] Honest README/migration write-up, candidate release notes, per-OS launch instructions and [manual acceptance protocol](../release/DESKTOP_VALIDATION.md) prepared.
- [x] Requested public landing update applied: default branch is `feat/avalonia-migration`; API confirms its README begins with Synups. Original master/history remain intact.

The separate **two-hour Core-engine test passed**: 7,201.54s total including restart, 28,792 real loopback probes, no failures/errors, four workers while running and zero after drain, 12.4ms stop, two clean durable sessions/reopen. Mean CPU1.07% of one core; RSS70.55→55.06MiB (peak85.48MiB); descriptors101→104 (peak105); database40,960bytes with no sampled growth. [Full summary/source qualification](../evidence/verification/2026-10-21-engine-endurance-macos-arm64.json) and [720 samples](../evidence/verification/2026-10-21-engine-endurance-samples.jsonl) are retained. This working-tree Core-console build is not a published apphost or multi-hour GUI certification.

## Delivery and remaining gates

- [ ] **Trusted macOS delivery:** Gatekeeper rejects the actual final bundle (assessment exit 3). Ad-hoc integrity is not Developer ID/notarization. Obtain a legitimate signing identity/notarization workflow or agree and verify a candid distribution policy; never disable OS security to manufacture a pass.
- [ ] **Ordinary downloaded desktop acceptance:** browser download/quarantine, consumer Windows desktop, real Linux desktops/Wayland, physical tray Show/Exit, native save-picker, topmost/z-order, keyboard traversal and screen-reader/high-contrast/high-DPI/disconnected-screen behavior. Programmatic controls, a Server runner and Xvfb cover narrower behavior.
- [ ] **OS notification request/delivery/denial:** macOS identity/permission and Linux daemon/host combinations. Windows currently has a documented in-app fallback; native Windows delivery and sounds are absent.
- [ ] **Broader reliability:** representative hours/days GUI and large-history workloads, real pending-native-ICMP cancellation, IPv6/VPN/firewall/no-network/suspend/resume, physical disk-full and power-loss experiments. Healthy loopback, file read-only tests and process kill are different evidence.
- [x] **Public delivery:** explicitly authorized [https://github.com/samscript18/Synups/releases/tag/v0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0) published at `2026-10-21T22:34:54Z`, exact tag/application commit `2973d3349fcb5af5639597920842288e6549bbd0`. All 15 assets anonymously downloaded, HTTP 200, SHA-256 and archive integrity passed. Stable Release links replace CI-only downloads. Installers/trusted signing remain absent.
- [ ] **Final competition submission:** public persistent runnable builds, final source/material review and separately authorized entry. Recorded deadline: October 23, 2026, 23:59 UTC.

No unresolved automated failure is hidden: Windows licence CRLF, post-archive Mac signatures, focus-test foreground assumptions and macOS 15 minimize/restoration failures and corrections remain in the migration log. The final exact application source passes all three jobs. Successful testing does not erase the open delivery gates.

## Transparent score

Ten equally weighted milestone checks, with half credit only for explicitly incomplete work. This is an assessment rubric, not a measured probability or a production-support claim.

| Check | Credit | Evidence / remaining work |
|---|---:|---|
| Provenance, public source and MIT | 1 | Preserved ancestry/licence and recorded baseline |
| Real monitoring and configuration | 1 | Actual apphost CRUD/ICMP and characterized semantics |
| Durable evidence and error ownership | 1 | Restart/interruption/read-only/repair and regression suite |
| Three native-OS published apphosts | 1 | Exact-source six-stage CI per RID |
| Reproducible builds and regression CI | 1 | Locked restore, zero-warning Release, retained tests/logs |
| Package integrity, identity and notices | 1 | Independent downloaded audit and extracted signature validation |
| Honest write-up and before/after evidence | 1 | Attributed original illustration, genuine apphost captures, detailed records |
| Resource and accessibility acceptance | 0.5 | Measured scoped observations/focus/contrast; broader GUI/accessibility pending |
| Reviewed public landing and release handoff | 1 | Public Release/assets, anonymous downloads/hashes and README links verified |
| Trusted ordinary-user distribution | 0 | Gatekeeper rejection and manual installed-desktop checks remain |
| **Total** | **8.5/10** | **Trust/desktop acceptance and separate submission authorization remain open** |

Legacy inventory parity remains **72.2% = (9 + 8×0.5)/18**. It is separate from this release rubric. Discovery, validated real-WirePeep settings import, login/sound/taskbar/focus and menu/clipboard/help details remain missing; see [FEATURE_PARITY.md](FEATURE_PARITY.md).

## Data and scale limits

SQLite commits are the evidence source of truth. Pending batches are memory-owned and do not survive force quit/power loss; keep the app open, fix storage and Retry. Corruption never triggers silent reset. The main-DB page limit defaults to 1 GiB (Settings 256 MiB–10 GiB); WAL/SHM/journal, backups, logs and temporary work need additional disk space. A page cap is not a total disk reservation.

Retention defaults to preserve-all; positive retention and manual cleanup require confirmation. Unresolved/interrupted sessions are retained. Manual cleanup creates a verified full backup before deletion; backup failure cancels deletion. Freed pages are reused without shrinking the DB. Automatic log copies have a separate 256 MiB cap and can lag until Stop/Close; a failed copy is visible and must be repaired or explicitly disabled.

Large-history loading/export/retention still materializes records in memory. No full-capacity bounded-memory/endurance certification is made. Offline restoration and real corruption/permission recovery need manual review; never move an open DB or discard a recovery journal. Use the in-app full SQLite backup, not outage CSV, for history preservation.

## Source-control handoff

Draft [PR #1](https://github.com/samscript18/Synups/pull/1) prepares a history-preserving migration review against original master. Source checkpoint commits/pushes are authorized; no force-push/history rewrite/Release/submission occurred. Repository metadata now identifies Synups. The requested default-branch change was applied and verified through the API on 2026-10-21: `feat/avalonia-migration`, README `# Synups`. Original master remains intact; this is a reversible presentation change, not a PR merge or Release.


## Phase 5 release preparation

All three **2973d3349fcb5af5639597920842288e6549bbd0** CI artifact containers and inner archives are independently downloaded/audited; the earlier 292b847 evidence remains historical. Production source is unchanged between those revisions, but source metadata/bundled documents differ and archive hashes are different. [The candidate index](../release/VERIFIED_ARTIFACTS.md) now pins the exact 2973d33 packages. Later documentation/tool commits do not replace the frozen application bytes.

Local repeated gates: locked restore/audit passed; Release zero warnings/errors;105 deterministic/headless passed with1 intentional network skip; separate real-loopback case passed;4 Python archive/licence checks passed. The exact downloaded Mac bundle passed all six real apphost stages and LaunchServices exercise again with normal exit0. Fresh strict extracted integrity passes; Gatekeeper still rejects (exit3). A read-only keychain query confirms0 valid code-signing identities, so legitimate Developer ID/notarization is blocked by unavailable credentials. Existing security settings remain unchanged. Apple’s user-controlled per-app guidance is linked in INSTALLATION.md; no approval/quarantine acceptance is claimed.

Accessibility automation is disabled on the local Mac. No permission was enabled, and physical keyboard/tray/native save-dialog or OS notification delivery was not certified. Three actual compact client renders were inspected; scrolling preserves below-viewport content and no release-blocking clipping defect was observed. No optional feature, monitoring-engine or UI redesign was introduced.

[Publication handoff](../release/PUBLICATION.md), [prepared release text](../release/RELEASE_BODY.md) and [submission fields](../release/SUBMISSION.md) are concrete and reviewable. The assembly tool validates reviewed input hashes/source/runtime stages, preserves archive bytes, excludes temporary databases/settings and prepares consolidated checksums plus retained CI logs/TRX/captures. Successful assembly/checksum verification and rejection of failed-workflow/existing-output inputs were exercised. No permanent public download, Release/tag or form submission is created. Planned download URLs must not be submitted before separately authorized publication and anonymous verification.

**Readiness stays8/10 under the same rubric**, rather than earning extra credit for preparing documents. Permanent public downloads, trusted/accepted ordinary Mac delivery and remaining desktop/accessibility acceptance are still real blockers. Conservative legacy parity stays 72.2%=(9+8×0.5)/18.


## Authorized public release verification

The later user explicitly authorized v0.1.0 publication and upload of the existing reviewed assets. This supersedes historical “not published/awaiting authorization” statements in the preparation section above; competition submission remains separately prohibited. Tag source 2973d3349fcb5af5639597920842288e6549bbd0 and all reviewed asset bytes are unchanged. [Publication record](../evidence/verification/synups-0.1.0-publication.json) retains UTC timestamps, public URLs, HTTP status and every SHA-256. The score adds only the remaining half-point for completed public handoff; trusted ordinary-user distribution remains 0, resource/accessibility remains 0.5, and no runtime/support claim is expanded. Windows remains unsigned; macOS remains ad-hoc/not notarized with Gatekeeper rejection.


## Competitive improvement checkpoint — 22 October 2026

- [x] Separate unpublished 0.1.1-rc.1; public 0.1.0 asset hashes unchanged.
- [x] Guided first-target/group save, no automatic probe or profile overwrite; two new UI regressions pass.
- [x] Three-OS source-specific run 39754244507: 107 deterministic/headless + 1 real-loopback + 8 Python cases per OS, zero-warning Release and six actual extracted-apphost stages.
- [x] Isolated Linux real ICMP loss → grace → confirmed incident → recovery → separate-process SQLite/comment/event reload; raw CSV and genuine client captures retained.
- [x] Native macOS normal launch/owned window/OS screenshot/native quit observed on stable executable; exact candidate also passed programmatic packaged checks locally.
- [x] Main README foregrounds original architecture, preserved/rewrite/improvement comparison, exact stable/candidate evidence and limitations. Judge’s guide prepared.
- [ ] Full mandatory macOS physical tray, keyboard, native save picker, minimize/restore, resize and active-close acceptance: Accessibility is disabled; no permission changed and no physical pass claimed.
- [ ] Trusted Mac/Windows delivery, native notification delivery and broader desktop/accessibility/endurance acceptance remain open.

Readiness stays **8.5/10 under the existing rubric**: stronger evidence and onboarding do not resolve trusted ordinary-user distribution or the outstanding human acceptance gates. Legacy parity remains 72.2%; no optional feature-count credit is added. A separate release candidate is justified by the first-run improvement, but publication is not authorized by this task. See [exact evidence and scope](CROSS_PLATFORM.md#competitive-candidate-verified-incident-and-onboarding--22-october-2026).


Final candidate `65d917ac645139836cf97e48c48c137a09825942` passed [run 39761184906](https://github.com/samscript18/Synups/actions/runs/39761184906) on all three native runners, including viewport assertions and six extracted-apphost stages. Downloaded image review confirms the actual Windows fresh-window clipping fix; failed 375e2f7 evidence is retained. Final namespace incident/restart/CSV repeats successfully. Local exact downloaded Mac six-stage rerun passes; strict integrity passes, Gatekeeper rejects (3). [Frozen candidate package hashes](../release/COMPETITIVE_CANDIDATE.md), [final machine-readable evidence](../evidence/verification/2026-10-22-competitive-candidate-final.json). The candidate is prepared locally, not released; human Mac interactions/trusted signing remain blocked. Readiness and parity stay 8.5/10 and 72.2%.


## Final competitive hardening checkpoint

Reviewed unpublished [0.1.1-rc.2](../release/FINAL_HARDENING_CANDIDATE.md) now passes native three-OS packaged gates,126 deterministic/headless +2 opt-in cases per OS,8 Python cases, CodeQL and isolated Release coverage. Partial explicit gateway discovery, bounded incident timeline/startup cache/streamed CSV, clipboard/help/shortcuts and opt-in confirmed-event focus are implemented. Stable0.1.0 and rc.1 archive sets are unchanged. Inventory75.0%, unchanged18-item rubric.

- [x] Exact-source native route queries and no-mutation/no-probe preview on all three CI systems.
- [x] Bounded timeline queries, older comments, interrupted incident preservation, complete CSV and malformed-page errors tested.
- [x] Native Linux real ICMP outage/recovery, persisted timeline/comment/export and focus/clipboard independently verified.
- [x] Separate local exact Mac downloaded-apphost repeat and fresh normal LaunchServices/window/OS capture/native quit.
- [x] Per-file/archive integrity, licences/notices/native SQLite/CPU/Unix permissions reviewed; separate rc.2 locally prepared.
- [x] Retained CodeQL SARIF, Cobertura/TRX, actual process/action reports, images and CI logs.
- [ ] Physical Mac tray Show/Exit, native Save-panel completion, keyboard traversal/focus/Escape, resize/minimize and close with pending writes: Accessibility/event-post denied; [exact manual checklist](../release/MACOS_ACCEPTANCE.md).
- [ ] Trusted macOS Developer ID/notarization; candidate Gatekeeper still rejects3. Windows remains unsigned.
- [ ] Broad physical desktop/notification/accessibility/VPN/IPv6/suspend and new-source long-running GUI acceptance.
- [ ] Review/explicit authorization for a separate candidate Release and separate competition submission. Actions downloads are not permanent public candidate links.

Readiness remains **8.5/10 under the existing rubric**. More functionality/tests do not resolve distribution trust or physical acceptance, so neither10/10 nor Phase-wide completion is claimed. The new candidate is functionally stronger with source-specific regressions, while rc.1 remains the smaller rollback baseline. Full-archive logging/retention still warrants large-history testing; the bounded2k/20k synthetic experiment is not endurance certification. No production mock measurements or historical latency/uptime invention exists.

## Current rc.3 checkpoint — 22 October 2026

**8.5/10 readiness remains unchanged; functional inventory rises to88.9%.** These are different assessments. Exact unpublished application 7ca85cccce6e3609d08d64e4f23172fa14fffb25 passes [three native jobs/coverage](https://github.com/samscript18/Synups/actions/runs/39821153798), [candidate/hash review](../release/LEGACY_COMPLETION_CANDIDATE.md).

- [x] Locked restore/security audit, zero-warning Release on all three native runners.
- [x] Windows/Mac 143 default .NET passes, Linux 144; each 3 separate real/native plus 8 Python cases. Intentional opt-in/platform skips documented.
- [x] Actual extracted executables: initial minimized state, restoration/second instance, clipboard/status keys, loopback, SQLite restart/interruption/read-only repair, normal clean exits.
- [x] Exact original candidate rules, bounded cancellation, confirmation/duplicates/ID preservation and guided defaults tested; no production simulator or automatic discovery.
- [x] Closed rc.2 real session/incident upgrade preserves every logical record/comment/ID/UTC and configuration/preferences bytes; no false recovery.
- [x] Exact CI archives, manifests, licences/notices/runtime/permissions/hashes independently audited and separately frozen as rc.3. Rollback sets unchanged.
- [x] CodeQL retained zero SARIF results; scoped coverage 80.81% lines/67.34% branches.
- [ ] Physical tray/keyboard/native CSV Save-panel/multiple-display acceptance: macOS Accessibility/event posting denied. Normal launch/window/OS quit and programmatic controls are separate evidence.
- [ ] Trusted Mac distribution: integrity valid but Gatekeeper exit 3; no Developer ID/notarization. Windows unsigned.
- [ ] Actual next-login/Windows startup-registry execution and OS notification delivery/sounds/taskbar certification. Explicit registration implementation alone is not completion.
- [ ] Genuine WirePeep settings fixture/import, wider IPv6/VPN/suspend/Wayland/desktop acceptance and representative long-running GUI workloads.
- [ ] Separate reviewed candidate Release authorization/public permanent download verification, then separately authorized competition submission. No new Release/submission performed.

Prior passed results remain source-qualified historical evidence. Do not callrc.3 fully production-ready or replace stable0.1.0/rc.1/rc.2 archives. Optional parity work must not delay a reliable submission.


## Desktop completion / presentation checkpoint —22 October2026

Exact application **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, version0.1.1-rc.4. [Native Windows/macOS/Linux verification39850198899](https://github.com/samscript18/Synups/actions/runs/39850198899), [exact candidate/hashes/failures](../release/DESKTOP_COMPLETION_CANDIDATE.md), [retained independent evidence](../evidence/verification/2026-10-22-desktop-completion.json). This supersedes previous current-state limitations only for this separately frozen candidate. Public0.1.0 and rc.1/rc.2/rc.3 bytes are unchanged; the proposed v0.1.1 release still uses rc.3 at7ca85cccce6e3609d08d64e4f23172fa14fffb25, with its internal rc.3 version disclosed.

Genuine original WirePeep now builds/runs on Windows: untouched upstream61ae143, .NET Framework4.8, original Menees5.1.2, populated native PrintWindow capture, clean WM_CLOSE exit0. The disposable runner blocks outbound traffic only for the original executable, so failed target rows are controlled results, not ISP outages. A real emitted .stgx fixture supports validated profile-only import with preview, explicit replacement confirmation, destination backup, preserved IDs/timings and no automatic probing. Unsupported formats fail safely. The fixture is test-only, not a production package resource. Windows-specific options/OS registrations are not blindly imported.

rc.4 also adds optional bounded local WAV requests, log naming modes (stable/UTC/local/fixed snapshot) and validated persistent pane heights. No application dependency, monitoring algorithm or history schema changed. Closed copies of real rc.3 sessions and incident/comment evidence preserve all logical records and config/preferences bytes; SQLite header changes are not claimed byte-identical. Actual extracted apphosts verify import/backup and applied/reloaded pane/log preferences before restoring loopback configuration for monitoring.

| Actual native platform | Default .NET | Separately enabled checks | Extracted executable |
|---|---|---|---|
|Windows Server2025 x64|159 passed,5 intentional skips|3 real/native+1 audio request passed;8 Python|Six stages passed;5 normal exits0, deliberate kill1|
|macOS15 arm64|159 passed,5 intentional skips|3 real/native+1 audio request passed;8 Python|Six stages passed;5 normal exits0, deliberate kill-9|
|Ubuntu24.04 x64 / Xvfb+Openbox|160 passed,4 intentional skips|3 real/native+1 audio fallback passed;8 Python|Six stages passed;5 normal exits0, deliberate kill-9|

164 unique .NET cases across invocations/platforms; no public endpoint is required by default tests. Locked restore/auditing and Release builds pass with0 warnings/errors. Downloaded artifact/API hashes, every manifest member, RID/CPU/native SQLite/runtime, MIT/notices, Unix executable modes and absence of test/debug assets independently pass. Same downloaded Mac apphost repeated all six stages locally on26.3.1. [Local record](../evidence/verification/2026-10-22-desktop-completion-local-mac.json). CodeQL39851153694 retains0 SARIF results. Scoped coverage2593/3248 lines=79.83%,1881/2812 branches=66.89%, lower than rc.3 after added integration code; no suppression.

Windows/macOS audio processes accepted the request, but audible delivery is unverified. Linux lacks paplay and reports a safe fallback. The attempted Windows native notification adapter failed independent on-screen Shell observation in run39848205141; it was withdrawn, not represented as delivered. Retained failure logs/reports are linked from the candidate review. Accessible in-app alerts remain. Physical tray/keyboard/native Save-panel/multiple-display and actual-login tests remain blocked: Accessibility/event posting denied and the owner explicitly unavailable. Programmatic real-control tests do not substitute for these. No real-user startup registration, host network or security setting was changed.

Inventory **91.7%=(15 complete+3 partial×0.5)/18**: settings persistence/profile import closes; alerts/preferences/placement retain their precise verification gaps. Frozen rc.3 remains88.9%. Readiness stays **8.5/10**, not100% or10/10. Trusted Mac distribution remains blocked by unavailable Developer ID/notarization; Windows remains unsigned. Wider topology/suspend/accessibility/Wayland/endurance and public publication/submission review remain open.


## Final release alignment — selected rc.4

The earlier rc.3 payload proposal above is superseded by the owner’s explicit selection of exact6c812fde9a29eeb8cfd6399a2f1a75e333e81e03/0.1.1-rc.4 for proposed public v0.1.1. No new build, runtime certification, signing or parity change occurs. Three archive hashes/manifests/native dependencies/permissions and exact-source live CI were rechecked. Current91.7% rubric and the three remaining partial areas are unchanged. [Aligned publication checklist and GO/NO-GO conditions](../release/v0.1.1/PUBLICATION.md). Publication still requires separate authorization; current public downloads remainv0.1.0.
