# Download and run Synups

This is a portable, local-first Avalonia/.NET 10 application. The .NET runtime and native SQLite are included. A cloud account, server and separate .NET installation are not required. Native OS/display libraries are still required. Verification is tied to an exact commit in the artifact manifest and migration evidence, not to every version of an OS.

## Download and integrity

Download the matching complete archive from **[Synups v0.1.1](https://github.com/samscript18/Synups/releases/tag/v0.1.1)** and its [SHA256SUMS](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS) or adjacent `.sha256` sidecar. Public Release downloads require no GitHub login and do not use CI artifact expiration. Extract the archive itself; there is no outer CI ZIP or `packages/` directory to navigate. Do not mix runtime files from different versions. These portable builds are not installers. Release/tag source is `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`; [anonymous download verification](../evidence/verification/synups-0.1.1-publication.json) confirms exact reviewed bytes.

Compare the archive SHA-256 with its sidecar before extraction:

- Windows PowerShell: `Get-FileHash .\Synups-win-x64.zip -Algorithm SHA256`
- macOS: `shasum -a 256 Synups-osx-arm64.tar.gz`
- Linux: `sha256sum -c Synups-linux-x64.tar.gz.sha256`

The inner `ARTIFACT_MANIFEST.json` records source SHA, RID, all published file hashes, identity and signature limits. `LICENSE`, `THIRD_PARTY_NOTICES.md`, `DEPENDENCY_NOTICES.json` and `licenses/` preserve the original attribution and dependency terms.

## Released v0.1.1 — internal rc.4 identification

The selected payload is the frozen **0.1.1-rc.4** application at **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, [native three-OS verification](https://github.com/samscript18/Synups/actions/runs/39850198899), **91.7% inventory parity**. Its public tag/release name is `v0.1.1`; Help/About and ARTIFACT_MANIFEST retain `0.1.1-rc.4`. These are unchanged verified bytes, not a rebuilt final-version executable. [Release notes and archive hashes](v0.1.1/RELEASE_BODY.md) · [Reviewed upload manifest](v0.1.1/ASSET_MANIFEST.json). All15 public assets passed anonymous download/hash verification. [v0.1.0 remains an unchanged rollback release](https://github.com/samscript18/Synups/releases/tag/v0.1.0).

For a reviewed rc.4 package, extract into its own directory rather than overlaying a different version. Back up history through Synups before upgrading and keep the existing installation. Closed-copy rc.3 history/configuration/preferences compatibility passed, including a real incident/comment; this is not every historical-format certification. Opening never starts monitoring by default. Supported genuine WirePeep `.stgx` profiles may be imported through explicit preview/confirmation with destination backup; no automatic import or probing occurs.

The README inside each frozen archive is the original build-time documentation snapshot and still discusses the older rc.3 proposal (88.9%). It is intentionally preserved byte-for-byte; use the accompanying current RELEASE_NOTES.md, INSTALLATION.md and VERIFICATION.json for this rc.4 payload (91.7%). Application identity is established by Help/About and ARTIFACT_MANIFEST.json, both identifying rc.4 and its exact source.

## Windows x64

Extract the complete `Synups-win-x64.zip` to a local user-writable folder. Open **Synups.exe**. Do not move just the EXE: it needs its adjacent managed and native files. This portable build has no trusted publisher certificate; SmartScreen or organization policies can warn or block execution. Normal opening does not create a registry login/startup entry. rc.4 offers a separate explicit confirmed enable/remove action; actual next-login execution remains unverified. An MSI/MSIX installer and publisher signing are not delivered.

Actual verification uses the exact Windows runner/image documented in CROSS_PLATFORM.md. A server runner is not proof of every ordinary Windows 10/11 desktop environment. Native OS notification delivery is unavailable in this portable Windows build; clearly labelled in-app alerts remain available.

## macOS arm64 (Apple silicon)

Extract `Synups-osx-arm64.tar.gz` with Archive Utility or `tar -xzf Synups-osx-arm64.tar.gz`. Move **Synups.app** to a local folder, such as your user Applications folder, and open it. Terminal launch from the extracted folder is `./Synups.app/Contents/MacOS/Synups`.

The bundle has Synups identity/icon and is **ad-hoc signed for local integrity only**. It has no Developer ID publisher identity and is **not notarized**. This does not establish Gatekeeper trust for Internet downloads; macOS or organization policy may block it. Do not disable system security to run this candidate. A read-only keychain check found **zero valid code-signing identities** on 2026-10-21. Signing/notarization cannot be completed here without legitimate credentials. Real browser-downloaded-bundle approval remains unverified. Intel Mac builds are not provided.

If macOS blocks an unidentified/unnotarized app, first verify its source and checksum and review [Apple’s official guidance](https://support.apple.com/en-us/102445). Apple documents an individual **System Settings → Privacy & Security → Open Anyway** approval after an attempted launch, where policy permits and the user independently decides to trust that particular app. This is not automatic trust, notarization or a step performed by this project. Managed Macs may prohibit it. A malware, damaged or revoked-app warning requires investigation rather than an override. Do not remove quarantine attributes, disable Gatekeeper or change global security policy to obtain a passing result. No browser/quarantine approval has been tested in this environment.

## Linux x64

Extract with `tar -xzf Synups-linux-x64.tar.gz`, enter `Synups-linux-x64`, then run `./Synups`. Tar preserves executable bits. Use a graphical user session; do not run as root. Ubuntu 24.04/X11 verification uses `libx11-6`, `libice6`, `libsm6`, `libfontconfig1`, `libgl1` and `libegl1`; CI additionally supplies Xvfb. Other distributions, Wayland sessions and sandboxed installers need their own checks. A `.desktop.example` is provided; it is not installed automatically. If you use it, replace both absolute paths before installing it through your desktop's normal user workflow.

A missing tray host must leave the window available through the taskbar. Optional Linux notification requests require `notify-send` and a running notification daemon; a request is not guaranteed delivery. There is no distro package or Flatpak/AppImage installer in this candidate.

## First run and daily use

The app opens stopped with an empty profile. In released v0.1.1 (internal rc.4), use the first-target group/name/literal-IP fields and **Add target and create group**, then **Start monitoring**. In public v0.1.0, add a group and literal IP target through Targets and groups. Restore original defaults is optional and never performs discovery. Polling, failure grace, response timeout and peer-group status are visible; a group response proves ICMP connectivity to a peer, not universal Internet health. Unprobed targets stay not checked/stale. Stop before editing. Applied configurations persist immediately.

History is stored separately from configuration/preferences in the per-user `.NET LocalApplicationData/Synups` folder; the UI shows paths and storage details. `--data-directory ABSOLUTE_PATH` selects a separate local workspace without changing the original one. Avoid network filesystems and do not run two workspaces against the same database through path aliases. A second normal process for the same directory asks the existing instance to show; it does not start another monitoring engine.

Closing means save, stop and quit, optionally confirmed; minimizing is separate. Show/Exit tray availability depends on the OS/session. Monitoring on launch is disabled until explicitly saved. No OS login registration is performed automatically; rc.4 offers explicit enable/remove controls, and actual next-login execution/removal remains unverified. Back up history through the in-app SQLite backup action. Pending write errors keep evidence owned in memory and block a misleading clean exit: repair storage and Retry rather than force quit. CSV is an outage export, not a full database backup. See the migration architecture/readiness documents for retention and recovery limits.

## Explicit installation check

`Synups[.exe] --help` describes the supported isolated installation check. It exercises the actual executable's normal App/VM/probe/store using real controls, explicit loopback and new temporary data. The repository's `eng/published-smoke.py` extracts an archive, validates it, runs separate apphost processes and checks durable results. It does not build a verifier. Programmatic control events are not physical mouse/keyboard, screen-reader, native save-picker or OS tray-click certification. Controlled termination is an explicit isolated process test, not a genuine network outage or a physical power-loss experiment.
