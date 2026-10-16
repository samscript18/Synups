# Migration roadmap

Phase 3 local functional work is implemented. Cross-platform runtime/distribution gates are outstanding, so Phase 3 is not declared fully complete or production-ready. See [release checklist](RELEASE_READINESS.md).

## Delivered
Phase 1: separate Avalonia/Core/Tests solution, audit/provenance/design tokens. Phase 2: real ICMP, bounded cancellable groups, characterized legacy behavior, monotonic thresholds/recovery, editable profiles, live UI and CSV. Phase 3: durable SQLite incidents/comments/sessions, interrupted-session handling, automatic session logs/statistics, retention/capacity safeguards, configurable alerts, tray policy, window placement and desktop preferences. No monitoring engine replacement or visual redesign.

## Next milestone — platform proof and release hardening
1. Review and commit/push the prepared changes explicitly. Run the three-OS workflow; preserve uploaded TRX, command logs, native captures and self-contained directories. Investigate every failure without disabling warnings or audits.
2. Run the actual published executables on Windows x64, macOS arm64 and Linux x64. Verify Windows ICMP/cancellation, native Linux display/permission paths, tray restore/menu exit, native notification permissions/denials and user-facing file errors. Publishing from macOS is not runtime proof.
3. Exercise external disconnect/reconnect, IPv6, VPN, sleep/resume, disconnected screens/DPI, disk-full repair and hours/days of monitoring. Run native keyboard/screen-reader tests and check resource trends. The two one-minute loopback runs are smoke evidence only.
4. Obtain an actual Windows WirePeep .stgx fixture and original WPF runtime capture. Validate the exact Menees 5.1.2 schema and settings semantics before shipping preview/backup/import. Implement explicit bounded discovery only after capability tests; never restore automatic startup probing.
5. Complete highest-value remaining legacy integrations: Windows native alert adapter, optional sound/focus/taskbar behavior where supported, explicit per-OS login registration, single-instance activation and clipboard/help/about. Keep fallback claims honest.
6. Produce attributed third-party notices and reviewed platform packages, signing/notarization, launch instructions, published artifact checksums and a genuine desktop/window capture where possible. The current captures are accurate native client-area renders.

No cloud, accounts, advanced analytics or major visual redesign is planned before these gates. No release, entry submission or automatic push is authorized by this phase.

## Competition
Primary Best IT Pro Tool; secondary Best Cross-Platform Port / Best Legacy Revival. Recorded challenge deadline: October 23, 2026 at 23:59 UTC, per the [challenge announcement](https://avaloniaui.net/blog/avalonia-port-challenge). Retained upstream Git ancestry and MIT attribution remain prerequisites. This task did not submit an entry.
