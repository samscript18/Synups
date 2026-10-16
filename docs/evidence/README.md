# Before / after evidence

Capture/verification date: 2026-10-16. These images use different systems and observation periods and are not a connectivity benchmark.

| Asset | Provenance and limits |
|---|---|
| [Before: WirePeep](before/wirepeep-upstream.png) | Downloaded unchanged from [Bill Menees' original published illustration](https://www.menees.com/Images/WirePeep.png), linked by the original README. Copyright Bill Menees; retained original MIT attribution. Its visible dates are June 2020. This is not a freshly captured baseline run or proof of this session's Windows execution. |
| [After: overview](after/macos-arm64/synups-overview.png) | Synups Release, Avalonia 12.1.4 / .NET 10.0.13, macOS 26.3.1 arm64. Actual visible native window client tree rendered with Avalonia RenderTargetBitmap, no OS chrome. Explicit temporary 127.0.0.1 configuration, live real ICMP and one connected group. |
| [After: targets](after/macos-arm64/synups-targets.png) | Same native process/profile, scrolled to the actual target. A successful loopback integer RTT of 0ms is a real PingReply, not a substituted unknown. |

Final captures show local time 2026-10-16 17:17:52 +01:00. They were inspected after generation. No screenshot metrics, device rows or outages were invented; no image-generation/editing tool was used. No real outage was induced for imagery. The earlier OS screencapture failure remains unexplained; this capture method does not claim Screen Recording permission or a compositor/desktop screenshot.

Reproduce in a native GUI session:

```sh
python3 eng/native-smoke.py --dotnet /path/to/dotnet --screenshots artifacts/screenshots --repeat 2
```

The harness creates temporary configuration/history/logs, probes only explicitly configured loopback, verifies bound values, minimizes/restores, stops and closes, then reloads durable sessions in a separate process launch. It is excluded from production. Captures are current working-tree implementation evidence, not a signed/published package screenshot. Source/capture hashes and actual results: [verification manifest](verification/2026-10-16-macos-arm64.json). Raw local command logs/TRX are under ignored `artifacts/phase3-final/`; these local files are not hosted CI evidence.

An original WirePeep Windows runtime capture remains outstanding. Phase 4A obtained genuine Synups native client captures on Windows Server 2025 x64, macOS 15.7.9 arm64 and Ubuntu 24.04.5 x64 under Xvfb. The corrected checkpoint captures were downloaded and viewed; source revision, PNG hashes and job/artifact links are in the [three-OS verification manifest](verification/2026-10-16-ci-corrected.json). Raw images remain in the run artifacts/local ignored downloads. They show explicit loopback in a production-composition verifier, not the published executable or OS desktop chrome. Use the [platform matrix](../migration/CROSS_PLATFORM.md) when making support claims.


Storage-capacity follow-up (same date): the original captures/manifest above are preserved as the Phase 3 baseline. [Storage-policy verification](verification/2026-10-16-storage-policy-macos-arm64.json) records the new source, 94 deterministic/headless passes plus separate loopback pass, and two one-minute native launches with 1 GiB page-limit checks. Actual overview/targets/Settings client captures and logs/TRX remain locally in ignored `artifacts/storage-verified/`; the Settings capture was inspected after fixing post-Stop log-total refresh. No new image was generated or edited, and no published package was produced.


## Desktop UI refinement captures

The user-requested shared UI refinement is captured separately; the older Phase 3 images above remain unchanged. macOS 26.3.1 arm64, Release/.NET10.0.13/Avalonia12.1.4, final local loopback capture **2026-10-16 19:05:42 +01:00**. [Overview](after/2026-10-16-ui/macos-arm64/synups-overview.png), [minimum 820×560 layout](after/2026-10-16-ui/macos-arm64/synups-overview-compact.png), [targets, scrolled to actual sample](after/2026-10-16-ui/macos-arm64/synups-targets.png) and [Preferences/storage](after/2026-10-16-ui/macos-arm64/synups-storage.png) were rendered from the visible native client tree and inspected. All measurements are actual explicit loopback; there are no invented outages. Native window chrome is excluded. Exact source/capture hashes, test counts and two clean monitoring/restart runs: [UI verification manifest](verification/2026-10-16-ui-macos-arm64.json). These local captures do not certify Windows/Linux or published apphosts; the new pushed source needs separate CI results.
