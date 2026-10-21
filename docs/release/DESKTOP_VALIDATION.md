# Desktop acceptance protocol

These are reviewer steps, not additional passed results. Use the exact candidate SHA/checksum and a separate local workspace (`--data-directory ABSOLUTE_PATH`). Keep normal user history untouched. Record OS version, CPU, display scale, package hash, timestamps, screenshots and observed failures. Automated evidence and its limits are in [CROSS_PLATFORM.md](../migration/CROSS_PLATFORM.md).

## Delivery and identity

- Download the whole matching archive through an ordinary browser. Verify its checksum, extract, and open its real apphost/app bundle without a development SDK or repository checkout.
- Check the Synups name/icon, native window controls, writable per-user paths and useful errors. Record publisher, SmartScreen/Gatekeeper and permission prompts. Do not disable OS security to obtain a pass. macOS ad-hoc signing is not Developer ID/notarization.
- Windows: exercise a normal Windows desktop as well as the recorded Server CI runner. macOS: assess the quarantined downloaded bundle, not only terminal/LaunchServices launch. Linux: use a real X11 desktop, test absent tray/notification hosts, and assess Wayland separately; Xvfb is not this test.

## Monitoring, editing and evidence

- Fresh launch must remain stopped. Create a group and a named `127.0.0.1` target, enable/save the group, then explicitly Start. Confirm actual check timestamps and response latency. A genuine loopback RTT can be 0ms. This does not prove Internet connectivity.
- Stop; rename/move/add/delete targets and groups; change timing/enablement; check invalid addresses and long names. Restart the actual executable and confirm configuration/IDs. No target should inherit a previous address's measurement.
- Use a controlled test network and approved real targets for disconnect/reconnect observations. Record the exact induced condition; distinguish failed ICMP from Internet downtime. Check failure grace, one confirmed incident, one observed recovery, comments, chronology and duration. Never fabricate incidents to populate screenshots.
- Close during a confirmed outage, reopen and confirm the previous incident remains unresolved/observation-ended. New monitoring must not recover an older session retroactively. Export CSV through the native save picker and reopen it with a CSV reader, including a comment with commas/quotes/newlines. Empty healthy history legitimately has no outage rows.
- Create a full history backup through Preferences and verify it independently while preserving the original. Test permissions/capacity/corruption only in disposable isolated workspaces with backups. Repair and Retry; do not force quit to bypass pending evidence or remove an open SQLite journal.

## Lifecycle and alerts

- Minimize, restore through the OS and physically click tray Show/Exit where a host exists. Test a second apphost for the same workspace: one monitor, original window restored. Keep separate workspaces separate.
- Verify topmost behavior against another window, saved geometry across displays/scales, launch minimized, both exit-confirmation buttons and clean stop/save/quit. Absent Linux tray hosts must not hide the application.
- Enable monitoring-on-launch explicitly, restart and verify it; disable it and verify passive launch again. No OS login/startup registration should be created.
- Exercise alert preferences on observed transitions and errors; check deduplication. Clearly distinguish in-app alerts, native requests and actual OS delivery/denial. Windows has an explicitly documented in-app fallback. No notification storm or hidden external service is acceptable.

## Accessibility and stability

- Traverse navigation, monitoring controls, editors, preferences and dialogs with keyboard only; inspect visible focus, selection, default/cancel and screen-reader names. Test minimum size, long text, 150%/200% scaling, disconnected displays and high-contrast settings. Static token contrast and programmatic focus captures do not replace this review.
- Leave the actual GUI running for hours/days with representative real targets and bounded disposable storage. Measure RSS, CPU, descriptors/handles, worker ownership, polling age, database/log growth, repeated errors and stop/restart. Healthy loopback and a two-hour Core-console observation are narrower evidence.
- Test no network, IPv6, VPN/firewall restrictions and suspend/resume on controlled machines. Record genuine OS failures separately from deterministic fault injection. Preserve failing evidence and rerun corrections against the exact new revision.
