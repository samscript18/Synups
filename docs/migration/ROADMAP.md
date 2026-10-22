# Migration roadmap

Current22 October2026 checkpoint: [reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md), application6c812fde9a29eeb8cfd6399a2f1a75e333e81e03,91.7% inventory/8.5 readiness. Earlier phase sections are historical. The user-selected proposed v0.1.1 payload is now frozen rc.4 at91.7%, with internal0.1.1-rc.4 version disclosed; public0.1.0 remains unchanged. Earlier rc.3 payload decisions below are historical.

Phase 3 local functional work and Phase 4A three-OS CI/native-harness checkpoint are implemented. Published-executable and installed-desktop/distribution gates remain open; Synups is not production-ready. See [release checklist](RELEASE_READINESS.md).

## Delivered
Phase 1: separate Avalonia/Core/Tests solution, audit/provenance/design tokens. Phase 2: real ICMP, bounded cancellable groups, characterized legacy behavior, monotonic thresholds/recovery, editable profiles, live UI and CSV. Phase 3: durable SQLite incidents/comments/sessions, interrupted-session handling, automatic session logs/statistics, retention/capacity safeguards, configurable alerts, tray policy, window placement and desktop preferences. No monitoring engine replacement or visual redesign.

## Next milestone — platform proof and release hardening
1. Preserve the reviewed checkpoint and [actual three-OS CI evidence](CROSS_PLATFORM.md#corrected-checkpoint--7b75296). Review subsequent UI changes and verify the new exact SHA; retain logs, TRX, native captures and packages. Investigate every failure without disabling warnings or audits.
2. Run the actual published executables on Windows x64, macOS arm64 and Linux x64. Verify Windows ICMP/cancellation, native Linux display/permission paths, tray restore/menu exit, native notification permissions/denials and user-facing file errors. Publishing from macOS is not runtime proof.
3. Exercise external disconnect/reconnect, IPv6, VPN, sleep/resume, disconnected screens/DPI, disk-full repair and hours/days of monitoring. Run native keyboard/screen-reader tests and check resource trends. The two one-minute loopback runs are smoke evidence only.
4. Obtain an actual Windows WirePeep .stgx fixture and original WPF runtime capture. Validate the exact Menees 5.1.2 schema and settings semantics before shipping preview/backup/import. Implement explicit bounded discovery only after capability tests; never restore automatic startup probing.
5. Complete highest-value remaining legacy integrations: Windows native alert adapter, optional sound/focus/taskbar behavior where supported, explicit per-OS login registration, single-instance activation and clipboard/help/about. Keep fallback claims honest.
6. Produce attributed third-party notices and reviewed platform packages, signing/notarization, launch instructions, published artifact checksums and a genuine desktop/window capture where possible. The current captures are accurate native client-area renders.

No cloud, accounts, advanced analytics or major visual redesign is planned before these gates. The Phase 4A request authorizes reviewed commits and pushes to the confirmed repository. Releases and competition submission remain separate actions and are not authorized.

## Competition
Primary Best IT Pro Tool; secondary Best Cross-Platform Port / Best Legacy Revival. Recorded challenge deadline: October 23, 2026 at 23:59 UTC, per the [challenge announcement](https://avaloniaui.net/blog/avalonia-port-challenge). Retained upstream Git ancestry and MIT attribution remain prerequisites. This task did not submit an entry.


## Phase 4B execution

Actual apphost protocol, single-instance activation, own icons, dependency notices, Mac bundle and candidate launch/release notes are implemented. Obtain and inspect exact-revision three-OS CI results; finish the recorded two-hour engine observation; retain evidence and prepare a reviewed default-branch merge. Do not equate programmatic control checks with physical desktop/accessibility certification or ad-hoc signatures with trusted publishing. Public Release and competition submission still require explicit authorization.


## Phase 4B verified checkpoint and next release gates

Actual extracted apphosts now pass on Windows/macOS/Linux at292b847 (run 39678991269); the default public landing identifies Synups. Prepared archives retain runtime/native/font notices, icons, checksums and real native evidence; the Mac resource layout passes strict validation after extraction. Candidate notes and per-OS launch/manual acceptance instructions are in docs/release. No public Release/submission was performed.

Next work prioritizes the remaining mandatory delivery gates over optional legacy expansion: trusted Mac signing/notarization or a reviewed verified distribution policy; ordinary downloaded-desktop acceptance on consumer Windows/real Linux and Mac quarantine; physical tray/save-picker/notification/keyboard/accessibility checks; representative GUI/large-history/endurance and external-network/IPv6/VPN/suspend/storage experiments. Then preserve approved immutable artifacts beyond CI expiration, finish source/material review and obtain explicit authorization before public Release/competition submission.

Legacy parity remains72.2%; discovery/import/login/sound/taskbar/focus and menu details stay separately tracked. Do not claim ordinary-user distribution or production readiness from the successful scoped apphost checks.


The scoped two-hour real-Core observation and two five-minute actual GUI processes now have retained resource evidence. These close the short healthy-loopback observation milestone, not representative multi-day GUI/large-history or physical-failure testing. Keep the broader acceptance work ahead of optional cosmetic/analytics features.


## Phase 5 handoff

Freeze app binaries at2973d3349fcb5af5639597920842288e6549bbd0/run39682197118; exact downloaded hashes and real apphost results are retained. [Publication instructions](../release/PUBLICATION.md) assemble original CI bytes and prepared notes/evidence without rebuilding. [Submission information](../release/SUBMISSION.md) lists the actual report/screenshots and explicitly non-live planned download URLs. Prioritize legitimate Mac trust/ordinary-desktop acceptance and user-authorized permanent Release delivery before optional legacy features. Publication and form submission remain separate user decisions; no automatic release or entry occurs.


## Public release completed

The user authorized [Synups 0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0) at frozen source `2973d3349fcb5af5639597920842288e6549bbd0`. Existing reviewed packages and evidence were published unchanged; all 15 anonymous downloads/hash checks passed. Permanent download/readme links are in place. Next actions remain legitimate signing/ordinary-desktop acceptance and separately authorized competition submission; do not submit the form automatically or infer10/10 readiness from public availability.


## Competitive work completed / next decision — 22 October 2026

Guided first-target setup, near-top README comparison and judges’ guide are implemented. Controlled real ICMP loss/recovery in a disposable Linux namespace now has actual native UI, CSV and separate-process persistence evidence; no host network was altered. Source-specific three-OS packaged verification is retained in CROSS_PLATFORM.md. Public 0.1.0 is untouched; new UI belongs to a separate unpublished 0.1.1-rc.1.

Next prioritize legitimate signing/trusted distribution and a human-operated desktop acceptance pass (especially Mac tray, keyboard and native Save dialog). Accessibility currently blocks those local automation tests. Discovery/import/native notification expansion remains deferred; do not trade a verified submission for speculative parity. Release/submission requires separate authorization.


## Current handoff after final hardening

Completed in separately verified rc.2: explicit read-only default-gateway preview/confirmation on all three runners; paginated persisted incident timeline/streamed CSV; IP/incident clipboard, Help/About, shortcuts and opt-in transition focus; CodeQL/security policy/coverage. [Exact candidate and limits](../release/FINAL_HARDENING_CANDIDATE.md). Earlier deferred-discovery/focus notes describe earlier checkpoints, not current rc.2. Original modem/transceiver discovery remains absent.

Prioritize a person completing the Mac acceptance checklist, trusted distribution where legitimate credentials exist, review of candidate release versus stable public0.1.0, and separately authorized competition submission before the deadline. Do not start latency-history architecture, speculative import/login/sound features or a redesign. Sparkline remains conditional on a valid persisted sample contract; no such history exists. Native notification delivery and broad physical desktop/suspend/IPv6 remain follow-up gates. Source-only tests are not substitutes for them.

## After verified legacy completion rc.3

Stop expanding scope: immutable7ca85cc now reaches88.9% inventory with verified three-OS packages. Review [candidate evidence](../release/LEGACY_COMPLETION_CANDIDATE.md), complete available [physical acceptance](../release/MACOS_ACCEPTANCE.md), then request separate publication/competition-submission authorization. Preserve public 0.1.0 and both older candidates. Trusted distribution and public judge access matter more than chasing94.4% through unverified login, OS notifications, physical tray or an invented importer. Keep those four rows partial until genuine evidence exists. No new release or submission is automatic.


## Post-rc.4 handoff

Genuine original-profile import is now verified; remove it from the current missing-feature list for rc.4 only. Public0.1.0/rc.3 do not acquire this functionality retroactively. The owner has now chosen frozen rc.4 for proposed v0.1.1. Next action is publication review/authorization using the aligned15-file upload set; internal version remains0.1.1-rc.4. Do not let optional features delay the deadline. Retain manual tray/Save/keyboard/multi-display/login and actual OS-delivery blockers; the owner is unavailable for those checks. Trusted signing remains credential-dependent. Publish only after authorization, anonymously verify every asset, then obtain separate submission authorization/receipt. [Current review](../release/DESKTOP_COMPLETION_CANDIDATE.md).
