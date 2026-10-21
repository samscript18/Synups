# Run the Synups candidate

This is a portable, local-first Avalonia/.NET10 application. The .NET runtime and native SQLite are included. A cloud account, server and separate .NET installation are not required. Native OS/display libraries are still required. Verification is tied to an exact commit in the artifact manifest and migration evidence, not to every version of an OS.

## Download and integrity

Until a separately authorized GitHub Release is published, use the [Synups workflow](https://github.com/samscript18/Synups/actions/workflows/synups.yml), select a successful run for the reviewed source SHA, and download its matching platform artifact. GitHub login may be required. These CI artifacts expire after30 days. Inside the download, use `packages/Synups-<rid>.zip` or `.tar.gz` and its adjacent `.sha256` file. Do not run executables from the outer CI ZIP or mix runtime files from different versions. Release candidates are not installers.

Compare the archive SHA-256 with its sidecar before extraction:

- Windows PowerShell: `Get-FileHash .\Synups-win-x64.zip -Algorithm SHA256`
- macOS: `shasum -a 256 Synups-osx-arm64.tar.gz`
- Linux: `sha256sum -c Synups-linux-x64.tar.gz.sha256`

The inner `ARTIFACT_MANIFEST.json` records source SHA, RID, all published file hashes, identity and signature limits. `LICENSE`, `THIRD_PARTY_NOTICES.md`, `DEPENDENCY_NOTICES.json` and `licenses/` preserve the original attribution and dependency terms.

## Windows x64

Extract the complete `Synups-win-x64.zip` to a local user-writable folder. Open **Synups.exe**. Do not move just the EXE: it needs its adjacent managed and native files. This portable build has no trusted publisher certificate; SmartScreen or organization policies can warn or block execution. No registry login/startup entry is created. An MSI/MSIX installer and publisher signing are not delivered.

Actual verification uses the exact Windows runner/image documented in CROSS_PLATFORM.md. A server runner is not proof of every ordinary Windows10/11 desktop environment. Native OS notification delivery is unavailable in this portable Windows build; clearly labelled in-app alerts remain available.

## macOS arm64 (Apple silicon)

Extract `Synups-osx-arm64.tar.gz` with Archive Utility or `tar -xzf Synups-osx-arm64.tar.gz`. Move **Synups.app** to a local folder, such as your user Applications folder, and open it. Terminal launch from the extracted folder is `./Synups.app/Contents/MacOS/Synups`.

The bundle has Synups identity/icon and is **ad-hoc signed for local integrity only**. It has no Developer ID publisher identity and is **not notarized**. This does not establish Gatekeeper trust for Internet downloads; macOS or organization policy may block it. Do not disable system security to run this candidate. Developer ID signing/notarization and real downloaded-bundle approval remain release gates. Intel Mac builds are not provided.

## Linux x64

Extract with `tar -xzf Synups-linux-x64.tar.gz`, enter `Synups-linux-x64`, then run `./Synups`. Tar preserves executable bits. Use a graphical user session; do not run as root. Ubuntu24.04/X11 verification uses `libx11-6`, `libice6`, `libsm6`, `libfontconfig1`, `libgl1` and `libegl1`; CI additionally supplies Xvfb. Other distributions, Wayland sessions and sandboxed installers need their own checks. A `.desktop.example` is provided; it is not installed automatically. If you use it, replace both absolute paths before installing it through your desktop's normal user workflow.

A missing tray host must leave the window available through the taskbar. Optional Linux notification requests require `notify-send` and a running notification daemon; a request is not guaranteed delivery. There is no distro package or Flatpak/AppImage installer in this candidate.

## First run and daily use

The app opens stopped with an empty profile. Add a group, add a literal IP target, enable the group, apply, then choose **Start monitoring**. Restore original defaults is optional and never performs discovery. Polling, failure grace, response timeout and peer-group status are visible; a group response proves ICMP connectivity to a peer, not universal Internet health. Unprobed targets stay not checked/stale. Stop before editing. Applied configurations persist immediately.

History is stored separately from configuration/preferences in the per-user `.NET LocalApplicationData/Synups` folder; the UI shows paths and storage details. `--data-directory ABSOLUTE_PATH` selects a separate local workspace without changing the original one. Avoid network filesystems and do not run two workspaces against the same database through path aliases. A second normal process for the same directory asks the existing instance to show; it does not start another monitoring engine.

Closing means save, stop and quit, optionally confirmed; minimizing is separate. Show/Exit tray availability depends on the OS/session. Monitoring on launch is disabled until explicitly saved. No OS login registration is performed. Back up history through the in-app SQLite backup action. Pending write errors keep evidence owned in memory and block a misleading clean exit: repair storage and Retry rather than force quit. CSV is an outage export, not a full database backup. See the migration architecture/readiness documents for retention and recovery limits.

## Explicit installation check

`Synups[.exe] --help` describes the supported isolated installation check. It exercises the actual executable's normal App/VM/probe/store using real controls, explicit loopback and new temporary data. The repository's `eng/published-smoke.py` extracts an archive, validates it, runs separate apphost processes and checks durable results. It does not build a verifier. Programmatic control events are not physical mouse/keyboard, screen-reader, native save-picker or OS tray-click certification. Controlled termination is an explicit isolated process test, not a genuine network outage or a physical power-loss experiment.
