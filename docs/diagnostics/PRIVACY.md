# Diagnostics privacy behavior

Diagnostics are local, explicit network investigations. There is no runtime telemetry or automatic upload in this candidate. This statement does not mean a requested network test is offline: probes and resolver queries disclose information to the systems receiving them.

## What leaves the application

The confirmation preview identifies the endpoint, protocols, port, resolver, limits and estimated probe count. TCP sends a connection handshake without application data. Ping and path tests send empty-payload ICMP probes. An explicit forward DNS test discloses the requested hostname to the selected resolver. Hostname-based TCP/ICMP tests may also invoke the system resolver, whose cache and platform resolution policy apply independently of the explicit DNS adapter.

Using an alternative literal resolver requires explicit resolver consent in addition to target approval. The default explicit DNS adapter reads configured interface DNS and selects the first available resolver; it does not infer VPN split-DNS or application DoH policy. Verify the resolver shown in the result before interpreting disclosure or DNS differences.

Optional reverse DNS is off by default. When enabled, it discloses observed hop addresses through PTR questions to the configured or approved resolver. It uses finite timeouts and a session-scoped cache, keeps literal addresses on failure, and does not contact enrichment services. Resolver-side caching may still apply; direct querying does not bypass upstream caches.

There is no packet-capture provider, ASN lookup, remote probe service, diagnostic scheduler or new diagnostic threshold-alert delivery in this candidate. Disabled capabilities do not request elevation, collect packet payloads or call external providers. Existing monitoring notifications retain their separate existing configuration; diagnostic results do not silently enable them.

## What is stored

Local SQLite history can contain approved target names, addresses and ports; DNS answers and resolver addresses; ICMP replies and hop addresses/names; interface identifiers, addresses, gateways and DNS configuration; native default-route information; timestamps, methods, outcome/error text, consent/configuration/profile versions; incident links; and baseline/comparison evidence. Existing incident group names and comments can be included in an explicitly generated evidence preview. Saved custom profiles are separate local JSON files and may contain resolver configuration and user-provided labels.

History, configuration, profiles, logs, verified migration backups and exports are not encrypted by Synups. They inherit operating-system file protection. Protect the containing account and disk, and inspect all files before sharing them. A local backup is another sensitive copy, not a sanitization operation. SQLite freed pages, backups and other copies are not claimed to provide secure erasure.

Existing retention is opt-in and protects linked incidents. Confirmed session/baseline deletion is available. Old terminal standalone diagnostics participate in existing opt-in retention; incident links, unfinished checkpoints and baseline/comparison sources are protected. Diagnostic evidence shares the configured history capacity. Exports/backups and freed SQLite pages are not securely erased. See [stability](STABILITY.md) for capacity failures and recovery.

## Export review and redaction

Redaction is the default preview setting. Changing it invalidates the prior preview and requires a fresh preview before export. Redaction replaces targets, resolver/DNS names and addresses, hop names/addresses, interface identifiers/addresses/gateways/DNS/routes, provider limitation details, incident group/comments, free observation text and error text before the renderers receive the document. Session analysis and comparison text are also sensitive and must follow the same redacted preview.

Numerical metrics, UTC timestamps, units, result kinds, state, version/provenance and durable IDs remain useful evidence. They can still reveal timing, activity and correlation information. Redaction is not anonymization. User-authored labels and other retained metadata should also be reviewed; never assume an evidence bundle is safe for public release merely because Redacted is true.

An unredacted export explicitly includes sensitive network details. JSON, Markdown and PDF all derive from the selected preview. The manifest hashes the exact exported bytes; it does not encrypt data, establish authorship, or prevent someone from changing the evidence and regenerating hashes. No credentials or captured payloads are intentionally collected or embedded.

For sensitive vulnerability reports, follow [SECURITY.md](../../SECURITY.md) rather than posting history, topology, comments, logs or screenshots publicly.

System hostname lookups for TCP/ICMP are bounded by BoundedHostnameResolver (two native workers, no waiting queue). Caller cancellation can return promptly while the original OS lookup still owns its slot. Further lookups fail explicitly when those slots are occupied; literal IP requests avoid DNS. Shutdown also drains retained native tasks within the remaining seven-second target; a hung OS resolver keeps ownership and requires retry, rather than abandoning work. Direct DNS uses owned cancellable UDP and, after validated truncation with spare probe budget, one same-endpoint TCP socket under the original deadline. PTR remains UDP.
