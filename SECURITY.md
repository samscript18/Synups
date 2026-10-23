# Security and privacy policy

This policy covers two explicit snapshots: **published v0.1.1**, application `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03` / internal `0.1.1-rc.4`, and the **separate diagnostics alpha**, `3c4462a71cbb90032603cd3eaf4366c2561474d7` / `0.2.0-alpha.1`. The alpha is not included in the v0.1.1 downloads. Paused, uncommitted expansion experiments are not verified capabilities. Consult [release evidence](docs/evidence/verification/synups-0.1.1-publication.json), [architecture snapshots](docs/migration/ARCHITECTURE.md) and [alpha hardening](https://github.com/samscript18/Synups/blob/feat/network-diagnostics/docs/diagnostics/HARDENING_REPORT.md).

## Report a vulnerability privately

Use [GitHub private vulnerability reporting](https://github.com/samscript18/Synups/security/advisories/new) when available for your account/repository access. Otherwise open a [non-sensitive issue requesting a private contact channel](https://github.com/samscript18/Synups/issues/new). Do not post exploit details, credentials, internal addresses/topology, history databases, comments, logs, identifying screenshots or unredacted evidence in a public issue. No response-time guarantee or independent security certification is claimed.

## Trust model

Synups runs locally under the signed-in user's OS permissions. There is no application account requirement, telemetry, automatic incident upload or cloud backend in either frozen snapshot. It is not a privileged service, security boundary against another administrator, or proof of Internet availability. The user chooses authorized network targets, protects local files and reviews exports. OS accounts, filesystem permissions and disk protection are prerequisites; Synups does not encrypt its history or evidence.

Published monitoring uses configured literal unicast IPs. Opening the application does not probe unless the user enabled monitoring on launch. Discovery checks require explicit review/confirmation. The alpha requires a separate confirmation of an exact diagnostic target, tests, port, resolver and finite budgets. Diagnostic workers do not change monitoring targets or incident recovery. ICMP may require OS capabilities/permissions; unsupported or denied operations remain typed outcomes. Synups never automatically elevates, disables a firewall/security control or changes routes to make a test pass.

## Network traffic and disclosure

| Capability | Snapshot | What leaves the process | Limits / what it does not establish |
|---|---|---|---|
| Continuous monitoring | v0.1.1 and alpha | Actual empty-payload ICMP echo to configured literal IPs | Finite timeouts, bounded group concurrency, rotating alternatives. ICMP nonresponse is not universal Internet downtime or firewall diagnosis. |
| Native gateway preview | Both | Read-only local OS route query; no probe merely to preview | OS policy/tool availability applies; results are candidates, not verified router identity. |
| Original modem/transceiver checks | Both | Explicit bounded ICMP to the original ordered candidate addresses after confirmation | No subnet scan, startup discovery or silent target addition. |
| ICMP quality | Alpha only | Bounded echo samples to the approved endpoint | Loss/jitter describe that window; missing RTT is not invented. |
| Traceroute | Alpha only | ICMP echoes with increasing TTL/hop limit | Intermediate suppression is not proven forwarding loss; actual physical/IPv6 support is not universally certified. |
| TCP connect | Alpha only | One connection to the selected resolved address and approved port; no application request/payload | Connect success is not application health; no port-range scanning or raw SYN adapter. |
| Explicit forward DNS | Alpha only | A/AAAA question to the shown configured or explicitly approved resolver; at most one same-endpoint TCP retry after validated UDP truncation | Shared deadline, probe cap and bounded response framing. Recursive resolver caches may apply; no DNSSEC validation, DoH or split-DNS policy inference. |
| Optional hop-name lookup | Alpha only; off by default | PTR questions disclose observed hop IPs to the configured/approved resolver | Session cache/task/query/time bounds; one representative per hop; failures retain literal IPs. PTR is not identity verification. |
| Hostname resolution for TCP/ICMP | Alpha when a hostname is entered | OS resolver traffic, even when the explicit DNS test is not selected | OS cache/resolution policy applies independently. Two retained native workers; cancellation may precede OS task completion. |
| Interface/default-route inspection | Alpha; gateway subset in public release | Read-only local API/command access | No network reconfiguration; default-route snapshot is not a complete routing table. |

In-app alerts are local presentation. Optional native notifications and sound requests are handed to OS facilities with their own settings; requesting them does not prove delivery/audibility. Notification text may expose a group name/status on the desktop. Existing login registration is explicit and reversible; actual next-login execution remains unverified.

## Local data and exposure

Public v0.1.1 stores configuration/preferences JSON, SQLite monitoring sessions, incidents, event timestamps, target/group identifiers, addresses, comments and derived logs. The alpha additionally stores diagnostic requests/consent, methods, UTC observations, outcome/error text, DNS resolver/answers/TTLs, hop addresses/names, interfaces/gateways/routes, successful baselines and comparisons. These can reveal internal topology, behavior and correlated activity.

History, logs, profile files, backups, screenshots and exports are **not encrypted by Synups**. Other processes with suitable account/administrator access can read or modify them. Safe path handling and application file ownership do not defend against a hostile OS administrator or a compromised account. Keep copies under appropriate OS protection; avoid shared folders when data is sensitive.

One owned SQLite connection and file lease serialize history work; `DELETE` journaling and `synchronous=FULL` support durable transactions but do not guarantee survival of every hardware/power failure. Corruption, unknown schema, competing ownership, busy/full or read-only storage fail visibly without authorizing a reset. Pending monitoring writes remain owned for retry. Startup marks interrupted observations without inventing recovery. The alpha checkpoints between returned tests; a crash can lose an in-flight test's unsaved samples.

Before the alpha upgrades schema 1 to schema 2, it creates and verifies a separate SQLite backup. Published v0.1.1 cannot read schema 2; rollback uses a **copy** of that preserved schema-1 backup in a separate workspace. CSV is not a database backup. [Migration/rollback](https://github.com/samscript18/Synups/blob/feat/network-diagnostics/docs/diagnostics/SQLITE_MIGRATION.md).

Retention is opt-in; keep-all is the default within configured storage capacity. Public retention protects unresolved/interrupted monitoring evidence. Alpha incident links and baseline/comparison references protect their source records. Confirmed alpha deletion respects foreign keys; deleting a baseline removes its comparisons, preserving source sessions. Eligible old terminal standalone diagnostics can be retained/removed under the opt-in policy. Freed SQLite pages are reused. **Deletion and retention are not secure erasure**: pages, backups, exports, OS snapshots and other copies can survive. Export folders are separate copies and are not removed by history deletion.

## Evidence generation, redaction and integrity

Published v0.1.1 offers chronological incident CSV and full-history backup; it does **not** contain the alpha's PDF/JSON/Markdown diagnostic bundles. In the alpha, one detached evidence snapshot is redacted before every renderer. Redaction is enabled by default and changing it invalidates the previous preview. Targets, resolver/answer/hop identifiers, interface/address/route details, incident group/comment and free observation/error/analysis/comparison text are replaced. UTC times, stable IDs, units and numerical metrics remain and may identify activity. Redaction is not anonymization; inspect the exact preview and all resulting files before sharing.

JSON uses Synups's ordinal sorted-member serialization, not claimed RFC 8785 canonicalization. JSON/Markdown retain Unicode; the portable PDF displays non-ASCII as explicit Unicode escapes. Export creates a unique staging folder, writes bounded JSON/Markdown/PDF files, hashes each file's **actual bytes**, writes a manifest and renames the complete folder. Ordinary cancellation removes staging; forced termination may leave an orphaned partial folder. No complete-export claim is made for it.

SHA-256 compares bytes with a chosen manifest. It **does not authenticate the producer**, encrypt data, or prevent an attacker replacing an artifact **and** its hash/manifest. Verify downloads against a trusted release/source channel. Independent alpha checks verified 17 export bundles and 687 PDF pages at the frozen source; they are scoped evidence, not a general security proof. [Exact audit](https://github.com/samscript18/Synups/blob/feat/network-diagnostics/docs/diagnostics/HARDENING_REPORT.md#final-evidence-export-audit).

## Advanced capabilities: actual frozen status

| Capability | Status in the verified alpha | Security implication |
|---|---|---|
| Raw TCP SYN | Unavailable / typed Unsupported | No raw packet adapter or automatic privilege acquisition is claimed. |
| UDP probing / UDP traceroute | Unavailable / typed Unsupported | DNS-over-UDP is implemented separately; it is not a UDP reachability/traceroute adapter. |
| Local packet capture | Blocked and disabled | No payload-safe capture adapter is verified; capture privacy protections are not claimed tested. |
| ASN/ownership enrichment | Deferred | No provider configured or production external lookup enabled. |
| Remote probes | Deferred | No third-party corroboration service is called. |
| Scheduled diagnostics | Deferred | No background diagnostic scheduler is enabled; the existing monitoring launch preference is separate. |

Per-run consent authorization for future development does not turn uncommitted code into a supported capability. The [31-feature matrix](https://github.com/samscript18/Synups/blob/feat/network-diagnostics/docs/diagnostics/FEATURE_MATRIX.md) distinguishes unavailable features from implemented-but-partially-verified behavior.

## Distribution and dependency security

- **Windows:** unsigned portable binaries, no trusted-publisher signature or installer; SmartScreen/organizational policy can block execution.
- **macOS:** ad-hoc bundle integrity only; no Developer ID or notarization. The retained distribution assessment records Gatekeeper rejection. Do not disable Gatekeeper, strip quarantine or bypass OS protection as an installation strategy.
- **Linux:** executable modes and native dependencies are retained; public runtime evidence is Ubuntu/X11/Xvfb/Openbox, not every distribution or Wayland session.
- **Dependencies:** pinned package versions/lock files, dependency auditing, warnings-as-errors and retained licences/notices support review; they do not prove absence of vulnerabilities. [Exact public-source CodeQL](https://github.com/samscript18/Synups/actions/runs/39851153694) had zero retained SARIF results; that result does not transfer to arbitrary later edits.

All 15 public v0.1.1 assets and its frozen tag remain protected; only the human-readable release description is editable in this documentation task. Verify the [public checksums](https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS), [source-specific asset manifest](docs/release/v0.1.1/ASSET_MANIFEST.json) and [installation guidance](docs/release/INSTALLATION.md). No diagnostics alpha publication or competition submission is implied.
