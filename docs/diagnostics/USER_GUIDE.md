# Use the diagnostics alpha

Read [release readiness](RELEASE_READINESS.md) and [migration/rollback](SQLITE_MIGRATION.md) first. This candidate has separate version0.2.0-alpha.1 and does not replace publicv0.1.1. Start with an isolated `--data-directory /absolute/new/workspace` if you want to keep the older application and data immediately usable. Opening genuine version1 history creates a verified pre-migration backup and upgrades that copy to schema2.

## Investigate an endpoint

Open **Diagnostics**, or press Ctrl+6 (Command+6 on macOS). Opening the page or selecting a profile sends no probes. Choose Quick Check, DNS Focused, Deep Dive, Incident Response or Custom; review the selected tests and their limits. Enter an endpoint you are authorized to test and exactly one TCP port. Review/Run opens a confirmation containing the snapshotted target, protocols, resolver, timeouts, estimated probe count and incident link. Continue executes that approved snapshot; Cancel sends nothing.

DNS A/AAAA tests need a hostname. A literal IP forward-DNS test reports Unsupported rather than inventing an answer. Leaving the resolver blank selects the first discovered active-interface DNS resolver, shown in the result. An alternative must be a literal unicast IP and is disclosed in the confirmation. The explicit DNS adapter uses UDP; truncated answers report Unsupported because TCP retry is not implemented. System hostname resolution for TCP/ICMP is separate, bounded and governed by the OS.

Ping Quality measures only the selected sample window. Failed replies have no invented RTT. Population standard deviation uses successful samples; jitter is the mean absolute difference of adjacent successful samples, with a missed reply breaking adjacency. Cancel retains completed samples and incomplete attempt counts. The elapsed indicator uses monotonic time during execution; retained observation times are UTC.

Traceroute uses real ICMP TTL/hop-limit probes. An intermediate asterisk means no response, not proven forwarding loss. Optional hop-name resolution is off by default; enabling it discloses addresses through bounded PTR requests. Literal hop addresses remain when names are unavailable. Raw SYN and UDP reachability adapters are unavailable and are not presented as selectable protocols.

Progress, current phase and **Cancel diagnostics** remain available while a session runs. Monitoring has independent workers and controls. A diagnostic cannot recover an incident, change targets or restart monitoring. An OS call may need time to drain after cancellation; shutdown reports the limitation and retains ownership for retry rather than claiming instant native cancellation.

## Investigate an incident

Open **Activity and outages**, select a real incident and choose **Diagnose**. The linked incident ID is shown in Diagnostics. Enter an approved endpoint: a group name is not an address. Select/review a profile and confirm. Sessions are linked at creation; attaching an already completed standalone session is not implemented. A recovered incident retains its original diagnostic observations, even if recovery occurs during execution.

Saved sessions show the latest50 summaries. Open a selected session to see retained results without sending probes. Startup marks unfinished checkpoints Interrupted and preserves prior saved tests without assigning a fabricated completion or recovery. Samples from a test interrupted before its checkpoint may not survive a process crash.

## Baseline and export

Open a fully successful completed session, enter a baseline label and save it. Select a baseline and compare a viewed later session. Parameter/method/target incompatibilities and missing measurements remain Unknown. Deltas describe observations, not fault responsibility. Saved baselines/comparisons retain their source sessions. Deletion/management of old diagnostic history is not yet available; it shares the configured SQLite capacity.

Redaction is on by default. Preview evidence and inspect the exact text before export. Disabling redaction includes sensitive addresses, configuration and comments; changing it invalidates the preview. Select a local parent folder: export creates a new unique bundle containing `evidence.json`, `evidence.md`, `evidence.pdf` and `manifest.json`. Existing exports are not overwritten. JSON and Markdown preserve UTF-8; portable PDF visibly escapes non-ASCII text. All formats use the same detached evidence snapshot.

The manifest hashes actual artifact bytes with SHA-256. It detects later byte changes; it does not prove origin or prevent regeneration of hashes. Export works offline and sends nothing to third parties. Durable IDs and UTC timing remain in redacted evidence and can still reveal correlations. Inspect bundles before sharing them.

Capture, ASN enrichment, remote corroboration, scheduling, threshold rules, new charts, a separate compact window and package-manager installation are unavailable in this alpha. See the complete [31-feature matrix](FEATURE_MATRIX.md) for precise limitations and verification status.
