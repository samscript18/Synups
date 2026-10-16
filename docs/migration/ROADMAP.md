# Migration roadmap

Phase 2 now provides a functioning local ICMP monitoring flow. Remaining legacy and platform/distribution gates mean it is not a submission-ready entry.

## Phase 2 — implemented
Real ICMP probing, cancellable bounded asynchronous group scheduling, legacy characterization, monotonic failure/recovery, editable profiles, atomic local persistence/recovery, live Avalonia state, session outages/comments and CSV export. See MIGRATION_LOG.md for actual checks. Outage history is process-session memory, with explicit export; durable reload is not implemented. Errors no longer imply recovery. Original default discovery rules are not executed.

## Phase 3 — recommended next, not started
1. Run the working-tree CI after an explicitly authorized commit/push. Validate native Windows/Linux, IPv6, ICMP permission failures, external disconnect/reconnect, suspend/resume and low-resource scenarios.
2. Complete legacy auto text logs, app preferences and explicit saved-WirePeep profile import. Add durable history only with corruption recovery and retention limits; never invent measurements across process gaps.
3. Port capability-based alerts, tray, sound, login launch, window placement and single-instance/minimized behavior. Keep unsupported OS capabilities visible and documented.
4. Finish remaining clipboard/about/help/menu parity and native keyboard/screen-reader verification. Address defects before cosmetic work or analytics.

## Later release gates
Run tests and real network scenarios on Windows, macOS and Linux; verify keyboard/accessibility, high DPI, resizing, light/dark preferences, suspend/resume and shutdown. Capture original WPF screenshots at baseline SHA on Windows and authentic Synups screenshots. Produce, launch and verify self-contained artifacts per claimed OS/architecture; handle signing/notarization/packaging requirements. Run dependency/license audit, publish public MIT repository/release, finish an honest README migration account and submit the entry.

## Competition scope
Primary: Best IT Pro Tool. Secondary: Best Cross-Platform Port / Best Legacy Revival. Official deadline: October 23, 2026 at 23:59 UTC (October 24 at 00:59 Africa/Lagos). Source: https://avaloniaui.net/blog/avalonia-port-challenge (checked October 16). Do not sacrifice reliability or claim untested platforms to meet the date. The original code predates the challenge; retained Git ancestry supplies provenance. No entry has been submitted by this work.
