# Migration roadmap

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
