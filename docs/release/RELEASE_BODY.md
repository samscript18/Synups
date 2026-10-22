# Synups 0.1.0 — Avalonia Port Challenge candidate

Application packages below are immutable CI bytes from **2973d3349fcb5af5639597920842288e6549bbd0**, [verified run 39682197118](https://github.com/samscript18/Synups/actions/runs/39682197118). Later documentation commits do not change these binaries. Do not rebuild or substitute packages under the same verification record.

Synups is a local-first network reliability monitor built with Avalonia, C# and XAML on .NET 10. It migrates Bill Menees' MIT-licensed WirePeep while retaining its ancestry, original source and attribution. Configure literal-IP peers, explicitly start real ICMP monitoring, inspect group health/freshness and retain confirmed incidents, comments and sessions locally in SQLite. No cloud account, backend, WebView or fabricated production measurements.

Download the complete matching archive and `SHA256SUMS`, verify its hash, then extract all files. Runtime, SQLite, MIT licence, dependency/runtime/font notices and per-file manifest are included. See `INSTALLATION.md` before launch.

| Platform | Asset | Launch | Important limit |
|---|---|---|---|
| Windows x64 | `Synups-win-x64.zip` | `Synups.exe` | Unsigned; SmartScreen or organization policy can block it. |
| macOS Apple silicon | `Synups-osx-arm64.tar.gz` | `Synups.app` | Ad-hoc integrity only; no Developer ID/notarization. Gatekeeper rejects trusted delivery. |
| Linux x64 | `Synups-linux-x64.tar.gz` | `./Synups` | Requires graphical/native libraries; Ubuntu X11/Xvfb tested, Wayland not certified. |

Each native CI job passed locked restore/audit, zero-warning Release, 105 deterministic/headless tests, one separately enabled real-loopback test, four Python safety tests and six actual extracted-apphost stages. Five normal exits were 0; the deliberately killed isolated process is separately recorded. Configuration/history reload, real loopback, stop, restoration, read-only SQLite failure and repair were verified. These are programmatic controls, not physical tray, native save-picker, keyboard-only or OS notification-delivery certification. Windows ran on Server 2025; macOS on 15.7.9; Linux on Ubuntu 24.04.5/Xvfb. Local macOS 26.3.1 repeated all six stages and LaunchServices launch.

`VERIFICATION.json` and `VERIFICATION_EVIDENCE.zip` retain source/package/test/capture hashes, CI logs/TRX, native client renders and scope limitations. Before illustration is attributed upstream, not a newly captured WPF run. Real loopback imagery does not prove Internet health. A separate qualified two-hour Core observation is documented in the repository; it is not multi-hour packaged-GUI certification.

Known limits: unsigned/unnotarized distribution; ordinary downloaded-desktop acceptance and broader accessibility review pending; Windows native alerts use a labelled in-app fallback; macOS/Linux notification requests depend on OS policy. Legacy import/discovery/login/sounds and some menu/taskbar details are absent. Conservative legacy inventory parity is 72.2%, not a complete-port claim. Use full history backup for preservation; CSV is not a database backup. Pending storage failures must be repaired before clean close.

[Repository and migration write-up](https://github.com/samscript18/Synups), [before/after evidence](https://github.com/samscript18/Synups/tree/feat/avalonia-migration/docs/evidence), [release readiness](https://github.com/samscript18/Synups/blob/feat/avalonia-migration/docs/migration/RELEASE_READINESS.md). No production-readiness or trusted-publisher claim. Publishing this release does not submit the competition entry.

Bundled handoff documents preserve their reviewed pre-publication wording. This public release page and the current repository record publication status; all reviewed asset bytes and checksums remain unchanged.
