# Cross-platform evidence and risks

Current22 October2026 checkpoint: [reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md), application6c812fde9a29eeb8cfd6399a2f1a75e333e81e03,91.7% inventory/8.5 readiness. Earlier phase sections are historical. The user-selected proposed v0.1.1 payload is now frozen rc.4 at91.7%, with internal0.1.1-rc.4 version disclosed; public0.1.0 remains unchanged. Earlier rc.3 payload decisions below are historical.

Targets are Windows, macOS and Linux. Target intent and successful cross-compilation do not establish runtime support. No downloadable release has been created. Phase 1/2 evidence is historical below; Phase 3 is historical; Phase 4A below is the current verification matrix.

## Toolchain
- Avalonia 12.1.4 stable, verified via official latest release and NuGet index on 2026-10-16: https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.4
- Official template Avalonia.Templates 12.1.4 generated avalonia.app with net10.0; package nuspec explicitly includes net10.0 (and net8.0) assets. Actual restore/build is the compatibility check.
- .NET 10 is LTS: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core. SDK installed for this audit: 10.0.402 osx-arm64. synups/global.json pins this SDK family; new packages have lock files in the working tree. Upgrade serviced SDK/package versions deliberately and revalidate.
- Original WirePeep net48/net6.0-windows remains separate. The new synups/Directory.Build.props does not import legacy Windows targets, signing or analyzer packages. Scoped global.json avoids changing legacy SDK selection.
- .github/workflows/synups.yml configures restore/build/headless tests on three OS runners. This was planned automation in Phase 1; actual Phase 4A run evidence is recorded below.

## Evidence matrix
| Platform | Restore/build | Tests | Native UI / real monitoring |
|---|---|---|---|
| macOS 26.3.1 arm64 | Release compilation verified; see MIGRATION_LOG | 10 automated tests passed, including headless shell | Native process launched and OS window verified. Window screenshot capture failed. No real monitoring implemented. |
| Windows | Not executed | Not executed | Original WPF capture and Synups smoke test pending |
| Linux | Not executed | Not executed | Native display-server/font/graphics validation pending |

## Risks and required checks
- ICMP privileges, firewall policy, IPv4/IPv6, unavailable networking, timeouts and ping API behavior differ. Test explicit error paths; never substitute fake data or require administrator access without evidence.
- Gateway TTL discovery may fail across VPNs, firewalls and routers. Discovering local devices must not block UI startup or silently add unverified targets.
- Linux needs compatible native display/graphics libraries and fonts. Test documented distro/session requirements, X11/Wayland, tray availability and notifications rather than assuming support from framework marketing.
- Windows Registry login launch, WinForms NotifyIcon/balloons, taskbar color, WPF MediaPlayer and Menees window placement cannot be carried over as shared code.
- macOS needs application bundle identity, menu/lifetime decisions, notification permissions and eventual signing/notarization. Native executable launch alone does not verify distribution packaging.
- Local settings directories must follow OS conventions; persistence/import needs validation, atomic writes, recovery and user-visible permission errors.
- Sleep, clock changes, overlapping callbacks and shutdown affect truthfulness of outages and percentages. Await cancellation and represent observation gaps explicitly.
- Screen-reader labels, contrast, keyboard navigation and high DPI must be verified on native OS sessions. Headless control tests do not prove native rendering or accessibility.


## Phase 2 verification and cancellation investigation

Actual runtime in this session: .NET 10.0.13, SDK 10.0.402, macOS 26.3.1 arm64. Avalonia remains 12.1.4; no dependency upgrade was needed.

| Platform | Phase 2 evidence | Limits |
|---|---|---|
| macOS arm64 | Deterministic tests, Avalonia headless tests, opt-in production ICMP loopback integration, native production App/VM start→measure→stop→close harness passed | Loopback IPv4 only; no live external outage/recovery manipulation, native accessibility audit, screenshot or signed distribution validation |
| Windows | Source inspection and CI job configured | No Windows runner or desktop was used; no native ICMP cancellation/runtime claim |
| Linux | Empty ICMP payload selected for .NET unprivileged fallback; CI loopback step configured | No Linux runner/display was used; fallback utility availability, permissions, X11/Wayland and runtime remain unverified |

The .NET [SendPingAsync API](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0) supports a timeout and CancellationToken. Source inspection of the released .NET 10 implementation found that [Ping.cs](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Ping/src/System/Net/NetworkInformation/Ping.cs) registers cancellation and awaits completion; [Windows](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Ping/src/System/Net/NetworkInformation/Ping.Windows.cs) uses finite native ICMP timeouts and observes cancellation in its completion callback. It does not justify an immediate-abort assumption. [Unix raw sockets](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Ping/src/System/Net/NetworkInformation/Ping.RawSocket.cs) pass the token to send/receive and distinguish timeout from cancellation. Unix can also use a ping-utility fallback. The v10.0.13 source URLs could not be fetched during this session, so the source investigation is specifically the v10.0.0 release implementation, not verification of every servicing patch's internals.

Synups always uses a finite 1–5000ms timeout and awaits disposal/completion. Service stop has a 7s deadline. If exceeded, the operation is retained, state stays Stopping, restart is refused and the window reports the problem instead of abandoning a task. A deterministic native-like non-cancellable probe test verifies that behavior. This bounds the UI's stop attempt; it cannot force a wedged OS to release a native operation. The raw-socket/native fallback distinction is why no immediate cancellation guarantee is made.

Real networking was explicitly requested for testing: 127.0.0.1 only, no public IP, subnet scan or device discovery. Native harness observed a real 0ms integer RTT on loopback; that is an actual PingReply measurement, not an unknown value filled with zero. Real system-disconnect/reconnect testing, IPv6 and permission-denied ICMP on native Windows/Linux are outstanding.

New CI has manual dispatch plus separate deterministic and explicitly enabled loopback steps on all three OS runners. These working-tree changes have not been committed or pushed, as instructed, so there is no remote workflow revision against which to execute them. No hosted result is claimed. Use `python3 eng/native-smoke.py --dotnet /path/to/dotnet` in a native macOS GUI session to reproduce the window integration; it creates only temporary loopback configuration.


## Phase 3 — current evidence

Environment: macOS 26.3.1 (25D2128), arm64, SDK 10.0.402, runtime 10.0.13, Avalonia 12.1.4. New direct dependency Microsoft.Data.Sqlite 10.0.13 with SQLitePCLRaw 2.1.13; Linux/Windows native bindings still need runtime proof.

| Gate | macOS arm64 | Windows x64 | Linux x64 |
|---|---|---|---|
| Locked restore / Release compilation | Executed, passed, zero build warnings/errors | No Windows build runner used | No Linux build runner used |
| Automated suite | 70 deterministic/headless passed; one deliberate opt-in skip | Not executed | Not executed |
| Explicit production ICMP test | One loopback test passed separately | Not executed | Not executed |
| Native UI / real measurements | Passed, two one-minute runs plus final two-process rerun | Not executed | Not executed |
| Restart / normal shutdown | Real native process restarts reloaded committed session records; clean Stop/Close passed | Source/tests on Mac only | Source/tests on Mac only |
| Tray | Native exporter/menu initialized; hide on minimize and restore handler passed | Source implementation only; no runtime claim | Hiding disabled; taskbar fallback; actual host unverified |
| Native notifications | Optional adapter implemented, delivery/permission not tested | Native adapter absent; labelled in-app alerts | Optional notify-send adapter; daemon/permission untested |
| Self-contained directory | Published from this Mac | Cross-published from this Mac | Cross-published from this Mac |
| Published executable launch / packaging | Direct apphost launch, bundle identity/signing not verified | Not executed | Not executed |
| Screenshots | Actual native client-area captures reviewed | Upstream WPF illustration only | None |

Self-contained outputs live in ignored `artifacts/publish/{osx-arm64,win-x64,linux-x64}`. Publication is not execution. No release/upload/submission was performed. The native harness references production App/VM/engine/probe and exercises native Avalonia, not Headless; it does not prove the published apphost/package launch.

CI now uses windows-latest (x64), macos-15 (arm64) and ubuntu-24.04 (x64), matching [GitHub runner documentation](https://docs.github.com/en/actions/reference/runners/github-hosted-runners). It retains restore/build/test/loopback/publish/native logs and TRX/captures/artifacts for 30 days, including failures. It publishes the RID matching each actual runner. Linux installs Xvfb and X11/graphics/font dependencies; `xvfb-run` supplies a virtual display for the native X11 test. This does not verify a physical Linux desktop, Wayland, notification daemon or tray host. Avalonia's [Linux deployment prerequisites](https://docs.avaloniaui.net/docs/deployment/linux) include libx11-6, libice6, libsm6 and libfontconfig1; normal .NET/graphics dependencies also apply. No audit/warning suppression or conditional permission-error skip was added.

No final working-tree CI run can be initiated without an authorized commit/push. A previously existing repository commit is not evidence for these subsequent changes. The agent issued no commit/push. Hosted green jobs and their downloaded evidence remain required.

The exact cause of the earlier OS `screencapture -l` failure is not established; no permission entitlement is claimed. Phase 3 uses Avalonia RenderTargetBitmap on the actual visible native window to save the client area. It includes real measured values, not a compositor screenshot or a hand-built mockup. See [capture metadata](../evidence/README.md).

Permission denial, corrupt storage and native adapter failure are covered deterministically. Real native permission denial, external network outages, IPv6, suspend/VPN, long runs, accessibility and OS notification delivery are outstanding. Two one-minute loopback observations cannot establish days-long stability or external Internet health.


`eng/package-artifacts.py` archives published directories as ZIP on Windows and tar.gz on macOS/Linux, with SHA-256 sidecars and simple run instructions. Unix executable permissions are verified inside the tar: raw [upload-artifact loses file modes](https://github.com/actions/upload-artifact#permission-loss). CI uploads these archives with logs/captures rather than a raw Unix directory. Archive integrity/mode verification is separate from executing the apphost. The helper uses Python standard-library APIs compatible with the locally verified Python 3.9.6; CI selects Python 3.12.


Launch-at-login evaluation: **not implemented on any OS**. Windows legacy code writes HKCU Run; a Synups adapter would need an explicit user action and stable installed executable path. macOS needs a validated login-item/bundle identity/permission flow, which these unpackaged outputs do not establish. Linux needs desktop-session autostart/path verification across environments; no adapter was validated. Launch minimized and monitoring-on-launch apply only when Synups is started, and must not be presented as login registration.


## Phase 4A — reviewed source checkpoint and hosted verification

The user explicitly authorized reviewed commit/push for `feat/avalonia-migration` to `https://github.com/samscript18/Synups.git`. Remote metadata confirms the public, non-archived Synups fork; project RepositoryUrl matches. The only initial remote branch was upstream-baseline master. No history rewrite or force-push is needed.

Fresh local validation of the current storage-policy implementation: locked restore passed; Release build passed with 0 warnings/errors (1.36s emitted); 94 deterministic/headless tests passed, with one opt-in networking skip, then the real loopback case passed separately (65ms). Native macOS harness passed two process launches, real ICMP, live UI, minimize/restore, Stop/Close and durable reload; each reopen reports 1 GiB configured/effective capacity. These are framework-dependent native harness runs, not published-apphost launches. Raw local evidence is in ignored `artifacts/phase4a-local/`.

Packaging now requires the managed entry point, dependency/runtime manifests, the platform CoreCLR and native SQLite libraries, and original MIT licence. It includes the exact restored runtime pack's LICENSE.TXT and THIRD-PARTY-NOTICES.TXT under licenses/dotnet. An artifact manifest records RID, CI source SHA, runtime libraries and every published file hash; each archive is checked against those hashes. Unix mode and archive SHA-256 sidecars are retained. Application-specific third-party licensing audit remains a release gate; runtime notices are not claimed to cover every Avalonia/font/native dependency.

At source-checkpoint preparation, no hosted pass is claimed. The new Synups workflow selects Synups.slnx on Windows x64, macOS arm64 and Linux x64, then separately runs tests, explicit loopback, native GUI harness and publishing/packaging. The original Windows-only WirePeep workflow is unchanged and only triggers for master/PRs to master. Published-executable startup and packaging/install/signing verification remain separate from the harness.


### Actual hosted results — checkpoint 88e7ea5

Pushed source: **88e7ea507d82a39238ba48ac97936a2d2fa23c28**, branch **feat/avalonia-migration**. [Run 38937720125, attempt 1](https://github.com/samscript18/Synups/actions/runs/38937720125) completed **success** on 2026-10-16. The authenticated API, completed job/step results and downloaded artifacts/logs were inspected. No failed job or compatibility-warning suppression was needed.

| Actual runner image | Locked restore / Release | Deterministic/headless | Real IPv4 loopback | Native production-composition harness | Self-contained publish/package | Published apphost launch |
|---|---|---|---|---|---|---|
| [Windows Server 2025 10.0.26100 x64](https://github.com/samscript18/Synups/actions/runs/38937720125/job/117097307554) | Pass, 0 warnings/errors | 94 passed; 1 opt-in skip | 1 passed separately | Two real native processes passed UI/ICMP/minimize-hide/restore/Stop/Close/reload | win-x64 passed | Not performed |
| [macOS 15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/38937720125/job/117097307387) | Pass, 0 warnings/errors | 94 passed; 1 opt-in skip | 1 passed separately | Two real native processes passed equivalent flow | osx-arm64 passed | Not performed |
| [Ubuntu 24.04.5 x64](https://github.com/samscript18/Synups/actions/runs/38937720125/job/117097307598) | Pass, 0 warnings/errors | 94 passed; 1 opt-in skip | 1 passed separately | Two native X11 processes passed under Xvfb; window remained visible with no established tray restoration | linux-x64 passed | Not performed |

Each OS exercised all 95 unique cases across the deterministic and opt-in invocations. These are genuine OS runs; the Avalonia headless tests and native harness are separate. Harnesses reference production App/VM/engine/probe but are separate verification entry points, not execution of Synups.Desktop's published apphost. SQLite native binding, real loopback and clean session reload were observed on every OS. Windows/macOS tray exporter initialization and hide/restore handlers worked; physical menu clicks, OS notifications, real permission-denial scenarios, external outages, pending native-ping cancellation and installed-desktop/accessibility/endurance behavior remain outstanding. Xvfb does not certify Wayland or a physical Linux desktop.

Downloaded all three artifact ZIPs and the complete workflow log ZIP into ignored `artifacts/phase4a-local/downloads/`; safely extracted platform evidence into `artifacts/phase4a-ci/`. ZIP digests match the GitHub API; inner archive sidecars, source revision, every published-file hash, original licence text, runtime licence/notices and Unix executable mode 0755 were verified. There are 236 verified published files for Windows and 233 each for macOS/Linux. Overview client captures from all three native runners were visually inspected. Metadata and hashes: [CI evidence manifest](../evidence/verification/2026-10-16-ci-checkpoint.json). Raw artifacts expire after 30 days on GitHub; local copies remain ignored and are not a public release.

The first inspection's byte-equality licence assertion exposed Windows checkout's CRLF conversion, not altered licence text. Text matches the original after newline normalization; repository LICENSE and copyright remain unchanged. A focused `.gitattributes` rule now pins LICENSE to LF so subsequent packages can be compared byte-for-byte across runners. The follow-up commit changes documentation/evidence and licence checkout policy only, without changing application/test source. Verify the follow-up run independently; the evidence matrix above refers specifically to 88e7ea5.


### Follow-up failure and storage-test isolation

[Run 38939047265](https://github.com/samscript18/Synups/actions/runs/38939047265), source **43446f535b29d35701c723dfeedceae698ca8646**, passed macOS/Linux (including native runs and verified exact LF licence bytes), but failed Windows: 93 deterministic/headless passed, one failed, one opt-in skip. `HistoryTests.EngineHistoryCommentsAndIdsSurviveNewServiceAndStore` exceeded the real seven-second worker-drain deadline. The failed TRX shows its 17.47s execution overlapping multiple real SQLite fsync/backup fixtures and the 269,484,032-byte legacy payload case (11.64s). This is observed overlapping I/O and a deadline failure, not proof of a particular OS/antivirus cause. No published Windows package or real-network/native step ran in that failed job.

The corrective test change puts disk-backed History/StoragePolicy/StorageSettings/DesktopPreferences fixtures in one nonparallel xUnit collection. No test is removed or skipped; no production deadline, warning or audit is weakened. Explicit engine concurrency/deadline tests remain, and a new four-concurrent-group/real-SQLite regression verifies distinct durable outages, drained stop/reopen and no false recovery. Local Release test run: **95 passed, one opt-in skip (96 total)**. Hosted results for this correction must be checked independently. Slow physical storage may still exceed the production deadline; the documented ownership/retry behavior remains intentional and endurance testing remains open.


### Corrected checkpoint — 7b75296

[Run 38940757093, attempt 1](https://github.com/samscript18/Synups/actions/runs/38940757093) completed **success** for **7b75296f97ea62d986e7d16b152dedaabcf999d6** on 2026-10-16. This supersedes the failed follow-up's pending status, without deleting its evidence. All three complete artifact ZIPs and full workflow logs were downloaded and inspected.

| Actual OS | Restore / Release | Deterministic + headless | Separate real loopback | Native verifier | Publish / package | Published apphost |
|---|---|---|---|---|---|---|
| [Windows Server 2025 x64](https://github.com/samscript18/Synups/actions/runs/38940757093/job/117107477127) | Pass; 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed | Two processes passed | win-x64 passed | Not launched |
| [macOS 15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/38940757093/job/117107477151) | Pass; 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed | Two processes passed | osx-arm64 passed | Not launched |
| [Ubuntu 24.04.5 x64](https://github.com/samscript18/Synups/actions/runs/38940757093/job/117107477026) | Pass; 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed | Two X11/Xvfb processes passed | linux-x64 passed | Not launched |

All **96 unique cases per OS** were exercised across the two invocations. Native runs observed real loopback, bound UI, minimize/restoration fallback, Stop/Close and durable reload (two clean sessions/four events), with configured/effective 1 GiB SQLite capacity. Exact original LICENSE bytes now match on all OSes; runtime notices, every archived file hash, source revision, sidecars and Unix 0755 mode passed inspection. Three native overview client captures were viewed. [Corrected evidence manifest](../evidence/verification/2026-10-16-ci-corrected.json) records job/artifact IDs, digests, counts and limitations. Raw downloads stay ignored locally; hosted artifacts expire after 30 days. Separate verifier entry points, Linux virtual display and all previously recorded installed-desktop/native-adapter/endurance limitations still apply. The later UI refinement must receive its own verification; these captures describe this exact source snapshot.


### Shared UI checkpoint — a368d83

User-requested UI refinement was separately reviewed, locally verified, committed and pushed as **a368d83dbb5494e283efa02b7e8036a9ce9664c4**. [Run 38944933816, attempt 1](https://github.com/samscript18/Synups/actions/runs/38944933816) completed **success** on 2026-10-16. No monitoring/persistence dependency or algorithm changed.

| Actual OS / job | Locked restore + Release | Deterministic / headless | Real ICMP loopback | Native UI / restart | Publish + verified package | Published executable |
|---|---|---|---|---|---|---|
| [Windows Server 2025 x64](https://github.com/samscript18/Synups/actions/runs/38944933816/job/117121549576) | Pass, 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed separately | Two processes passed | win-x64 passed | Not launched |
| [macOS 15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/38944933816/job/117121549911) | Pass, 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed separately | Two processes passed | osx-arm64 passed | Not launched |
| [Ubuntu 24.04.5 x64](https://github.com/samscript18/Synups/actions/runs/38944933816/job/117121549893) | Pass, 0 warnings/errors | 95 passed, 1 opt-in skip | 1 passed separately | Two X11/Xvfb processes passed | linux-x64 passed | Not launched |

Downloaded all three complete artifact ZIPs and full workflow log ZIP. Checked TRX, native logs, all published file hashes, sidecars, exact original MIT bytes, runtime notices, source revision and Unix0755 modes. The native flow still passes real measurements, minimize/restore or safe Linux fallback, Stop/Close and clean durable reload. Viewed normal and minimum820×560 client captures on each OS: compact navigation remains usable, long content scrolls, and metrics are actual loopback-derived values. Retained selected genuine captures and [UI CI evidence/hashes](../evidence/verification/2026-10-16-ci-ui.json) in source. No native accessibility, OS notification-delivery or installed-desktop certification is inferred from them. Published-apphost, Wayland, external outages/IPv6/permissions, endurance and distribution/signing limits remain unchanged. The final evidence-only commit changes documentation/captures, with no production/test/workflow/package-tool changes; its exact subsequent run can be inspected independently from this verified UI source.


## Phase 4B apphost protocol — verification pending

The new workflow launches the extracted self-contained Synups.exe (Windows), Synups.app/Contents/MacOS/Synups (macOS) and Synups (Linux/Xvfb), with normal application composition. It validates package/file hashes, RID/CPU, native SQLite and bundled CoreLib location before accepting a pass. Six isolated process stages cover CRUD, real loopback, stop/restart, interruption, read-only SQLite and repair. Actual process exit, native platform handle, UTC evidence, captures and database integrity are retained. Programmatic controls and rendered client-area captures are not physical desktop input or full-desktop screenshots.

Local macOS checks have exercised the new apphost; Windows/Linux execution for this source is pending the new CI run. The three-OS Phase 4A matrix above remains valid only for its recorded source and component harness. Packages now include a Mac app bundle (ad-hoc integrity only, no Developer ID/notarization), Windows executable icon and Linux manual desktop-entry example. See [launch instructions](../release/INSTALLATION.md) and [candidate notes](../release/RELEASE_NOTES.md). CI artifacts remain temporary downloads, not a public release.


### Actual first Phase 4B results and signature follow-up

Source6c3d9a8fd5879103ecc92a41679a254bb4389f5d passed [push run 39674076801](https://github.com/samscript18/Synups/actions/runs/39674076801) on Windows Server 2025 x64, macOS 15.7.9 arm64 and Ubuntu 24.04.5 x64/Xvfb. Each launched the real extracted apphost and passed all six stages,105 deterministic/headless plus1 explicit loopback case and4 archive/licence cases. Independent downloaded artifact inspection passed all hashes/architectures/licences/notices and recorded native SQLite/runtime reload. Actual Windows termination code1 and Unix-9 are deliberate interruption, not ordinary exit failures; five normal stages returned0.

This run is not the final packaging gate: downloaded Mac bundle strict validation subsequently failed on a managed DLL after archive extraction. Its successful execution is retained as evidence but does not erase the signature defect. The corrected resource layout passes local six-stage launch and extracted strict validation; a new exact-source CI run will supersede the packaging result. Gatekeeper still rejects ad-hoc identity, and no security bypass is performed.


## Final Phase 4B published-executable evidence — 292b847

**Source292b847bffa96d2a23d771c3ba0ce4648c06844b**, branch feat/avalonia-migration, [run 39678991269](https://github.com/samscript18/Synups/actions/runs/39678991269), attempt1, completed success. This supersedes earlier Phase 4B pending/failing source results, which remain in the migration log. [Downloaded verification manifest](../evidence/verification/2026-10-21-published-apphosts.json) retains exact API artifact digests, archive/apphost/file/capture/TRX hashes, native state and UTC timestamps.

| Actual OS / native job | Locked restore / Release | Tests | Actual extracted apphost | Separate component harness |
|---|---|---|---|---|
| [Windows Server 2025 x64](https://github.com/samscript18/Synups/actions/runs/39678991269/job/119496207992) | Pass,0 warnings/errors |105 deterministic/headless +1 separately enabled real loopback +4 Python cases | Six stages passed; five normal exits0, deliberate termination1 | Two processes passed |
| [macOS 15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/39678991269/job/119496207520) | Pass,0 warnings/errors | Same | Six stages passed; five normal exits0, deliberate termination-9; strict extracted signature pass | Two processes passed |
| [Ubuntu 24.04.5 x64 / Xvfb](https://github.com/samscript18/Synups/actions/runs/39678991269/job/119496207880) | Pass,0 warnings/errors | Same | Six stages passed; five normal exits0, deliberate termination-9 | Two X11 processes passed |

All 106 unique .NET cases were exercised per OS across separate invocations; the default suite intentionally skips its opt-in network case. Actual apphosts validated CRUD and invalid input, stable IDs across restart, real loopback and bound UI, worker drain, preferences, secondary-instance restoration, Cancel/Continue exit, interrupted-session reload without false recovery, actual OS read-only SQLite error and repair. Each finished with 5 durable sessions/1 interrupted and no fabricated outage/recovery. Native save-picker/physical tray clicks and OS alert delivery are not automated.

Independent full archive audit passed Windows277 files/25 runtime-package notice entries, macOS322/24, Linux275/24; exact MIT bytes, all manifests/sidecars, native apphost+SQLite architectures and Unix executable modes match. Mac managed assemblies are resources, and the supported SDK-bound apphost uses their relative location. Strict/deep validation of the actual extracted CI bundle passes on local macOS 26.3.1; LaunchServices opens that exact downloaded bundle and verifies monitoring/restoration/clean close. **Gatekeeper assessment rejects it (exit3)**. There is no Developer ID/notarization or trusted-download claim. Windows remains unsigned; Linux has a portable directory and optional desktop-entry example, not an installer.

Actual client-area images were inspected and retained in the [before/after evidence](../evidence/README.md). Linux Xvfb is not Wayland/physical desktop support. Windows Server is not every consumer Windows version. Source-inspected adapters, native notification delivery/denial, physical menu clicks, broad permissions/IPv6/VPN/suspend/high-DPI/screens/readers and representative endurance remain separate gates. Packages/notices/checksums and [launch instructions](../release/INSTALLATION.md) are prepared, not publicly released. CI artifacts expire after 30days.

Two local five-minute actual GUI processes at 3cf272037a913bdc7a696d3414e3dbd9c7bf23a9 passed. Primary-process peak RSS 232.7/219.9MiB; CPU5.57%/3.25% of one core averaged including startup; descriptors 153 after warm-up through final samples; DB 40,960bytes. Warm-up-to-final RSS increased6.23/6.27MiB; samples and limits are [retained](../evidence/verification/2026-10-21-gui-resources-macos-arm64.json). This preceding-source measurement does not certify the later restoration change or establish a memory-leak-free/multi-hour GUI result.


### Completed two-hour Core observation — local macOS arm64

From2026-10-21T19:50:02.840361Z to21:50:04.319557Z, requested7,200s observation /7,201.54s total including restart. Exit0, independent SQLite quick_check=ok, two clean sessions. Four real groups polling127.0.0.1 at1s,8MiB history cap.28,792 actual probes,0 failures/errors; sampled measurement age≤1.020s. Stop12.4ms; workers/probes/pending evidence fully drained before restart/reopen. Main DB40,960bytes throughout. CPU77.03s /1.07% average of one core; RSS70.55→55.06MiB, peak85.48MiB; descriptors101→104, peak105.

[Summary, exact loaded engine SHA and source qualification](../evidence/verification/2026-10-21-engine-endurance-macos-arm64.json), [720 raw resource samples](../evidence/verification/2026-10-21-engine-endurance-samples.jsonl). Built from the earlier uncommitted Phase4B Core source subsequently checkpointed at2aff56a and unchanged through292b847; this is not claimed as an exact-commit CI binary or published-GUI endurance. Healthy loopback, mixed development host, no external outage or physical power/disk failure. Sampled trends are not proof of no leak. Windows/Linux multi-hour and representative large-history GUI observations remain pending.


## Phase 5 frozen release candidate — 2973d33

Selected application revision **2973d3349fcb5af5639597920842288e6549bbd0**, [successful run39682197118](https://github.com/samscript18/Synups/actions/runs/39682197118). All three full artifact containers were fetched through the authenticated API; outer API SHA-256, ZIP integrity, inner sidecars, every packaged file, native CPU/RID/runtime/SQLite, original MIT, retained notices and Unix execute bits passed independent audit. New hashes differ from292b847 despite no production-source change; bundled README/build metadata are revision-specific. [Exact candidate record](../evidence/verification/2026-10-21-release-candidate.json), [download index](../release/VERIFIED_ARTIFACTS.md).

| Actual native runner | Build / tests | Real published apphost |
|---|---|---|
| [Windows Server2025 x64](https://github.com/samscript18/Synups/actions/runs/39682197118/job/119507213765) | Locked restore/audit,0-warning Release;105 deterministic/headless +1 separately enabled real-loopback;4 Python | Six stages pass;5 normal exits0, deliberate kill1;5 durable sessions/1 interrupted |
| [macOS15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/39682197118/job/119507213700) | Same | Six stages pass;5 normal exits0, deliberate kill-9; extracted strict/deep signature pass |
| [Ubuntu24.04.5 x64/Xvfb](https://github.com/samscript18/Synups/actions/runs/39682197118/job/119507213388) | Same | Six stages pass;5 normal exits0, deliberate kill-9; native SQLite and restart/repair pass |

Local macOS26.3.1 re-ran the exact downloaded2973d33 package: six stages passed22:11:01–22:11:33UTC on2026-10-21; real loopback, CRUD/configuration/history reload, stop/drain, secondary-instance restoration, exit confirmation, interrupted session and actual read-only SQLite repair. LaunchServices independently opened that same bundle and completed exercise/clean exit0. These checks use actual programmatic controls; native save-picker service export remains distinct. No real-world outage was staged. Existing qualified two-hour Core and preceding-source ten-minute GUI measurements are preserved and not relabelled as this revision's endurance.

Read-only local identity check returned0 valid code-signing identities. Gatekeeper assessment rejects the2973d33 bundle (exit3), despite strict integrity passing. No Developer ID/notarization or browser-quarantine approval is claimed. Existing Accessibility permission is false; physical keyboard/tray/dialog/notifications could not be exercised here without changing permissions, which was not done. Windows consumer desktops, Linux physical/Wayland sessions and broad accessibility remain pending. Final packages are prepared locally, with no permanent anonymous download until separately authorized Release publication. Later documentation commits do not change the candidate package source SHA.


## Published v0.1.0 distribution evidence

User-authorized [Synups 0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0), published `2026-10-21T22:34:54Z`. Tag/application source `2973d3349fcb5af5639597920842288e6549bbd0`; no rebuilt/replaced binaries. All 15 uploaded files matched GitHub server SHA-256; independent unauthenticated public downloads all returned HTTP 200 and matched reviewed hashes. ZIP and tar/gzip integrity passed. [Exact release/tag/asset URLs, hashes and timestamps](../evidence/verification/synups-0.1.0-publication.json). README uses permanent Release links; CI expiration/login is no longer the download gate. Source-matched existing native CI/runtime results above remain unchanged. Download/hash verification does not establish ordinary desktop acceptance: Windows is unsigned, Mac Gatekeeper still rejects ad-hoc identity, and Linux physical/Wayland desktops remain unverified. No competition submission.


## Competitive candidate: verified incident and onboarding — 22 October 2026

The unchanged public [v0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0) remains the rollback baseline. Unpublished candidate **0.1.1-rc.1**, application source `535b91f18e2d98714d703364b9e3e3631cdad992`, passed [run 39754244507](https://github.com/samscript18/Synups/actions/runs/39754244507). Each native OS job passed locked restore/audit, zero-warning Release, **107 deterministic/headless + 1 separately enabled real-loopback + 8 Python safety cases**, and six actual extracted-apphost stages. Default .NET invocation intentionally skips its opt-in network case. Independent downloaded audits verified outer API hashes/ZIP integrity, inner sidecars/every packaged file, CPU/RID/apphost/native SQLite, exact MIT, notices and Unix modes. [Machine-readable evidence](../evidence/verification/2026-10-22-competitive-candidate.json), [retained CI logs](../evidence/verification/2026-10-22-competitive-ci-logs.zip).

| Actual runner | Actual packaged executable | Additional scoped evidence |
|---|---|---|
| [Windows Server 2025 x64](https://github.com/samscript18/Synups/actions/runs/39754244507/job/119742115768) | Six stages; five normal exits 0; deliberate interruption 1 | Fresh-profile one-action setup saves without probing |
| [macOS 15.7.9 arm64](https://github.com/samscript18/Synups/actions/runs/39754244507/job/119742115959) | Six stages; five normal exits 0; deliberate interruption -9 | Exact downloaded package reran on macOS 26.3.1; strict integrity passes; Gatekeeper still rejects (3) |
| [Ubuntu 24.04.5 x64/Xvfb](https://github.com/samscript18/Synups/actions/runs/39754244507/job/119742116397) | Six stages; five normal exits 0; deliberate interruption -9 | Separate namespace-confined real ICMP loss/recovery and second-process reload passed |

The actual Linux incident is a production IcmpProbe/MonitoringService **source-component native verifier**, separately distinguished from the six packaged-apphost stages. Incoming loopback replies were dropped by an isolated namespace-local kernel rule, then restored. First failure 09:19:49.8702205Z, confirmation 09:19:52.1138880Z after 2s grace, recovery 09:19:52.6619422Z; confirmed duration 0.5480901s. Exactly one incident/recovery, stable ID/comment/events after separate-process reload; namespace/capture hashes, SQLite quick_check and actual CSV independently checked. [Full protocol/raw records/screenshots](../competition/REAL_INCIDENT.md). This is not an Internet/provider outage, fake probe result, physical network change or OS notification-delivery test. Both in-app transition banners are visibly captured.

Local genuine macOS desktop acceptance remains partial: normal published 0.1.0 LaunchServices open, owned native window, genuine OS-window screenshot, passive zero-session startup and native OS quit/process disappearance were observed. Accessibility permission is false; physical tray/menu, keyboard-only traversal, native CSV save picker, physical resize/minimize/restore and close-with-active-work could not be certified. No permission/security setting was changed. Programmatic controls do not earn physical-interaction credit.

Legacy inventory stays **72.2% = (9 + 8×0.5)/18**, before and after this work. Three largest bounded gaps addressed: first-run friction, missing genuine controlled incident evidence, and hard-to-find migration achievements. Native notification delivery and trusted ordinary-user distribution remain open. No discovery implementation was added without native three-OS proof. [Judge’s guide](../competition/JUDGES_GUIDE.md), [competitor source audit](../competition/COMPETITIVE_AUDIT.md). No new Release/tag or competition submission.


## Final competitive candidate — 65d917a

Frozen application source **`65d917ac645139836cf97e48c48c137a09825942`**, [successful run 39761184906](https://github.com/samscript18/Synups/actions/runs/39761184906). This supersedes preceding candidate binaries for adoption; public v0.1.0 remains untouched. Native jobs: [Windows](https://github.com/samscript18/Synups/actions/runs/39761184906/job/119765466486), [macOS](https://github.com/samscript18/Synups/actions/runs/39761184906/job/119765466786), [Linux/Xvfb](https://github.com/samscript18/Synups/actions/runs/39761184906/job/119765466694). Each passed locked restore/audit, zero-warning Release, 107 deterministic/headless plus 1 separately enabled real-loopback case, 8 Python safety tests and six actual extracted-apphost stages. All three first-run viewport assertions and independently reviewed final images pass. Windows fresh-window clipping is resolved by normalizing unsaved geometry to actual native client dimensions through the existing safe-placement policy; the earlier failing source/assertion remains recorded.

Downloaded final packages passed outer/inner/all-file SHA-256, archive integrity, RID/CPU/runtime/native SQLite, original MIT, dependency-notice and executable-mode checks. Exact final Mac package also passed all six stages locally on macOS 26.3.1 (10:21:56–10:22:32Z), five ordinary exits 0 and one deliberate -9 interruption, strict/deep integrity 0. Gatekeeper still rejects (3); no trusted publisher, notarization or physical-input pass is inferred. [Final full evidence](../evidence/verification/2026-10-22-competitive-candidate-final.json), [retained logs](../evidence/verification/2026-10-22-competitive-ci-logs-final.zip), [candidate hashes/notes](../release/COMPETITIVE_CANDIDATE.md).

Linux repeated the actual isolated incident: failure 10:13:54.9720378Z → confirmation 10:13:57.2113907Z → recovery 10:13:57.7604230Z, monotonic confirmed duration 0.5490790s. One outage, one recovery, stable ID/comment/events after process restart; actual CSV and SQLite integrity independently checked. This remains source-component/native UI evidence distinct from actual packaged-apphost tests and from physical desktop interaction. Both initial successful and final raw records are retained in the [protocol](../competition/REAL_INCIDENT.md).

Mandatory requirements status: **Mac physical desktop acceptance partial/blocked** by existing disabled Accessibility; normal published launch/window/OS capture/quit and programmatic packaged checks passed, physical tray/keyboard/native Save dialog/resize/active-close did not. **First-run implementation complete and regression/native-apphost checked**; no human one-minute completion-time claim. **Main README migration visibility complete**, with original/rewrite/improvement comparison, exact stable/candidate counts and links, genuine before/after, gaps and signing limits. No new dependency, monitoring-engine rewrite, production mock data, discovery claim or OS notification-delivery claim. Parity remains 72.2%; readiness remains 8.5/10 under the existing rubric. No new Release/tag or competition submission.


## Final hardening — verified rc.2, 22 October 2026

Frozen application source **6446990571c285b5c2e2a4bb293cbd8e3af40171**, [successful native three-OS/coverage run](https://github.com/samscript18/Synups/actions/runs/39782799326), [independent retained evidence](../evidence/verification/2026-10-22-hardening-final.json), [candidate hashes/scope](../release/FINAL_HARDENING_CANDIDATE.md). Later docs do not change these binaries; no candidate release/submission was performed.

| Actual environment | Restore / Release | Deterministic/headless | Separate loopback + native route | Actual extracted apphost |
|---|---|---|---|---|
| Windows Server2025 x64 | Locked/audit passed; 0 warnings/errors |126 passed, 2 intentional opt-in skips|2 passed|6 stages;5 normal0, intentional interruption1|
| macOS15.7.9 arm64 | Same |126 passed,2 opt-in skips|2 passed|6 stages;5 normal0, interruption-9|
| Ubuntu24.04.5 x64 / X11 Xvfb | Same |126 passed,2 opt-in skips|2 passed|6 stages;5 normal0, interruption-9|

Each OS also passes8 Python safety cases. Actual native route checks use read-only route facilities; no discovery pings or configuration edits. The apphost preview verifies no automatic target creation or measurement; deterministic/UI cases verify confirmation, stable IDs, duplicates and cancellation. Clipboard, routed F1, SQLite reload, bounded stop/restart, preferences, second-instance restoration and actual read-only storage repair pass. This is programmatic real-control automation, not physical clicks/keystrokes/native Save-panel completion. Full manifests, every archive/file hash, CPU/native SQLite/runtime, notices/licences and Unix modes were independently inspected.

Local macOS26.3.1 repeats the downloaded exact package's six stages, then normal LaunchServices launch→owned window/OS screenshot→native quit on an independent fresh stopped profile (zero sessions). No observable LaunchServices application exit code is claimed. AX and event-post permission remain false; physical acceptance is blocked with [manual actions](../release/MACOS_ACCEPTANCE.md). Strict/deep ad-hoc integrity passes; Gatekeeper rejects3. Windows unsigned; Linux X11/Xvfb does not certify Wayland/absent tray hosts.

The same-source Linux namespace incident passes genuine ICMP grace/confirmation/recovery, new timeline, stable ID/comment and CSV after restart. It verifies native clipboard details and opted-in window restoration, with2 focus requests. This source-component native verifier is separate from the actual packaged apphost suite. [Protocol/raw records](../competition/REAL_INCIDENT.md#final-hardening-timeline-and-focus-verification).

CodeQL [run39782799390](https://github.com/samscript18/Synups/actions/runs/39782799390) retained zero SARIF findings; coverage job reports2127/2638 lines and1435/2116 branches, not physical desktop coverage. Initial discovery run39767319536 failed Windows missing-IPv6 query semantics; fixed by enumerating ActiveStore then filtering, preserving actual errors. Run39780986452 subsequently failed a raw-file corruption-test read under Windows sharing; corrected test FileShare flags without altering production storage or byte-equality checks. Failed logs remain recorded; neither failure was skipped/suppressed. Final run passed all jobs.

## Legacy completion rc.3 — actual final verification, 22 October 2026

Frozen application **7ca85cccce6e3609d08d64e4f23172fa14fffb25**: [run 39821153798](https://github.com/samscript18/Synups/actions/runs/39821153798), all native/coverage jobs succeeded. No result is borrowed from rc.2. [Independent package/TRX/launch/hash evidence](../evidence/verification/2026-10-22-legacy-completion-final.json), [retained reports](../evidence/verification/2026-10-22-legacy-completion-reports.zip), [CI logs](../evidence/verification/2026-10-22-legacy-completion-ci-logs.zip), [frozen candidate/hash table](../release/LEGACY_COMPLETION_CANDIDATE.md).

| Actual environment | Locked restore / Release | Default tests / separate opt-in | Actual extracted executable |
|---|---|---|---|
|Windows Server 2025 x64|Passed;0 warnings/errors|143 passed/4 intentional skips;3 real/native passed;8 Python passed|Six stages; five normal exits0, one deliberate kill1|
|macOS 15 arm64|Passed;0 warnings/errors|143 passed/4 intentional skips;3 real/native passed;8 Python passed|Six stages; five normal exits0, deliberate kill-9|
|Ubuntu 24.04 x64, X11/Xvfb+Openbox|Passed;0 warnings/errors|144 passed/3 opt-in skips;3 real/native passed;8 Python passed|Six stages; five normal exits0, deliberate kill-9|

All 147 .NET cases are exercised on Linux across default/opt-in invocations. The Gio autostart launcher case is Linux-only, intentionally skipped on Windows/macOS. New checks cover initial `/Minimize`/`--minimized`, actual minimized/restored window, second-instance restoration even with minimize arguments, native clipboard row/value and unchanged loopback/SQLite/interruption/permission repair. These are actual apphosts and real controls, not physical input, native Save-panel or tray-menu certification. Linux now starts Openbox, waits for EWMH readiness, and preserves the real minimized-state assertion; bare Xvfb alone cannot implement window-manager behavior. Wayland/absent physical tray hosts remain separate.

All three artifact API digests, ZIP CRC, every inner manifest file/set/hash, archive sidecars, RID/CPU/runtime/native SQLite/licence/notices and Unix executable modes independently passed. Archives are unchanged CI bytes. No native login/startup settings were altered: Mac/Linux temporary registration files and native Linux Gio argument execution were tested; Windows command planning is tested but registry mutation and actual next-login execution are not certified. Supported paths with spaces/& work on Linux; problematic Exec quoting characters are refused explicitly. Native route queries pass; Windows cold startup requires finite 15s instead of8s. Original candidate checks retain explicit address lists,50ms waits, no scan or network changes; no actual modem/transceiver response is claimed.

Same downloaded Mac package repeated all six stages locally. Normal LaunchServices launch at17:28:28Z showed an owned native window and zero fresh-profile sessions, and exited on native OS quit; exit code is unobservable through LaunchServices. Native OS capture succeeded. Read-only permissions remain Accessibility=false/event posting=false/screen capture=true. Physical tray, native CSV Save, actual keyboard traversal/minimize/resize/pending-write close remain blocked; [operator checklist](../release/MACOS_ACCEPTANCE.md). Strict/deep Mac signature integrity passes; Gatekeeper rejects(exit 3). No Developer ID/notarization, security bypass or Windows publisher signing. No sound/OS notification delivery claims were added.

Same-source isolated Linux ICMP failure/recovery, stable incident/comment/CSV reload and old rc.2 logical-data upgrade also pass; component incident UI is clearly separate from packaged execution. Upgrade preserves every SQL record and configuration/preferences bytes, not SQLite header bytes. Earlier endurance/history-scale evidence retains its earlier source. CodeQL run 39821182932 has zero downloaded SARIF results; scoped coverage 80.81% lines/67.34% branches. [Failures39816823353/39818020120/39819866935 and fixes](../release/LEGACY_COMPLETION_CANDIDATE.md) are retained, not suppressed. Public 0.1.0/rc.1/rc.2 archives and MIT ancestry remain unchanged.


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
