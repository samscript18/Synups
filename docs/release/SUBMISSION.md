# Prepared competition information — not submitted

Application: **Synups**. Primary category: **Best IT Pro Tool**. Secondary strengths: Best Cross-Platform Port and Best Legacy Revival. Open-source entry under the original MIT licence; no account, licence key or cloud service required.

Description: Synups is a local-first network reliability monitor for developers and IT professionals. The Avalonia/C#/XAML migration of Bill Menees' WPF WirePeep performs real ICMP checks, evaluates rotating peer groups, detects confirmed incidents and observed recoveries, and preserves sessions, comments and outage evidence in local SQLite. It includes editable persistent profiles, CSV/session logs, storage safeguards and desktop preferences. Results distinguish failed ICMP, unavailable probes and unobserved periods rather than claiming fabricated Internet uptime.

Repository: https://github.com/samscript18/Synups

Migration report: [public README](https://github.com/samscript18/Synups#readme). Detailed [migration log](../migration/MIGRATION_LOG.md), [feature inventory](../migration/FEATURE_PARITY.md), [architecture](../migration/ARCHITECTURE.md) and [platform evidence](../migration/CROSS_PLATFORM.md). Original application existed in 2024; upstream SHA `61ae14391151ca9f209223e62c98fae3df5482b8` remains an ancestor. AI assistance is disclosed; no total development hours were instrumented or invented.

Before/after: [attributed original illustration and genuine Synups captures](../evidence/README.md). The original image is Bill Menees' upstream illustration; Synups images are actual native client renders, not OS desktop screenshots. Do not relabel either source.

Candidate application revision: **2973d3349fcb5af5639597920842288e6549bbd0**. [Actual three-OS run](https://github.com/samscript18/Synups/actions/runs/39682197118). Windows x64, macOS arm64 and Linux x64 packages contain the runtime/native SQLite. Claims are limited to the documented native runner/local OS versions; consumer Windows, Linux physical desktops/Wayland and ordinary browser-quarantined Mac launch are not certified.

## Published release — competition submission still separate

Published tag: `v0.1.0`, attached to the exact application revision above. All following public addresses returned HTTP 200; all 15 release asset downloads were anonymously hash-verified. Publication was explicitly authorized; the competition form has not been submitted.

```text
https://github.com/samscript18/Synups/releases/tag/v0.1.0
https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-win-x64.zip
https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-osx-arm64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-linux-x64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.0/SHA256SUMS
```

Installation limitations must accompany the entry: Mac ad-hoc integrity passes but Gatekeeper assessment rejects it; no signing identity is available here. Windows is unsigned. Native tray/menu/save-picker/notification delivery and broad accessibility checks are incomplete. Judges must not be told to disable OS security. See [installation](INSTALLATION.md).

The [official announcement](https://avaloniaui.net/blog/avalonia-port-challenge) was rechecked on 2026-10-21: deadline **2026-10-23 23:59 UTC**, submission through its linked [official form](https://forms.gle/bgvQLAmJpTCNcqwR9). The browser-fetch tool could not retrieve the redirected Google form; no fields were filled and no entry was submitted. Entrant identity/contact/team eligibility must be supplied and reviewed by the user, not invented.

Publication/anonymous download checks are complete and [recorded](../evidence/verification/synups-0.1.0-publication.json). Before any entry, obtain separate submission authorization, review entrant identity/contact/team eligibility and all stated limitations, recheck public links and record the actual submission receipt. Prepared information is not an accepted entry.


## Optional newer candidate — do not silently substitute

Separately verified unpublished [0.1.1-rc.2](FINAL_HARDENING_CANDIDATE.md) adds explicit gateway discovery, bounded incident timeline, clipboard/help/shortcuts and opt-in event restoration. Its source,128 unique .NET cases exercised per OS,8 Python cases, exact packages/hashes, CodeQL/coverage and genuine images are recorded separately. Public judge downloads above remain0.1.0 until a separately authorized Release is published and anonymously checked. No new candidate URL or accepted competition entry is implied. Physical Mac acceptance/trusted signing remain explicit limitations.
