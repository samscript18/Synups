# Release readiness — Phase 4B

Assessment date: **2026-10-21**. **8/10 submission readiness; not production-ready and not 10/10.** Mandatory ordinary-user delivery and desktop acceptance gates remain open. No GitHub Release or competition entry has been published. Historical assessments remain in Git and [MIGRATION_LOG.md](MIGRATION_LOG.md); this checklist supersedes their current-state wording.

Verified application source: **292b847bffa96d2a23d771c3ba0ce4648c06844b**, `feat/avalonia-migration`, [successful three-OS run 39678991269](https://github.com/samscript18/Synups/actions/runs/39678991269). [Exact downloaded evidence](../evidence/verification/2026-10-21-published-apphosts.json), [platform limits](CROSS_PLATFORM.md#final-phase4b-published-executable-evidence--292b847), [launch instructions](../release/INSTALLATION.md) and [candidate notes](../release/RELEASE_NOTES.md).

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

The separate two-hour Core-engine observation is still running at this documentation checkpoint; its final result will be appended only after stop/restart/reopen pass. It does not replace GUI endurance.

## Mandatory open gates

- [ ] **Trusted macOS delivery:** Gatekeeper rejects the actual final bundle (assessment exit 3). Ad-hoc integrity is not Developer ID/notarization. Obtain a legitimate signing identity/notarization workflow or agree and verify a candid distribution policy; never disable OS security to manufacture a pass.
- [ ] **Ordinary downloaded desktop acceptance:** browser download/quarantine, consumer Windows desktop, real Linux desktops/Wayland, physical tray Show/Exit, native save-picker, topmost/z-order, keyboard traversal and screen-reader/high-contrast/high-DPI/disconnected-screen behavior. Programmatic controls, a Server runner and Xvfb cover narrower behavior.
- [ ] **OS notification request/delivery/denial:** macOS identity/permission and Linux daemon/host combinations. Windows currently has a documented in-app fallback; native Windows delivery and sounds are absent.
- [ ] **Broader reliability:** representative hours/days GUI and large-history workloads, real pending-native-ICMP cancellation, IPv6/VPN/firewall/no-network/suspend/resume, physical disk-full and power-loss experiments. Healthy loopback, file read-only tests and process kill are different evidence.
- [ ] **Public delivery:** final reviewed release handoff; CI artifacts expire after 30 days and may require login. Preserve approved packages/logs and publish only after explicit authorization. No installers, trusted publisher signatures or public Release are delivered.
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
| Reviewed public landing and release handoff | 0.5 | Reviewed source/materials and draft PR; final landing/handoff tracked below |
| Trusted ordinary-user distribution | 0 | Gatekeeper rejection and manual installed-desktop checks remain |
| **Total** | **8/10** | **Submission remains blocked; no 10/10 claim** |

Legacy inventory parity remains **72.2% = (9 + 8×0.5)/18**. It is separate from this release rubric. Discovery, validated real-WirePeep settings import, login/sound/taskbar/focus and menu/clipboard/help details remain missing; see [FEATURE_PARITY.md](FEATURE_PARITY.md).

## Data and scale limits

SQLite commits are the evidence source of truth. Pending batches are memory-owned and do not survive force quit/power loss; keep the app open, fix storage and Retry. Corruption never triggers silent reset. The main-DB page limit defaults to 1 GiB (Settings 256 MiB–10 GiB); WAL/SHM/journal, backups, logs and temporary work need additional disk space. A page cap is not a total disk reservation.

Retention defaults to preserve-all; positive retention and manual cleanup require confirmation. Unresolved/interrupted sessions are retained. Manual cleanup creates a verified full backup before deletion; backup failure cancels deletion. Freed pages are reused without shrinking the DB. Automatic log copies have a separate 256 MiB cap and can lag until Stop/Close; a failed copy is visible and must be repaired or explicitly disabled.

Large-history loading/export/retention still materializes records in memory. No full-capacity bounded-memory/endurance certification is made. Offline restoration and real corruption/permission recovery need manual review; never move an open DB or discard a recovery journal. Use the in-app full SQLite backup, not outage CSV, for history preservation.

## Source-control handoff

Draft [PR #1](https://github.com/samscript18/Synups/pull/1) prepares a history-preserving migration review against original master. Source checkpoint commits/pushes are authorized; no force-push/history rewrite/Release/submission occurred. Repository metadata now identifies Synups. The requested default-branch change was applied and verified through the API on 2026-10-21: `feat/avalonia-migration`, README `# Synups`. Original master remains intact; this is a reversible presentation change, not a PR merge or Release.
