# Prepared competition entry — not submitted

Checked against the [official Avalonia Port Challenge announcement](https://avaloniaui.net/blog/avalonia-port-challenge) on22October2026. Deadline: **23October2026,23:59UTC**. Submission uses the announcement's linked [official form](https://forms.gle/bgvQLAmJpTCNcqwR9). No form was submitted and no acceptance/receipt exists. Entrant name/contact and solo/team eligibility must be supplied/reviewed by the user, not invented.

## Copy-ready information

**Application name:** Synups

**Short description:** A local-first Avalonia network reliability monitor that records confirmed ICMP incidents, observed recoveries and durable troubleshooting evidence.

**Longer description:** Synups helps individuals, developers and IT professionals investigate intermittent connectivity to configured network peers. Real ICMP checks rotate through peer groups, apply failure grace and distinguish disconnected, unavailable, stale and not-checked states. Confirmed incidents, observed recoveries and comments persist in local SQLite; chronological CSV and session logs support troubleshooting and ISP/IT conversations. Synups migrates Bill Menees' Windows WPF WirePeep to Avalonia/C#/XAML, preserving its monitoring intent and MIT provenance while adding cancellable asynchronous services, monotonic timing and durable evidence. ICMP reachability does not prove every Internet service is available. No account, backend or cloud infrastructure is required.

**Primary category:** Best IT Pro Tool. The application addresses a concrete networking/support problem and keeps reproducible evidence locally. Secondary strengths: Best Cross-Platform Port and Best Legacy Revival. No award or unsupported competitor comparison is claimed.

**Repository:** https://github.com/samscript18/Synups

**Original application:** https://github.com/menees/WirePeep

**Original preserved revision:** `61ae14391151ca9f209223e62c98fae3df5482b8`,1June2024, before the challenge opened. Original MIT licence/notices and Git ancestry retained.

**Migration report:** [Main README](https://github.com/samscript18/Synups#readme), [judge's guide](../competition/JUDGES_GUIDE.md), [architecture](../migration/ARCHITECTURE.md), [migration log](../migration/MIGRATION_LOG.md), [feature inventory](../migration/FEATURE_PARITY.md).

## Working public release links

**Submit these current v0.1.1 links**, application/tag **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, internal binary version **0.1.1-rc.4**:

- [Release page](https://github.com/samscript18/Synups/releases/tag/v0.1.1)
- [Windows x64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip)
- [macOS arm64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz)
- [Linux x64](https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz)
- [SHA256SUMS](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS)

All15 public assets were anonymously downloaded and matched the approved hashes/byte sizes; archive/full-manifest/runtime/licence checks passed. [Publication receipt](../evidence/verification/synups-0.1.1-publication.json). **Latest released parity91.7%=(15 complete+3 partial×0.5)/18.** These are the unchanged rc.4 CI archives, not rebuilt or relabelled executables. [Exact source/CI/hashes](DESKTOP_COMPLETION_CANDIDATE.md).

Historical rollback: [public v0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0), source2973d3349fcb5af5639597920842288e6549bbd0,72.2% inventory; its15 asset IDs/sizes/API digests remain unchanged. Historical rc.1/rc.2/rc.3 candidates are also preserved. Do not use their feature/test claims for this release.

## Migration answers and evidence

**Preserved:** named literal IPs and identities, peer groups/timings, sorted rotation/stop-on-success, failure grace, outage/comment/log/export intent, original XML/MIT resources/history.

**Rewritten:** WPF presentation/lifetime, UI-coupled scheduling/logging/state and Windows-only integration boundaries. Avalonia MVVM with Core/Desktop/Tests replaces Windows-target inheritance; actual native apphosts run on three OSes.

**Improved:** bounded asynchronous cancellation, monotonic timing, errors that do not falsely recover incidents, explicit stale/unknown confidence, durable SQLite sessions/incidents/comments, interrupted-session reload, bounded history pages, streaming CSV, safe manual discovery and first-run guidance.

**Biggest challenges:** preserving tri-state/group semantics while correcting false recovery; draining probes/pending storage safely; actual read-only filesystem repair; minimize/focus on different window systems; native dependency/permissions and honest unsigned distribution. Actual failures and corrections remain in the migration log.

**Actual effort:** AI-assisted repository sessions began16October2026. Frozen rc.4 was committed22October with39 commits after the preserved upstream; the README provides the reproducible command. No total developer-hours estimate was measured or invented.

**Released payload verification:** [run39850198899](https://github.com/samscript18/Synups/actions/runs/39850198899): Windows/Mac159 default .NET cases, Linux160, each3 opt-in real/native+1 audio request/fallback and8 Python cases; zero-warning Release; six actual extracted executable stages on native Windows/macOS and Linux/Xvfb+Openbox. Of164 unique .NET cases,4 are opt-in and1 Linux-native Gio case is platform-specific. [CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) zero SARIF results, scoped coverage79.83% lines/66.89% branches. Actual source/package/runtime distinction remains explicit in [platform evidence](../migration/CROSS_PLATFORM.md).

**Before/after:** [fresh original WPF Windows capture, credited upstream illustration and genuine source-specific Synups captures](../evidence/README.md). [Real incident demonstration](../competition/INCIDENT_DEMO.md): actual kernel-filtered ICMP in a disposable namespace, confirmation/recovery/comment/CSV/restart evidence, not an ISP outage or injected result.

## Limitations to disclose with the entry

Windows builds are unsigned. Mac ad-hoc integrity passes but Gatekeeper rejects trusted delivery; no Developer ID/notarization is available. Do not tell judges to disable OS security. [Installation](INSTALLATION.md) · [Signing findings](DISTRIBUTION_TRUST.md).

Physical tray/keyboard/native Save-panel/multiple-display acceptance, actual OS notification delivery/login execution and broader IPv6/VPN/suspend/Wayland/endurance remain incomplete. Mac Accessibility/event posting is denied. The frozen rc.4 inventory remains91.7%, with alerts/preferences/placement partial. Genuine profile import is verified for the captured schema, not arbitrary historical formats; no100% claim. No key, account or cloud service is required, but organizational signing policy can still prevent launch.

## Before submitting

Obtain separate submission authorization; review entrant/contact/team eligibility, chosen public version, all limitations and verified public links. A public Release does not submit the form. Record the actual confirmation/receipt only after a real authorized submission. Do not claim acceptance from this prepared text.


**Publication is complete; competition submission is not.** The approved release body/handoff assets retain their original preparation wording, as required; this current draft and the anonymous receipt identify the released version. The official article/form URL was checked, but the form remains inaccessible through available tooling. The owner must review entrant/contact/team information and explicitly authorize form submission; no acceptance or receipt is claimed.
