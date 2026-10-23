# Synups 0.2.0-alpha.1 diagnostics development candidate

This independently versioned development branch preserves public v0.1.1 at application commit `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`. It is not a public release or competition submission. Review [RELEASE_READINESS.md](RELEASE_READINESS.md) and the complete [31-feature matrix](FEATURE_MATRIX.md) before using a candidate artifact.

The Diagnostics workspace appends a sixth navigation page. Review and explicitly confirm one endpoint, test selection, resolver, single TCP port and finite limits. TCP connect, explicit A/AAAA UDP DNS, bounded ICMP quality, genuine ICMP TTL path observations and local read-only interface/default-route snapshots execute separately from continuous monitoring. Unsupported raw SYN/UDP strategies are explicitly unavailable. Optional reverse DNS is off by default, bounded and uses raw-IP fallback. Failed probes do not manufacture incidents or resolve monitoring outages.

Diagnostic sessions/checkpoints, incident foreign keys, saved successful baselines and comparisons persist in additive SQLite schema v2. Opening a v1 archive first creates and verifies a closed SQLite backup in its `.backups` folder. Keep this backup to use v0.1.1 again in a separate workspace; the older application cannot open schema v2. Configuration/imported settings and original incident payloads are not rewritten by diagnostics. Linked incident sessions are protected from ordinary retention deletion; diagnostics share the configured database capacity and have confirmed FK-safe session/baseline deletion plus protected opt-in retention.

Evidence preview defaults to redaction. Each export writes JSON, Markdown, a paginated portable PDF and an exact-byte SHA-256 manifest to a new unique folder. Hashes detect later changes, not origin/authenticity. PDF uses visible Unicode escape sequences for non-ASCII characters; JSON and Markdown retain UTF-8. Export never transmits incident records.

Capture, ASN enrichment, remote tests, diagnostic scheduling, threshold rules, new historical charts, a separate compact window and package-manager submissions are not delivered by this candidate. In-app Diagnostics shortcut and tray navigation are present; physical native input, tray/notification delivery and global/configurable shortcuts have separate limitations. The unchanged legacy parity reference is 91.7%; new diagnostics do not inflate it.

Use isolated data until source-specific package/native/upgrade gates pass. All artifacts remain unsigned or locally ad-hoc signed as disclosed by the existing packaging path; no trusted signing or notarization is implied. No security protection bypass is recommended.

Final hardening adds bounded DNS TCP fallback after validated truncation, tie-safe history paging, partial results while running, and fault/large-export/concurrency coverage. See [hardening report](HARDENING_REPORT.md) for exact source-specific acceptance.
