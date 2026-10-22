# Diagnostics verification report

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
