# Discovery and WirePeep settings investigation

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
