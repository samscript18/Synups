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


The refreshed UI subsequently passed its own [three-OS run38944933816](https://github.com/samscript18/Synups/actions/runs/38944933816) at **a368d83dbb5494e283efa02b7e8036a9ce9664c4**. Downloaded complete logs/packages/TRX and viewed actual native overview captures at both sizes. Selected captures are retained without editing:

| Actual native runner | Normal overview | Minimum820×560 |
|---|---|---|
| Windows Server2025 x64 | [Overview](after/2026-10-16-ui/ci/win-x64/synups-overview.png) | [Compact](after/2026-10-16-ui/ci/win-x64/synups-overview-compact.png) |
| macOS15.7.9 arm64 | [Overview](after/2026-10-16-ui/ci/osx-arm64/synups-overview.png) | [Compact](after/2026-10-16-ui/ci/osx-arm64/synups-overview-compact.png) |
| Ubuntu24.04.5 x64 / X11 Xvfb | [Overview](after/2026-10-16-ui/ci/linux-x64/synups-overview.png) | [Compact](after/2026-10-16-ui/ci/linux-x64/synups-overview-compact.png) |

These are actual loopback observations at different times, not a benchmark. Below-viewport content scrolls; Windows/macOS runner work areas can constrain the normal window. Client-area rendering excludes OS chrome and does not establish native screen-reader/desktop behavior. [UI CI manifest](verification/2026-10-16-ci-ui.json) records exact source, job/artifact IDs and original PNG hashes. Published apphosts were not launched; the separate native verifier references production composition.


## Phase 4B — actual published apphosts (2026-10-21)

The earlier verifier captures remain historical. New captures below come from the **real extracted self-contained executables** at source **292b847bffa96d2a23d771c3ba0ce4648c06844b**, [run 39678991269](https://github.com/samscript18/Synups/actions/runs/39678991269). All three complete artifact ZIPs and workflow logs were downloaded; API digests, inner checksums, every file hash, licences/notices, native architecture, actual PID/apphost/runtime paths, reports and SQLite records were independently checked. These are actual visible-window client renders, not physical desktop/compositor screenshots. No image generation/editing or fabricated measurements were used.

| Actual OS | Overview | Minimum layout with real programmatic focus | Actual read-only error |
|---|---|---|---|
| Windows Server 2025 x64 | [Overview](after/2026-10-21-apphost/win-x64/exercise-overview.png) | [Focus](after/2026-10-21-apphost/win-x64/exercise-focus.png) | [Error](after/2026-10-21-apphost/win-x64/storage-error-read-only.png) |
| macOS 15.7.9 arm64 | [Overview](after/2026-10-21-apphost/osx-arm64/exercise-overview.png) | [Focus](after/2026-10-21-apphost/osx-arm64/exercise-focus.png) | [Error](after/2026-10-21-apphost/osx-arm64/storage-error-read-only.png) |
| Ubuntu 24.04.5 x64 / X11 Xvfb | [Overview](after/2026-10-21-apphost/linux-x64/exercise-overview.png) | [Focus](after/2026-10-21-apphost/linux-x64/exercise-focus.png) | [Error](after/2026-10-21-apphost/linux-x64/storage-error-read-only.png) |

[Exact artifact/apphost/capture/test hashes, timestamps, job links and limitations](verification/2026-10-21-published-apphosts.json). Each suite used explicit 127.0.0.1; all normal exits were0, and only the deliberate interruption was nonzero. The final downloaded Mac bundle passes strict/deep signature validation and a local LaunchServices/real-monitor/close check on macOS 26.3.1. Gatekeeper assessment rejects it (exit3): ad-hoc integrity is not Developer ID/notarization or browser-download trust.

[GUI resource observations](verification/2026-10-21-gui-resources-macos-arm64.json) retain two separate five-minute apphost processes at the preceding 3cf2720 checkpoint, including exact package/apphost hashes and5s samples. This is not multi-hour GUI endurance or proof of no leak. The earlier overlapping GUI observation failed focus and remains documented; it is not counted as a pass. [Static token contrast](verification/2026-10-21-theme-contrast.json) and visible focus captures are narrow checks, not a keyboard/screen-reader/high-contrast certification. Physical tray/save-picker/notification delivery and original fresh WPF Windows capture still need review.


The [two-hour real-Core observation](verification/2026-10-21-engine-endurance-macos-arm64.json) completed on local macOS arm64 with28,792 real loopback probes and clean stop/restart/reopen. [Raw720 samples](verification/2026-10-21-engine-endurance-samples.jsonl) retain CPU/RSS/descriptors/workers/probes/database data. Source/build qualification is explicit: earlier working-tree Core subsequently committed unchanged, exact loaded assembly hash retained; not a published-apphost or multi-hour GUI claim. No actual outage, physical power failure or leak-free result is fabricated.


## Genuine macOS OS-window capture —22 October2026

[Normal launch of unchanged published0.1.0 including native OS chrome](after/2026-10-22-competitive/macos-stable-normal-launch.png), [exact source/apphost/PID/permission/quit record](verification/2026-10-22-macos-desktop-acceptance.json). Used the reviewed downloaded bundle at2973d3349fcb5af5639597920842288e6549bbd0 through LaunchServices with a fresh isolated profile, no installation-check mode and no configured target. CoreGraphics identified only its owned native window; screencapture for that exact window succeeded. Zero sessions were created; no measurements are implied. Native OS quit request completed, but application exit code is unknown. Accessibility permission remains false, so this is not evidence of physical tray/keyboard/save-picker interaction. Earlier failed screenshot attempts remain historical; no image was generated, edited or staged with invented metrics.


## Competitive incident evidence — 22 October 2026

[Source-specific independent package/runtime audit](verification/2026-10-22-competitive-candidate.json), [CI logs](verification/2026-10-22-competitive-ci-logs.zip) and [controlled incident protocol](../competition/REAL_INCIDENT.md) retain exact source/run/hash distinctions. Linux namespace-local real ICMP packet loss/recovery produced the healthy/grace/confirmed/recovered/restarted images under `after/2026-10-22-competitive/linux-incident/`. These are genuine native client-tree renders from the production-source component verifier, not physical desktop/OS-notification captures or a packaged-apphost incident claim. Raw transition/restart/CSV evidence is in `verification/2026-10-22-real-incident/`.

The normal Mac stable-release capture has OS chrome and separate source/hash/permission evidence. Candidate fresh-profile captures are from actual extracted apphosts; they show no invented measurements. Initial Windows first-run capture revealed a stale-layout capture race after screen clamping; the follow-up capture-only correction and source-specific verification are recorded separately. Do not relabel preceding-source captures as later binaries.


Final first-run images in `after/2026-10-22-competitive/{win-x64,osx-arm64,linux-x64}/first-run.png` are from actual extracted **65d917ac645139836cf97e48c48c137a09825942** apphosts, [run 39761184906](https://github.com/samscript18/Synups/actions/runs/39761184906); native viewport checks and independent visual review pass. `win-x64/first-run-before-fix.png` intentionally retains the failed 375e2f7 state. The incident PNGs remain from successful 535b91f; later final 65d917a independently repeats transitions with raw data under `verification/2026-10-22-real-incident-final/`. [Final archive/runtime/capture/TRX hashes](verification/2026-10-22-competitive-candidate-final.json), [final CI logs](verification/2026-10-22-competitive-ci-logs-final.zip). No source substitution or invented measurements.

[Retained final TRX and actual apphost action reports](verification/2026-10-22-competitive-reports-final.zip) preserve the six test invocations and eighteen process-stage JSON reports beyond CI artifact expiry; their archive hash is in the final verification record.
