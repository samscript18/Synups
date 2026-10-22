# Legacy parity remains 91.7%

The protected public v0.1.1 application is source `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`, internally `0.1.1-rc.4`. Its released inventory score is **16.5/18 = 91.7%**. The diagnostics expansion does not add points to that score or retroactively change released bytes.

The authoritative inventory, historical scores, evidence and release alignment remain in [Feature inventory and parity](migration/FEATURE_PARITY.md#desktop-completion-rc4--current-application-inventory), including its [final public-release assessment](migration/FEATURE_PARITY.md#public-v011-917-released-inventory). This document restates the same original 18 rows. Complete = 1 point, partial = 0.5, pending = 0. Simulation is deliberately excluded from production, and tests/builds are an engineering gate; those two original source-inventory rows remain outside the denominator.

| Original inventory item | Frozen v0.1.1 score | Qualification preserved |
|---|---|---|
| Startup / shutdown | Complete 1 | Owned stop/save/close, minimized startup and existing-instance restoration |
| Main status UI | Complete 1 | Real measurements, freshness, selection, scoped clipboard and timing |
| ViewModels / state | Complete 1 | UI-free snapshots and observable presentation |
| Named IP targets | Complete 1 | Validated literal IPv4/IPv6 editing and stable identifiers |
| Groups and timing | Complete 1 | Membership, enablement and bounded timing controls |
| Default profile | Complete 1 | Original resource/timings, non-destructive merge, explicit discovery setup |
| Discovery | Complete 1 | Confirmed native gateway and original ordered candidate checks; safe manual substitution for startup probing |
| Connectivity | Complete 1 | Genuine finite-timeout ICMP with typed errors |
| Poll orchestration | Complete 1 | Bounded independent group loops and stop-on-success rotation |
| Failure / recovery | Complete 1 | Characterized thresholds, monotonic intervals, explicit indeterminate state and no shutdown recovery |
| Outage list/comments | Complete 1 | Real stable incidents/comments and retained timeline |
| Text logs / statistics | Complete 1 | Durable transitions and observed summaries; no invented Internet uptime |
| Export | Complete 1 | Chronological escaped UTC CSV and explicit unknown recovery |
| Alerts | Partial 0.5 | Actual OS notification delivery, audible output and platform attention/taskbar behavior remain unverified |
| Preferences | Partial 0.5 | Actual next-login execution/removal remains unverified; arbitrary legacy filename templates are not claimed |
| Settings persistence | Complete 1 | Genuine Menees 5.1.2 fixture, validated profile-only import, confirmation, backup and reload; unsupported historical variants rejected |
| Placement / tray | Partial 0.5 | Physical tray clicks, native multiple-display restoration and full desktop acceptance remain unverified |
| Menus / dialogs | Complete 1 | Inventoried functional intent implemented; physical native Save-panel/keyboard acceptance remains separate |

**Calculation: (15 complete × 1 + 3 partial × 0.5 + 0 pending × 0) / 18 = 16.5/18 = 91.7%.** This is the unchanged coarse functional inventory rubric, not a percentage of engineering effort, source coverage or platform certification.

Diagnostics, durable investigation evidence, new protocol tests and packaging improvements earn no extra legacy inventory rows. Their independent status is recorded in [the 31-feature diagnostics matrix](diagnostics/FEATURE_MATRIX.md), [verification report](diagnostics/TEST_REPORT.md) and [release decision](diagnostics/RELEASE_READINESS.md). A diagnostic test or a headless UI check does not close the three remaining legacy desktop evidence gaps. Public v0.1.1, v0.1.0 and historical candidates retain their original source, assets and assessment.
