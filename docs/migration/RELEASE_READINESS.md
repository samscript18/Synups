# Next release milestone checklist

**Phase 4B is in progress (2026-10-21).** Published-apphost automation, single-instance restoration, identity/bundling and dependency notices are implemented; final exact-source three-OS outcomes and two-hour measurements are pending. Historical assessments below are retained and will be superseded by the final evidence section. No10/10 or production-readiness claim.

Assessment: reviewed Phase 4A checkpoint with three-OS CI/native-harness evidence and configurable storage policy, **7/10 product readiness**. The 10/10 submission target remains aspirational; storage improvements do not close the platform/release gates. Not production-ready or a completed competition entry. Phase 4A now provides three-OS CI/native-harness evidence; published-apphost, installed-desktop and other release gates remain open.

## Passed locally on macOS arm64

- [x] Existing monitoring engine preserved; failure/recovery/rotation/freshness/lifecycle regressions pass.
- [x] Locked restore with NuGet audit enabled; Release build zero warnings/errors.
- [x] 95 deterministic/headless tests passed locally after storage-test isolation; corrected CI also passed all 96 unique cases per OS across default and opt-in loopback invocations. Includes 24 storage-policy/Settings cases and concurrent SQLite coverage; local correction logs/TRX are in `artifacts/phase4a-correction/`.
- [x] Durable sessions/incidents/comments and stable IDs reload through new store/service instances.
- [x] Two native process launches reload clean session records; no recovery is inferred at restart or stop.
- [x] Corrupt history fails closed and preserves bytes. Pending write failures retain memory and block clean disposal until retry.
- [x] Transaction rollback, duplicate retry, retention and size/competing-writer boundaries tested.
- [x] 1 GiB default, Settings range 256 MiB–10 GiB, existing-page-size enforcement and atomic durable capacity changes/reopen tested.
- [x] Legacy database upgrades preserve records/IDs; real >256 MiB fixture remains intact above a 256 MiB setting. Unsafe lowering is rejected.
- [x] Real SQLITE_FULL retains pending batches, blocks cleanup/backup/close, and recovers after a stopped capacity increase and Retry.
- [x] 80%/95% allocation thresholds and notification deduplication tested; real WAL/SHM, backup and log file sizes accounted separately.
- [x] Verified SQLite online snapshots (including a committed WAL record), independent reopen, confirmed manual cleanup and backup-failure cancellation tested.
- [x] Keep-all default, confirmed persisted retention, daily cleanup during monitoring, and protection of active/unresolved/interrupted sessions tested.
- [x] Automatic session JSON/CSV and summaries, missing-copy rebuild, file error/fallback paths tested.
- [x] Alert preference/deduplication/cooldown and bounded native-worker cancellation tested.
- [x] Geometry/clamping/topmost/auto-start/confirm-close and corruption defaults tested headlessly.
- [x] Native macOS hide-on-minimize/restoration handler and Stop/Close passed; no orphan smoke process remained.
- [x] Final storage-change smoke: two one-minute native processes passed (60.623s/60.595s observed), with clean reload, 1 GiB effective-limit checks and inspected overview/targets/Settings captures. No remaining Synups process. This is short smoke evidence, not endurance validation.
- [x] Phase 3 baseline only: three self-contained RID directories published on this Mac; original licence included. Local verification archives preserve Unix executable mode and include checksums. No release created.
- [x] Authentic native client-area captures and attributed upstream before illustration recorded.
- [x] MIT licence and original source/build history preserved; no production mock probes or generated metrics.

Latest verified application checkpoint: [shared UI three-OS CI manifest](../evidence/verification/2026-10-16-ci-ui.json), source a368d83dbb5494e283efa02b7e8036a9ce9664c4. Historical manifests retain their original source snapshots.

## Required before release / supported-platform claims

- [x] Reviewed checkpoint 88e7ea507d82a39238ba48ac97936a2d2fa23c28 pushed to feat/avalonia-migration. Run 38937720125 passed all three OS jobs; all logs/artifacts downloaded and inspected. [CI results](CROSS_PLATFORM.md#actual-hosted-results--checkpoint-88e7ea5).
- [ ] Launch each **published executable** on its target OS. Current native harness references production App/VM but is not a published-apphost/package test.
- [x] Windows/Linux native loopback ICMP, SQLite binding/storage tests, native UI/Stop/Close/restart and publishing passed in CI.
- [ ] Real pending native-ICMP cancellation and genuine permission-denial/read-only/moved-path scenarios on each OS; deterministic error tests do not replace these.
- [ ] Native tray menu click/exit and secondary-instance behavior. Linux tests must cover absent hosts without hiding the window.
- [ ] OS notification request/delivery/denial tests; macOS app identity and Linux daemon availability. Windows native adapter remains missing.
- [ ] External disconnect/reconnect, IPv6, VPN/firewall, sleep/resume, clocks, DPI and disconnected-screen tests.
- [ ] Hours/days monitoring with CPU, memory, pending-worker and database growth measurements. No endurance claim yet.
- [ ] Real abrupt-process/power-loss recovery test. Current automated interruption test rolls back an uncommitted transaction; it is not a physical power-failure experiment.
- [ ] Physical disk-full, read-only/moved-database recovery UX on each target OS. Logical SQLite-full → increase → Retry passes locally; physical exhaustion still blocks pending flush/cleanup/close until storage repair. No automatic relocation or destructive reset exists.
- [ ] Validate offline restore instructions across target filesystems/permissions and genuine corruption; independent snapshot reopen passes locally, but no in-app restore/repair wizard exists. Never move an open SQLite file or discard its recovery journal.
- [ ] Native keyboard/screen-reader/high-contrast/high-DPI review; actual desktop/window screenshots where possible.
- [ ] Fresh original WirePeep Windows capture, exact-version real settings fixture and validated preview/backup importer.
- [ ] Explicit bounded discovery and remaining legacy sound/login/focus/taskbar/clipboard/help/single-instance activation as prioritized parity work.
- [ ] Platform bundles/installers, executable launch instructions, signing/notarization, dependency notices/licences, checksums and artifact retention beyond CI expiration.
- [ ] Public source/release review and honest final migration write-up; separately authorized challenge submission.

## Storage and shutdown limits users must understand

Database commits are the evidence source of truth; derived log copies may lag until Stop/Close. The default main-database limit is 1 GiB; Settings accepts 256–10240 MiB. It is a SQLite page limit, not a total disk budget. WAL/SHM/journal, managed backups, derived logs and temporary work require extra space. Shared volume free space and measured file lengths are displayed separately; external copies and old log folders are not tracked.

At a logical page-limit failure, monitoring stops with unsaved batches retained. Stop, raise capacity, then Retry history storage; the recovery path does not flush before changing capacity. Physical disk errors/permissions still need repair. Backup/cleanup flush first and cannot discard pending evidence. Keep Synups open until pending writes save; pending batches are memory-owned and do not survive force quit/power loss. Corruption with no new pending data allows normal close without reset. A log-copy failure keeps the window open; fix its folder or explicitly disable automatic copies, then retry close.

Retention is off by default; positive days require confirmation. Manual cleanup confirms scope and creates a verified full SQLite backup before deletion; unresolved/interrupted sessions and derived copies are kept. Freed pages are reused without shrinking the database, so 80%/95% allocation warnings can remain. Each capacity emits at most one warning and one critical escalation per process; restarting can warn again. Backups/logs need manual archival and can consume the disk independently of the database limit. [Backup/restore instructions and storage design](ARCHITECTURE.md#capacity-policy-backup-and-recovery) describe the recovery file set and remaining limits.

The configured upper bound is tested without filling a 10 GiB database. Large-history load/export/retention still materializes records in memory; no bounded-memory, full-size endurance or physical-power-loss claim is made. Phase 4A regenerated and downloaded archives for the current storage-policy checkpoint on each matching runner; direct apphost execution remains unverified. Unresolved incidents remain unresolved unless a real recovery was observed in that session; a later session does not manufacture recovery.

## Phase 3 Git/CI instructions — historical

From the repository root, inspect `git status --short` and `git diff`, then stage only the reviewed files:

```sh
git add README.md docs/ .github/workflows/synups.yml eng/native-smoke.cs eng/native-smoke.py eng/package-artifacts.py synups/
git diff --cached --check
git diff --cached --stat
git diff --cached
git commit -m "Add durable Synups history and desktop lifecycle preferences"
git push origin HEAD
```

Do not add ignored artifacts or caches. Push triggers the prepared workflow; manual dispatch is also available. Check the exact resulting commit's jobs and artifacts before updating platform claims. This is a recommendation, not authorization to commit/push/release on the user's behalf.


## Phase 4A checkpoint review

The current request explicitly authorizes a reviewed source commit and push to the verified Synups remote. Local gates have passed again for the current storage-policy source: 94 deterministic/headless cases plus one explicit real-network case, zero Release build warnings/errors and two native macOS runs. Fresh three-RID archives passed verification with runtime notices, full content hashes, original licence and Unix executable permissions. Build outputs, TRX and caches stay ignored. No public release or competition submission is authorized.

Hosted results and checkpoint SHA will be recorded after the push attempt. Existing Windows/Linux and published-apphost checkboxes remain open until the corresponding evidence exists. Readiness remains 6.5/10 pending that evidence.


### Recorded checkpoint evidence

The previous pending note above is historical. Source commit **88e7ea507d82a39238ba48ac97936a2d2fa23c28** was committed and pushed without force or history rewrite. [Actual run 38937720125](https://github.com/samscript18/Synups/actions/runs/38937720125) passed Windows Server 2025 x64, macOS 15.7.9 arm64 and Ubuntu 24.04.5 x64. On each: 94 deterministic/headless passed, the separately enabled loopback case passed, two native verification processes passed, and self-contained publishing/packaging passed. Downloaded log/TRX/native-capture/archive evidence was checked; details/hashes are in the [CI manifest](../evidence/verification/2026-10-16-ci-checkpoint.json).

Readiness is now **7/10** based on genuine OS evidence. Published-apphost execution is **not tested on any OS**, native adapters/menu interactions and installed desktop environments need further checks, and signing/notarization/complete dependency notices, endurance, accessibility and remaining legacy gaps still block a production release. The default branch remains original master; review/merge and release remain separate actions. No public release or competition submission occurred. A focused licence LF policy and these evidence updates are the only follow-up source-control changes; inspect their subsequent run separately from the recorded source checkpoint.


The documentation/licence follow-up run38939047265 passed macOS/Linux but exposed a Windows storage-integration timeout (93 passed/1 failed/1 opt-in skip); it is not counted as a full pass. The corrective collection isolates real disk fixtures while preserving production seven-second shutdown behavior and explicit concurrency/deadline coverage; a new concurrent-groups SQLite case passes locally. See CROSS_PLATFORM.md for the exact failed SHA and evidence. Follow-up hosted verification remains required before calling the latest branch green.


### Corrected checkpoint passed

The pending correction above is now verified: **7b75296f97ea62d986e7d16b152dedaabcf999d6**, branch **feat/avalonia-migration**, [run 38940757093](https://github.com/samscript18/Synups/actions/runs/38940757093), passed all three OS jobs. Each passed 95 deterministic/headless plus one separate real-loopback case (96 unique), zero-warning/error Release, native Start/measure/Stop/Close/restart and publishing. All downloaded package hashes/runtime notices/exact licence bytes/Unix executable modes and actual native logs/captures passed inspection. The failed previous run remains documented. Readiness stays **7/10**; published-apphost execution and remaining release gates are open. The following UI refinement is a separate reviewed checkpoint and must not inherit this run's pass automatically.


### User-requested UI refinement

Shared ink/teal tokens, typography, semantic badges, real-state overview tiles, compact navigation and grouped configuration/history/preferences panels are implemented without changing the engine or adding dependencies. Final local Release/headless suite passed95 cases plus the intentional networking skip; explicit real loopback and two native macOS restart runs passed. Native normal/minimum820×560 client captures were inspected and retained with [source hashes](../evidence/verification/2026-10-16-ui-macos-arm64.json). Exact new UI source must pass its own three-OS CI run before its platform claims are updated. Native accessibility/keyboard review and published-apphost execution remain open; cosmetic refinement does not raise the7/10 readiness score.


### UI checkpoint verified on all targets

The pending UI gate above is now passed for **a368d83dbb5494e283efa02b7e8036a9ce9664c4**: [run38944933816](https://github.com/samscript18/Synups/actions/runs/38944933816) passed all three OS jobs. Each exercised95 deterministic/headless plus1 separate real-loopback case, Release0 warnings/errors, two native process/Stop/Close/restart checks and verified self-contained publishing. Downloaded complete packages/logs/TRX and normal/minimum-size client captures were checked; exact source/licence/notices/hashes/Unix modes passed. Selected genuine OS captures and sanitized [evidence](../evidence/verification/2026-10-16-ci-ui.json) are retained in Git. The final evidence commit does not change that application source. All unchecked release gates above remain open; readiness stays **7/10**, and published executables remain **not launched on any OS**.
