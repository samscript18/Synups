# Security policy

Synups is a local-first network monitor with a diagnostics development candidate. Public release identity and artifact evidence are recorded in the release documents; candidate source changes do not replace or certify those artifacts. Windows, macOS and Linux are intended targets, but native APIs, permissions, architecture, desktop integration and packaging must be accepted for each exact candidate. No general platform certification or response-time guarantee is implied.

To report a suspected vulnerability, use [GitHub private vulnerability reporting](https://github.com/samscript18/Synups/security/advisories/new) when available. Otherwise open a [non-sensitive issue requesting a private contact channel](https://github.com/samscript18/Synups/issues/new). Do not publish exploit details, credentials, private topology, history databases, comments, logs, identifying screenshots or unredacted evidence bundles.

## Network actions and privilege

Monitoring, gateway discovery and diagnostic execution are explicit user actions. A diagnostic confirmation identifies the target, tests, limits, port and resolver. Only investigate endpoints you are authorized to use. Request validation enforces finite probe/session limits and explicit consent for an alternative DNS resolver. Diagnostics have separate bounded workers; they do not silently start monitoring or change incident recovery.

Implemented diagnostics use real TCP connect, explicit UDP A/AAAA DNS queries, ICMP echo quality/TTL path probes and read-only local interface/default-route inspection. TCP sends no application request. The DNS adapter reports the resolver actually queried, validates bounded wire responses, and performs at most one bounded TCP retry to the same resolver after validated UDP truncation, within the original deadline and approved probe cap. Default resolver selection does not infer VPN split-DNS policy. Optional bounded PTR hop lookup is off by default; PTR names are not verified identity. Hostname-based tests may use the system resolver and disclose the target to it.

Raw TCP SYN and UDP diagnostic kinds are Unsupported. Packet capture, ASN enrichment, remote corroboration, diagnostic scheduling and new diagnostic threshold alerts are not implemented. No capture privilege, automatic elevation or external-service credential is requested by those disabled capabilities. Native default-route discovery uses fixed read-only commands with bounded execution; it does not scan, change routes or change settings. Runtime telemetry and automatic diagnostic uploads are absent from this candidate.

ICMP failure does not establish Internet downtime, forwarding loss or firewall policy. Probe errors never imply recovery. Timeout and cancellation preserve unknown measurements. Some native enumeration/resolution calls cannot be guaranteed to interrupt immediately; owned work must drain. Diagnostic shutdown reports a retryable failure if its seven-second wait expires instead of claiming the worker was released.

## Local evidence and storage

Configuration, SQLite history, saved profiles, comments, logs, backups and exports can contain sensitive network information. They are local files and **are not encrypted by Synups**. Protect them with OS accounts, file permissions and appropriate disk protection. Redaction is the default export preview, but retained timestamps, IDs, metrics and metadata can remain identifying; inspect every artifact before sharing it. Deliberately selecting an unredacted preview includes network details.

JSON, Markdown and PDF derive from the same evidence preview. JSON uses a documented sorted-member serialization algorithm, not claimed RFC 8785 canonicalization. Portable PDF renders non-ASCII as visible Unicode escapes without installed fonts. The manifest hashes actual exported bytes with SHA-256. Hashes detect disagreement with that manifest; they do not prove origin, encrypt content or stop an attacker regenerating both evidence and hashes. Exports are written in a unique staging directory and published only on completion; forced termination may leave a partial staging directory.

Existing v1 history receives a SQLite backup verified with `quick_check` before the diagnostics schema migration. Migration, corruption, permission and full-disk errors do not authorize resetting history. Session checkpoints are saved between returned tests; a crash can lose the in-flight test's unsaved samples. Restart marks unfinished saved checkpoints Interrupted without inventing completion or recovery. Existing opt-in retention protects diagnostic-linked incidents. Confirmed diagnostic session/baseline deletion respects foreign keys; baseline deletion also removes its comparisons and preserves source sessions. Existing opt-in retention removes only eligible old terminal standalone diagnostics, protecting unfinished and referenced records. Diagnostics consume shared configured history capacity. CSV/evidence exports are not full database backups, and deletion is not claimed to securely erase SQLite pages or copies.

## Distribution and dependencies

Existing Windows builds are unsigned and have no publisher authentication; candidate artifacts require their own documented signing/trust assessment. macOS ad-hoc signatures provide integrity without Developer ID/notarization or a trusted Gatekeeper assessment. Do not disable operating-system protections to test a build. Optional notification requests are distinct from actual delivery and do not prove alert acceptance.

Dependencies remain pinned with lock files and NuGet auditing; these diagnostics introduce no new package dependency. The solution uses warnings as errors. A CodeQL/CI workflow definition is not a passed scan or proof of absence of vulnerabilities. Findings, native acceptance limits and candidate evidence must remain visible.

See [diagnostics privacy](docs/diagnostics/PRIVACY.md), [stability and recovery](docs/diagnostics/STABILITY.md), and [architecture](docs/diagnostics/ARCHITECTURE.md) for the implemented boundaries. Consult [the feature matrix](docs/diagnostics/FEATURE_MATRIX.md) and current release evidence for verification status.
