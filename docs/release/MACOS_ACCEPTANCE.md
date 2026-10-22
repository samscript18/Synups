# macOS desktop acceptance — operator checklist

The current hardware is macOS 26.3.1 arm64. A fresh read-only check at **2026-10-22T12:35:25Z** returned Accessibility **false**, event posting **false**, screen capture **true**. No permission prompt or change was made. Therefore this agent cannot perform physical tray/menu clicks, OS keyboard input or native Save-panel completion. Programmatic routed keys/buttons and ViewModel calls are explicitly different evidence.

A person can complete the following checklist with the exact extracted candidate. Record the application revision from Help/About, archive SHA-256, OS/build, timestamps, actual observations and failures. Do not describe these as passed until someone performs them. Do not enable permissions or change security settings merely to manufacture evidence.

## Isolated normal launch

Verify the archive against its candidate checksums, extract the complete bundle, then open Synups.app normally. If using Terminal to select a disposable profile:

```sh
synups_acceptance_dir="$(mktemp -d)"
open -n "/absolute/path/Synups.app" --args --data-directory "$synups_acceptance_dir"
```

This workspace must be new and separate from real user data. Gatekeeper may reject the unsigned publisher identity; follow the standard [installation guidance](INSTALLATION.md), and stop/report if organizational policy requires a trusted signature. Do not disable Gatekeeper, remove quarantine or claim notarization. Normal opening is not the --verify-installation protocol.

## Physical actions to perform

| Case | Exact operator actions | Required observation |
|---|---|---|
| First run | Tab through group/name/IP fields; type 127.0.0.1; activate Add with Enter/Space | No probes before explicit Start; enabled group saved; unknown state remains Not checked |
| Real monitoring | Click Start; observe timestamp/RTT; click Stop, then Start again | Actual loopback response; no invented Internet health; clean restart |
| Gateway | Stop; Targets and groups → Discover; inspect interface/address; Cancel or dismiss confirmation | Read-only snapshot; no automatic target/probe; existing IDs stay unchanged |
| Tray Show | Preferences → enable minimize-to-tray only if capability is reported; Apply; click yellow minimize; click Synups status icon → Show | Window restored and focused, or an honest dock fallback; no unreachable hidden window |
| Tray Exit | Enable exit confirmation; Start monitoring; click status icon → Exit; first Cancel, then repeat and Continue | Cancel keeps the session; Continue drains/saves and exits; unresolved does not become recovered |
| Keyboard | Use Tab and Shift+Tab across each screen; Enter/Space on buttons; F1 Help; Command+1…5 screens; F5/Shift+F5 Start/Stop | Visible focus, correct route, no text-input shortcut interference |
| Escape | Open a confirmation and press Escape; separately run discovery and press Escape | Confirmation cancelled; active discovery cancelled without configuration mutation; Escape does not quit the main window |
| Clipboard | Select target → Copy IP; paste into TextEdit; select incident → Copy details and paste | Correct IP/incident ID/comment; UTC evidence preserved; unknown recovery says Not observed |
| Resize | Resize through normal and minimum sizes; scroll each view | Readable wrapping, reachable buttons/navigation, no clipped right edge; no keyboard traps |
| Always on top | Stop; toggle preference and Apply; switch to another ordinary app | Actual OS stacking behavior recorded; restore the prior preference afterwards |
| Event focus | Explicitly enable event focus; use a controlled, authorized incident environment | Once per confirmed outage/recovery, not per sample/error; OS may refuse activation; do not disrupt the real network |
| Error feedback | Use an invalid literal-IP edit, cancel discovery, and inspect any actual ICMP permission failure | No destructive save, clear recovery instructions, errors remain indeterminate |

## Genuine native CSV Save-panel test

Export needs a real recorded incident. Do not inject a fake one. Use an existing authorized history, or a **closed copy** of the real Linux namespace demonstration’s history.v1.db in a separate disposable workspace. Verify that database’s hash from the exact CI artifact/namespace record before copying; never copy or move an open database or omit a required recovery journal. Copy only the closed history file, not CI preferences or machine-specific log paths. This is genuine historical Linux evidence displayed on macOS, not a newly induced Mac outage.

Open that disposable workspace. Activity and outages → Export outages CSV. Verify a genuine macOS Save panel appears; choose the disposable folder, enter Synups-acceptance.csv and click Save. Inspect the exported incident ID, UTC timestamps, duration, comment and quoting against the source record. Open the panel again and press Escape; no new export should be created. Record actual Save/Cancel results and an owned-window capture where permitted. Service-level export and a routed button call alone do not pass this physical interaction test.

## Closing with active monitoring / pending evidence

On the disposable loopback workspace, confirm ordinary Close while monitoring drains/saves. For an optional controlled file-permission fault, remove write permission **only from that disposable directory** while monitoring; this should prevent a new SQLite rollback journal. This is intentional filesystem fault injection, not an actual disk failure or network outage. Attempt Close: the app must retain pending evidence and show repair feedback, without inventing recovery. Restore directory write permission, use Retry history storage, Stop and Close. Verify a subsequent launch reloads the session/incident state. Never alter the real profile, system directories or live network, and never force-quit to claim a successful clean close.

## Results and limits

The operator cases above remain **not physically certified** because both Accessibility and event posting are denied. Headless control/routing tests and real extracted-apphost/native clipboard/loopback/restart/permission tests are recorded separately in CROSS_PLATFORM.md. Normal native launch/window capture/OS quit can be observed without substituting those checks for physical tray, Save-panel or keyboard acceptance. [Release readiness](../migration/RELEASE_READINESS.md) retains this blocker.

## rc.3 handoff update

Use source **7ca85cccce6e3609d08d64e4f23172fa14fffb25**, [candidate/hashes](LEGACY_COMPLETION_CANDIDATE.md). Fresh check22October16:40:38Z still reports Accessibility=false/event posting=false/screen capture=true. Normal extracted-bundle LaunchServices/window/OS quit at17:28:28Z and six direct actual-apphost stages passed; the physical actions above remain unperformed.

Add these operator checks: select a target/incident and exercise chosen-value/row context copying, F4 edit, Delete confirmation and Escape returning focus; launch with `/Minimize` and reopen the same workspace; use explicit candidate checks only on networks you are authorized to probe and cancel before adding if not desired. Test mixed-display restoration on actual hardware. Do not enable login registration merely for routine acceptance: only if a person explicitly wants that OS change, confirm enable, verify the actual next login, then confirm remove and verify reversal. Automated tests used isolated files and did not alter your OS startup settings. Native notification delivery remains a separate real operator observation.
