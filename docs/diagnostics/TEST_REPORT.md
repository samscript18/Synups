# Diagnostics verification report

> Frozen e16e3cd baseline report. See [final hardening](HARDENING_REPORT.md) for follow-up changes, verification and the current release decision.

Development source revision will be frozen at the first candidate commit; starting reviewed HEAD `bbd92ef7395ae4cd20534920398b317353e41371`. Protected v0.1.1 application `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`. Host: macOS26.3, osx-arm64; .NET SDK10.0.402, runtime10.0.13. User-facing reporting date October22,2026, Africa/Lagos; machine observation times are retained in UTC.

## Commands and results so far

Commands execute from `synups/` unless a root path is stated. Local SDK path is `/private/tmp/synups-dotnet/dotnet` (called `dotnet` below for readability). Exact arguments retained here; test TRX stores individual outcomes and skipped reasons.

| Command | Result |
|---|---|
| `dotnet --info` | SDK10.0.402/runtime10.0.13; MacOS26.3 arm64 |
| `dotnet restore ../Synups.slnx --locked-mode --disable-parallel -m:1 -nr:false` | Passed baseline/current; all five runtime targets locked restore passed |
| `dotnet build ../Synups.slnx -c Release --no-restore -p:UseSharedCompilation=false -m:1 -nr:false` | Original and integrated builds zero warnings/errors |
| `dotnet test ../Synups.slnx -c Release --no-build -m:1 -nr:false --logger trx --results-directory ../artifacts/diagnostics/baseline` | Original164:159 passed,0 failed,5 gated skips |
| Same test command, `integrated` output | First expanded229:219 passed,2 failed,8 skips. Dynamic modal lookup tests used FindControl without a NameScope. Fixed by visual button matching; assertions retained. |
| `dotnet test ../Synups.slnx -c Release --no-restore -p:UseSharedCompilation=false -m:1 -nr:false --logger trx --results-directory ../artifacts/diagnostics/regression` | Final integrated230:222 passed,0 failed,8 gated skips |
| `SYNUPS_REAL_NETWORK_TESTS=1 dotnet test ../Synups.slnx -c Release --no-build -m:1 -nr:false --filter 'FullyQualifiedName~DiagnosticNetworkTests.Native\|FullyQualifiedName~NativeConfiguredResolverQuery\|FullyQualifiedName~RealLoopback\|FullyQualifiedName~RealDefaultGateway' --logger trx --results-directory ../artifacts/diagnostics/native` |6 passed,0 failed,0 skipped; genuine configured resolver example.com A query, ICMP echo quality and TTL loopback, read-only routes/interfaces, existing ICMP regression |
| `dotnet list ../Synups.slnx package --vulnerable --include-transitive --format json` | Completed; no vulnerable packages reported for the three projects |
| `dotnet test ../Synups.slnx -c Release --artifacts-path ../artifacts/diagnostics/coverage-build -p:DebugType=portable -p:DebugSymbols=true -p:RestoreLockedMode=true -p:UseSharedCompilation=false -m:1 -nr:false --settings ../eng/coverage.runsettings --collect 'Code Coverage' --logger trx --results-directory ../artifacts/diagnostics/coverage` |222 passed,0 failed,8 skips. New Core unique source lines823/958 =85.91%; not a whole-application coverage claim |
| Root `python3 -m unittest discover -s eng/tests -v` |8 passed; unsafe archive paths/links, namespace mutation guard and retained notices |

Sandbox baseline build failed on Avalonia build-service log access outside the workspace; baseline test IPC was denied. Both were rerun with approved access. A sandbox locked restore was cancelled and then passed with approved network access. Initial compilation found a nullable enumerable comparison error and xUnit cancellation/analyzer issues; these were fixed without weakening requirements.

Gated skips: real ICMP engine, legacy candidate loopback, OS gateway, native diagnostic quality/trace, native snapshot, configured DNS (enabled separately:6 pass); optional native sound and Linux-only desktop launcher remain skipped on this host. Local loopback DNS fixture sockets are genuine socket I/O with explicitly synthetic authoritative responses; these are distinguished from the separately enabled actual configured-resolver query.

## Acceptance boundaries

Headless UI tests cover navigation, passive opening, parameter validation, exact confirmation snapshot, Cancel, Stop independence, owned shutdown, restart/profile/baseline/evidence preview and corrupt profile preservation. They do not establish physical keyboard, native tray, screen-reader, Save-panel or notification delivery acceptance. Engineering apphost checks have been extended with real loopback diagnostics, redacted exports/hashes and restart reopening, but source-specific packaged results are still pending.

SQLite fixtures cover fresh/additive v1 schema, foreign keys, terminal and checkpoint immutability, cross-session result ownership, interrupted checkpoints, retention pinning, baseline requirements and corrupt row errors. Existing regression cases preserve monitoring semantics, capacity/busy/corrupt/backup/error behavior, imported profile compatibility and durable incident semantics. Genuine copied v0.1.1 incident data upgrade, restart and artifact checks will be recorded separately after execution. New diagnostic-specific disk-full/read-only/concurrent corruption/fault-injection variants are not all exercised.

## Platform state

| Platform | Built/tests | Runtime/provider executed | Extracted app/native UI | Physical input |
|---|---|---|---|---|
| macOS arm64 | Release and230 tests pass with8 explicit skips |6 real network/native checks pass | Pending candidate package smoke | Not tested |
| Windows x64 | Candidate CI pending | Candidate CI pending | Candidate CI pending | Not tested |
| Linux x64 | Candidate CI pending | Candidate CI pending | Candidate CI/namespace incident pending | Not tested |
| Linux arm64 | Declared locked build-only target | Not executed | Not executed | Not tested |
| macOS x64 | Declared locked build-only target | Not executed | Not executed | Not tested |

CI links and release hashes: pending a source-specific candidate commit/run/package; historical v0.1.1 evidence is not reused as candidate proof. No competition form or public release action occurred.

## Genuine v0.1.1 copied-data upgrade (pre-freeze source)

Root command: `python3 eng/diagnostics-smoke.py --output artifacts/diagnostics/upgrade-linux-rc4 --dotnet /private/tmp/synups-dotnet/dotnet --previous-data artifacts/presentation/rc4-inspected/linux-x64/incident --query-public-dns`. Passed restore/build/observe/separate reload. Input is a genuine closed v0.1.1 rc.4 CI incident archive, schema1, one incident/comment, one monitoring session/five events. Originals' database/configuration/preferences hashes were unchanged. The copy migrated to2 with verified original-schema backup; both full four-test loopback sessions completed, actual existing incident ID linkage persisted, baseline and comparison survived a second process, JSON/Markdown/PDF hashes verified. Optional real configured-resolver DNS example.com query succeeded. No host network setting was modified and no incident was manufactured. This is Core execution on macOS, not a Linux native test or packaged UI claim.

The report explicitly records a dirty pre-freeze source tree and hashes all Core sources. It does not mislabel the reviewed starting commit as the new implementation. Redacted summaries/checksums are retained under `docs/evidence/verification/diagnostics/`. Raw copied history/network records remain local under artifacts.

PDF QA: pypdf successfully reopened the one-page DNS report and two three-page combined reports. All text included final limitations/page numbers and redaction placeholders. pypdfium2 rendered all3 pages of one combined report to PNG; page1 visually inspected with full margins and no clipping. Remaining pages and large-report stress verification continue with final candidate artifacts.

Root `python3 docs/release/v0.1.1/verify_assets.py artifacts/release-alignment/v0.1.1-rc4`: passed all15 unchanged protected assets, including archive/native/signature/manifest checks. No public mutation was attempted.

## Source freezes and CI follow-up

Application source freezes: `0501106374075ff4df0aec512461ed579ce1b91c` (first full candidate), `6ac4c9c1b8c527ac81f43944bde5fc44ce79579d` (native resolver cap and corrected fixtures), and `e16e3cd4209ec12bf2150a194373c30efcdf744f` (final bounded DNS datagram guard). Later documentation/evidence commits do not change these frozen applications.

Initial CI [39867783924](https://github.com/samscript18/Synups/actions/runs/39867783924): macOS, coverage and both additional builds passed. Windows honestly returned Timeout when the fixture expected Refused inside one second; the test now allows the supported five-second deadline and retains its Refused assertion. Linux packaged app passed all six stages, but the namespace harness's IPv4 listener was addressed via localhost, which selected IPv6; it now uses explicit127.0.0.1 and retains successful TCP/observed loss assertions. Failure logs and exact checksums were preserved; no test was removed or production failure rewritten.

Corrected CI [39868686853](https://github.com/samscript18/Synups/actions/runs/39868686853) at6ac4c9c passed all six jobs, including three native platforms, isolated genuine incident workflow and two build-only additional RIDs. Windows/macOS232 tests:224 passed,0 failed,8 skipped. Linux232:225 passed,0 failed,7 skipped. Each native platform passed6 real network checks and its explicit optional sound-request/fallback test. Sound request success does not establish audible delivery. All three extracted apps passed exercise, restart, externally interrupted process, recovery, genuine read-only storage error and repaired-storage restart.

Final-source CI [39869158253](https://github.com/samscript18/Synups/actions/runs/39869158253) includes oversized DNS datagrams: a real9000-byte UDP fixture must return MalformedResponse without answers. Source records retain the4096-byte parser bound and refuse truncation-prefix acceptance. Final status is recorded in FINAL_REPORT.md and final CI summary evidence; no pending job is counted as passed here.

Final copied-incident command: `python3 eng/diagnostics-smoke.py --output artifacts/diagnostics/e16e3cd-upgrade-linux-rc4 --dotnet /private/tmp/synups-dotnet/dotnet --previous-data artifacts/presentation/rc4-inspected/linux-x64/incident --query-public-dns`. Passed genuine TCP, ICMP statistics, destination TTL trace, network snapshot, existing incident linkage, baseline/comparison, three-format export and separate process restart. Original incident/comment and source configuration/preferences hashes remain unchanged. This copied Linux-produced incident was exercised on macOS, explicitly not claimed as a native Linux run. A second independent copied macOS rc.4 data check at6ac4c9c also passed. Raw archives/topology remain local-only; redacted summaries/checksums are retained.

PDF QA completed with pypdf, pdfplumber and pypdfium2: all7 pages across three real exported reports parse, retain expected footer/page numbering, have character bounds inside the printable area, and render without clipping. A seven-page contact sheet and individual pages were inspected; UTF-8 is retained in JSON/Markdown, while PDF uses declared visible Unicode escapes. These checks are separate from native file-picker/physical printing tests, which are not claimed.

## Final exact-source acceptance (October23,2026 Africa/Lagos)

Final application `e16e3cd4209ec12bf2150a194373c30efcdf744f`, [CI39869158253](https://github.com/samscript18/Synups/actions/runs/39869158253): **all6 jobs passed**. Windows233 total225 passed/0 failed/8 skips; Linux233 total226 passed/0 failed/7 skips; macOS233 total225 passed/0 failed/8 skips. Each native job additionally passed6 opt-in genuine networking checks and1 optional audio-request/fallback check. Windows Microsoft Windows10.0.26100 (Server2025 hosted image), Linux Ubuntu24.04.5 LTS, macOS15.7.9; runtime10.0.13, SDK10.0.402. Local native/upgrade checks used macOS26.3.1 arm64. Physical input is untested everywhere.

All3 native packages passed actual extracted apphost/native UI stages: exercise, restart, controlled external process termination, recovery, real read-only database error and repaired-storage restart. Diagnostic actions used a real loopback listener and production ICMP/TTL/configuration adapters. Linux namespace incident diagnostics retained real loss and successful TCP; literal-target forward DNS stayed Unsupported. Its separate configured-resolver DNS test passed, with no invented answer. Saved linked evidence survived restart and export. Both additional RIDs passed untrimmed self-contained publish, without execution claims.

Final CI coverage:856/993 unique new Core source lines =86.20%. Final downloaded artifact SHA digests, archive CRCs, all package per-file hashes and source/RID/version manifests independently passed. Eleven final redacted JSON/Markdown/PDF bundles passed exact-byte manifest verification. All29 final PDF pages parsed and passed printable-area/footer checks; final Linux incident PDF was rendered and visually inspected. Native screenshot examples, source-specific JSON summaries, original failures, package manifests and checksums are retained under `docs/evidence/verification/diagnostics/`.

CI commands are exactly in `.github/workflows/synups.yml`: locked restore; NuGet audit; zero-warning Release build; deterministic/headless tests; separately enabled networking/audio; real Core diagnostic/restart/export harness; self-contained publish; package integrity; actual apphost/native UI checks; isolated Linux incident; coverage; and two additional build-only targets. The canonical local SDK path is `/private/tmp/synups-dotnet/dotnet`. Candidate artifacts remain development CI outputs; no public release was created.

Final verdict: **CONDITIONAL GO for the isolated core diagnostics alpha**, with the complete31-item scope explicitly unfinished. Detailed limitations and entire feature table: FINAL_REPORT.md. No unresolved failing final CI job remains; broader unexecuted fault/physical/platform scenarios stay documented rather than counted as passes.


## Final hardening follow-up

Source `3c4462a71cbb90032603cd3eaf4366c2561474d7`, [run 39896104073](https://github.com/samscript18/Synups/actions/runs/39896104073): all six jobs passed; 251 total cases, Windows/macOS243 passed+8 gated skips, Linux244 passed+7 gated skips, zero failures; six separately enabled native network tests passed per OS; new Core coverage911/1049 lines=86.84%. Eighteen cases were added after the frozen alpha. See [HARDENING_REPORT.md](HARDENING_REPORT.md) for exact fault/control coverage, real multihop, extracted executables, export audit, package hashes, upgrade preservation and the retained first-run fixture failure.

Commands: `.NET SDK10.0.402 dotnet test Synups.slnx -c Release --no-restore --logger trx` locally; `python3 -m unittest discover -s eng/tests -v` (9 passed); full native commands remain in `.github/workflows/synups.yml` at the frozen source. Real multihop runs `sudo env PATH=... DOTNET_ROOT=... unshare --net -- python eng/path-smoke.py --worker ASSEMBLY --output NEW_DIRECTORY`, only after guard checks. No host routing/firewall/sysctl changes or security bypass are permitted.
