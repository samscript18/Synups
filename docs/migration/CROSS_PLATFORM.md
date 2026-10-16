# Cross-platform evidence and risks

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
