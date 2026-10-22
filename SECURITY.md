# Security policy

Synups is an early, local-first network monitor. The current public release is 0.1.0; unpublished candidates are separately identified in the release documents. Security fixes are best effort; no support or response-time guarantee is implied.

To report a suspected vulnerability, use [GitHub private vulnerability reporting](https://github.com/samscript18/Synups/security/advisories/new) when available. If that option is unavailable, open a [non-sensitive issue requesting a private contact channel](https://github.com/samscript18/Synups/issues/new). Do not put exploit details, credentials, private IP topology, history databases, logs or identifying screenshots in public issues.

Monitoring and gateway discovery are explicit user actions. Discovery reads native routing information with fixed commands and bounded execution; it does not scan, change routes or start monitoring. Only monitor addresses you are authorized to use. ICMP failure does not establish Internet downtime, and probe errors never imply a recovery.

Configuration, SQLite history, comments, backups and exports can contain sensitive network information. They are local files, **not encrypted by Synups**; protect them with OS accounts, file permissions and appropriate disk protection. CSV is an evidence export, not a full database backup. Review data before sharing it. Repair pending write errors before closing; forced termination can lose uncommitted evidence.

Windows builds are unsigned. macOS ad-hoc signatures provide integrity only, without Developer ID/notarization or a trusted Gatekeeper assessment. Do not disable operating-system protections to test a build. Optional OS notification requests are distinct from actual delivery, and Windows retains an in-app fallback.

Dependencies are pinned with lock files and restored with NuGet auditing enabled. The Synups solution uses warnings as errors. The CodeQL workflow analyzes that solution and retains SARIF; a workflow definition alone is not a passed scan or proof of absence of vulnerabilities. Findings and genuine verification limits must remain visible in the migration evidence.
