# Discovery and WirePeep settings investigation

**Current public release:** [v0.1.1](https://github.com/samscript18/Synups/releases/tag/v0.1.1), source6c812fde9a29eeb8cfd6399a2f1a75e333e81e03, internal0.1.1-rc.4,91.7% inventory. [Anonymous publication verification](../evidence/verification/synups-0.1.1-publication.json). Earlier unpublished/proposed-release statements below are historical. Remaining signing/physical/login/alert blockers are unchanged; competition submission is separate.

Current22 October2026 checkpoint: [reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md), application6c812fde9a29eeb8cfd6399a2f1a75e333e81e03,91.7% inventory/8.5 readiness. Earlier phase sections are historical. The user-selected proposed v0.1.1 payload is now frozen rc.4 at91.7%, with internal0.1.1-rc.4 version disclosed; public0.1.0 remains unchanged. Earlier rc.3 payload decisions below are historical.

Work date: 2026-10-16. These P2 items were investigated after the durable-history and desktop work. No discovery feature or importer is shipped in Phase 3.

## Discovery
Original `src/WirePeep.Common/Profile.cs` builds defaults and executes Find rules during construction. Gateway discovery calls `Pinger.GetAddressAtTtl(8.8.8.8, 1, wait)`, accepting Success or TtlExpired and using the reply address. This is a TTL-limited external ICMP request, not an authoritative OS default-route query. VPNs, filtering, multiple interfaces, IPv6 and platforms without usable TTL replies can change its result. It is synchronous and only bounded by the Ping wait; it has no cancellation.

Cable-modem discovery tries 192.168.100.1, 10.0.0.1 and 10.1.0.1 in order, stopping at the first successful ICMP response. Transceiver discovery tries 192.168.1.254. A response proves reachability, not device identity or that it is the user's modem/SFP. Original default wait is 50ms; failure can mean filtering or scheduling delay, not absence. These literal candidates come from the retained DefaultProfile.xml, not guessed scan ranges.

A future workflow should show exact requested probes or enumerate OS gateway candidates, require explicit user action, use finite cancellable IcmpProbe calls, preview candidates and add only on confirmation. Passive NetworkInterface gateway enumeration differs from the legacy TTL method and needs VPN/multi-interface validation; synchronous OS enumeration cannot be assumed immediately cancellable. There is no safe verified cross-OS discovery adapter in this session. Manual validated IP configuration remains fully functional; no startup discovery or arbitrary subnet scan occurs.

## Settings format
Original MainWindow calls `ApplicationInfo.CreateUserSettingsStore`, then accesses AppOptions and Profile nodes. Profile saves `PeerGroups/{group GUID}` and `Locations/{target GUID}` with properties in original PeerGroup.cs/Location.cs. Original application options include Windows paths and registry/login behavior that cannot be imported wholesale.

The restored Menees.Common 5.1.2 package documentation describes a hierarchical ISettingsStore; its assembly contains FileSettingsStore/FileSettingsNode. Inspected upstream library source at [36437da8d92525cb735db7b1ef71a82aff3f69e2](https://github.com/menees/Libraries/tree/36437da8d92525cb735db7b1ef71a82aff3f69e2/src/Menees.Common) shows `CreateUserSettingsStore() => new FileSettingsStore()`. FileSettingsStore represents nodes as `Settings` elements with Name attributes and `Setting` children with Name/Value attributes. Candidate names are application-domain-user `.stgx` files, searched beside the executable, under LocalApplicationData/Menees and under the temp Menees directory. This source revision is not asserted identical to the shipped 5.1.2 implementation; it corrects an assumption that the settings necessarily live in Registry. XML docs/package metadata and source were inspected read-only.

No actual Windows WirePeep user .stgx fixture or Windows runtime is available here. Therefore exact-version serialization, locale-sensitive TimeSpan values, missing/default nodes, window options and compatibility have not been validated. There is no hypothetical XML importer and no import UI that claims success. Original settings are never opened or modified by Synups.

Before implementing: capture a real fixture with the baseline Windows app and package version, redact only user-private values, preserve schema/IDs, test exact parsing and limits/DTD rejection, preview a diff, back up the Synups destination, and import profiles only after explicit confirmation. Reject duplicate/missing group references and unsupported timing values rather than silently dropping targets. Do not alter the original source file. This is a release parity blocker, not a completed feature.


## Final hardening follow-up

The earlier unimplemented-discovery assessment describes the previous release. Unpublished rc.2 now reimplements only useful default-gateway intent with explicit read-only OS route queries, candidate/interface preview and confirmed addition, not TTL probing or modem/transceiver candidates. All three native CI route queries pass; permissions/no routes/VPN/multiple routes/cancellation are typed and fixture-tested. No actual-fixture legacy importer, startup-registration integration or sound adapter was invented. See [exact reviewed scope](../release/FINAL_HARDENING_CANDIDATE.md).

## rc.3: original discovery intent and explicit login integration

The rc.2 paragraph above is historical. Frozen source 7ca85cccce6e3609d08d64e4f23172fa14fffb25 adds explicit original CableModem192.168.100.1/10.0.0.1/10.1.0.1 and Transceiver192.168.1.254 checks with original 50ms waits, ordered stop-on-success, finite overall cancellation, result preview and separate confirmation. Gateway confirmation can populate the original Local Router group; missing-default merge preserves IDs/configuration. Native route discovery replaces risky startup TTL probing. No range scan, automatic monitoring, device identity inference or real candidate response is claimed. Configuration failures leave manual IP setup available.

Explicit per-workspace login enable/remove exists for Windows HKCU Run, macOS LaunchAgents and Linux XDG autostart. Only confirmed user actions write registrations; normal opening/preferences save does not. Temporary fixtures and native Linux Gio.launch are tested; no actual login/Windows registry mutation is certified, so Preferences stays partial. Supported-path limits and frozen evidence: [rc.3 review](../release/LEGACY_COMPLETION_CANDIDATE.md).

No genuine Menees settings-store fixture was supplied or found in the available repository/evidence. Original serialization depends on the external store; Windows CI access is not an actual user's historical fixture. No hypothetical importer was added and original WirePeep data remains untouched. Settings persistence stays partial until a genuine fixture and compatible read-only preview/import can be verified.


## rc.4: actual original Windows fixture and validated profile import

The earlier no-fixture statements are historical. The untouched original executable ran on Windows with Menees.Common5.1.2 and emitted a genuine `.stgx` file in [run39840176062](https://github.com/samscript18/Synups/actions/runs/39840176062). The unmodified test-only fixture SHA-256 is37b66a46f1fffaee7912b2733d00d76ef9fb06589635bdb7f8b6e7020d0ed7c1. A later run39841203591 captures the populated original WPF window; each run's separately generated random IDs/fixture hash remain distinguished. Source files and upstream ancestry are unchanged.

rc.4 supports only the actual captured hierarchical schema with validated literal addresses, stable GUIDs and positive invariant whole-millisecond timings. Read-only bounded XML parsing rejects DTDs/duplicates/missing references/unsupported timing. UI preview and explicit confirmation replace only the Synups profile after preserving destination bytes in a unique backup; corrupt destination/source failures refuse replacement. No probing or OS preference import occurs. Actual packaged apphosts on Windows/macOS/Linux verify preview, confirmation,15 targets, stable IDs, source hash and backup, then restore their loopback profile before starting. The fixture is not packaged in production. This closes the previously documented profile-import gap without assuming arbitrary historical format compatibility. [Reviewed rc.4](../release/DESKTOP_COMPLETION_CANDIDATE.md).
