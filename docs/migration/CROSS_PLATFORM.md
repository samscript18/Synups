# Cross-platform evidence and risks

Targets are Windows, macOS and Linux. Target intent and successful cross-compilation do not establish runtime support. No downloadable release artifacts have been produced. Phase 1 evidence is retained below; Phase 2 results follow.

## Toolchain
- Avalonia 12.1.4 stable, verified via official latest release and NuGet index on 2026-10-16: https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.4
- Official template Avalonia.Templates 12.1.4 generated avalonia.app with net10.0; package nuspec explicitly includes net10.0 (and net8.0) assets. Actual restore/build is the compatibility check.
- .NET 10 is LTS: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core. SDK installed for this audit: 10.0.402 osx-arm64. synups/global.json pins this SDK family; new packages have lock files in the working tree. Upgrade serviced SDK/package versions deliberately and revalidate.
- Original WirePeep net48/net6.0-windows remains separate. The new synups/Directory.Build.props does not import legacy Windows targets, signing or analyzer packages. Scoped global.json avoids changing legacy SDK selection.
- .github/workflows/synups.yml configures restore/build/headless tests on three OS runners. This is planned automation; no remote workflow results have been observed.

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
