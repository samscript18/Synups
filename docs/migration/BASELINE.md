# WirePeep baseline

Audited 2026-10-16 on macOS 26.3.1 arm64. Working tree initially clean. IDE supplied an AGENTS.md tab, but no AGENTS.md exists on disk in this repository.

Upstream: https://github.com/menees/WirePeep
Fork remote: https://github.com/samscript18/Synups.git
Baseline / upstream master SHA (GitHub API independently matched): `61ae14391151ca9f209223e62c98fae3df5482b8` (Bill Menees, 2024-06-01, Fix whitespace).
Existing Git ancestry retained; no history rewriting, renames or deletions. MIT LICENSE, copyright (c) 2020 Bill Menees retained verbatim.

## Observed source, not runtime claims
Two projects, both inherit net48 and net6.0-windows. WPF executable additionally uses WinForms and Menees.Windows.Presentation 5.1.2; Common uses Menees.Common 5.1.2 and Nullable 1.3.1. Shared props add signing, analyzers and System.Resources.Extensions 6.0.0. Language version 12 is requested while existing CI installs .NET 6: a toolchain mismatch to validate on Windows.

Entry: App.xaml/App.xaml.cs creates a mutex by executable path, loads MainWindow and supports /Minimize. App exit saves settings and disposes UI resources. No generic host, DI container or dedicated ViewModel classes. StatusRow and LogRow are observable presentation models; MainWindow is the service/presentation coordinator.

One-second System.Threading.Timer uses Interlocked to avoid overlapping callbacks, invokes StateManager.Update, then Dispatcher.BeginInvoke. Group updates use Parallel.ForEach; each group rotates literal-IP targets and stops on success. Ping.Send is synchronous. No cancellation token or awaited timer shutdown. Snapshot copies are shallow; profile edits and active updates deserve concurrency testing.

## Problems visible in source
- Environment.ProcessorCount / 2 can produce zero MaxDegreeOfParallelism on a one-core environment.
- Timer.Dispose does not wait for an in-flight update; shutdown logging/settings may race with callbacks.
- StateManager subscribes to profile collections without unsubscribe/disposal.
- DateTime.UtcNow drives elapsed thresholds; wall-clock changes and suspend gaps are not modeled.
- Unavailable resets IsFailed, potentially representing a local probe error as outage end.
- Default profile construction performs synchronous network discovery and re-adds defaults when saved locations are empty.
- History resides in WPF memory; text logs are written/exported but no history rehydration was found.
- Diagnostic simulation is accessible in release via Ctrl+Alt+Shift menu reveal. Synups will not carry a production simulator.
- No tests or test project found. Existing workflow builds/publishes on windows-2022 for master only; does not run tests.

## Screenshots
Original README points to http://www.menees.com/Images/WirePeep.png. This is an upstream illustration, not a screenshot captured during this audit. No authentic local original capture: WPF cannot launch on macOS. Obtain a Windows capture at the recorded SHA for submission. No before/after screenshots are fabricated.

## Execution evidence
Initially `dotnet --info` failed: dotnet not on PATH. Installed Microsoft SDK 10.0.402 to /tmp/synups-dotnet, with CLI home/package cache under /tmp. These temporary paths are environment-specific and not committed.

| Command | Actual result |
|---|---|
| `dotnet restore WirePeep.sln` | Failed NETSDK1100 on macOS; Windows targeting must be enabled. NETSDK1138 warns net6.0-windows is out of support. |
| `dotnet build WirePeep.sln -p:EnableWindowsTargeting=true` | Restore and compilation succeeded for both projects and both frameworks, 0 errors, 2 NETSDK1138 warnings; elapsed build output 26.89 seconds. No source changes required. |
| `dotnet test WirePeep.sln --no-restore -p:EnableWindowsTargeting=true` | Exit 0; only build output, no tests discovered/executed because no test projects exist. This is not a passing test suite. |
| Original UI launch | Not attempted: Windows WPF runtime cannot run on macOS. No runtime feature behavior or original UI screenshot verified. |

A first sandboxed restore stalled; network-enabled retry supplied the actual baseline above. The SDK 10 compiler satisfies the legacy C# 12 request; existing .NET 6 CI is still a separate toolchain concern, not a build failure observed here.

Original LICENSE SHA-256: `68f2ae1659c084bc448af6d450e8ba1149a34ae990f6183c421fd695b20d548e`.
Original DefaultProfile.xml SHA-256: `d231ee7ff92be2dddb13cde37b23d90a09b63a8fa10592a736caa09afa5563b8`.


## Original Windows execution — 22 October 2026

The earlier unavailable-WPF statements are historical. [Run39841203591](https://github.com/samscript18/Synups/actions/runs/39841203591) builds untouched upstream61ae14391151ca9f209223e62c98fae3df5482b8 for net48 and runs it under actual Windows/.NET Framework4.8. The populated main-window capture uses native PrintWindow and was independently inspected. Application-scoped outbound blocking confines startup probes to the disposable test environment; failed rows are not an ISP outage. Normal WM_CLOSE exited0. This proves original build/launch, not full original functional certification. The first runner lacked the empty standard Run key and displayed an error dialog; the harness now creates/removes that empty key without installing a startup entry. An initial too-early capture and settings-search failure were retained in CI history. A genuine Menees5.1.2 .stgx fixture was generated by the original executable in run39840176062; it is test-only and unmodified.

[Exact source, image hash and raw-report references](../evidence/verification/2026-10-22-presentation.json).
