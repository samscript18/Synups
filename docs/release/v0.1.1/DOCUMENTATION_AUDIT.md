# Documentation and release presentation audit — 23 October 2026

This is documentation-only work for public **v0.1.1**, unchanged application `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03` / internal `0.1.1-rc.4`. The separate alpha remains `3c4462a71cbb90032603cd3eaf4366c2561474d7` / `0.2.0-alpha.1`; paused expansion edits are not part of either verified snapshot. No application binaries were rebuilt, signed, uploaded or replaced. No alpha release or competition form was submitted.

## Public release description

The exact release is [v0.1.1](https://github.com/samscript18/Synups/releases/tag/v0.1.1), ID **421217596**, title **Synups 0.1.1 — verified rc.4 build**. Authenticated preflight verified repository write permission, exact tag target and every protected asset before changing **only `body`** through GitHub's release API. A fresh GET confirmed the saved description matches [the replacement Markdown](RELEASE_BODY.md) verbatim.

The body replaces prepublication wording with public status, adds direct downloads/quick-start steps, preserves archive hashes/history/limitations and explicitly separates diagnostics development. It retains Bill Menees/WirePeep MIT attribution. All **15 asset IDs, names, sizes, SHA-256 digests and upload states**, release title, publication/prerelease flags and tag object/target are identical before/after. The independent local frozen-asset verifier also passed archive contents, source/RID/version/native libraries/licences and executable modes; it never builds.

Before body SHA-256: `9d3a8a6b7d4567ce1dbb6390e870a3eaf45a29bb80fe8e31806413e921978783`.
After body SHA-256: `7e613f6e63b18026a5b4be04a9ab084bfde5eb1aeb97889ae19f774b3507ed5f`.

[Before snapshot](../../evidence/verification/2026-10-23-documentation-audit/release-before.json) · [after snapshot](../../evidence/verification/2026-10-23-documentation-audit/release-after.json) · [verification summary](../../evidence/verification/2026-10-23-documentation-audit/verification.json). Credentials stay in memory; no credential is retained in these receipts. Live asset hashes come from GitHub's SHA-256 digest metadata matched against the reviewed manifest; the original anonymous full-download verification is preserved separately. Frozen uploaded handoff text remains byte-identical and can contain historical prepublication wording.

## A2 — source-backed parity

[FEATURE_PARITY.md](../../migration/FEATURE_PARITY.md) now leads with all **18 original inventory rows**, each mapping original behavior/source to the actual Synups implementation, current score, preserved intent, changes, limitations and evidence. Source links pin the original upstream and public application. **(15 complete + 3 partially complete × 0.5) / 18 = 91.7%** is unchanged. Alerts, preferences and placement/tray remain partial for delivery/audibility, later-login and physical/multi-display gaps. [The concise summary](../../PARITY.md) uses the same score/statuses.

The technical-difficulty table explains the original coupling or platform issue, the implemented replacement and its verification. Genuine before/after images retain their source/provenance; original outbound-blocked rows are not called an ISP failure. Earlier phase scores and decisions are preserved in [FEATURE_PARITY_HISTORY.md](../../migration/FEATURE_PARITY_HISTORY.md), not presented as current. Diagnostics adds no legacy parity points.

## A5 — architecture

[Architecture](../../migration/ARCHITECTURE.md) explicitly separates released schema-1 monitoring and alpha schema-2 diagnostics. Five Mermaid diagrams describe overall boundaries, monitoring lifecycle, diagnostic lifecycle, incident-to-evidence flow and SQLite entities. The ER explanation distinguishes application-managed v1 GUID relationships from enforced v2 foreign keys.

The engineering-decision table covers alternatives already recorded in the repository, rationale, trade-offs, operational implications and evidence. It documents lifecycle ownership, sorted peer scheduling, monotonic grace/recovery, pending writes, serialized SQLite, UI propagation, real settings import and independent diagnostics. Reflection/JSON/native-resource considerations are assessed without claiming AOT support; signing and physical-platform gaps remain explicit. Accurate historical design detail is retained beneath the current snapshots.

## C2 — security and privacy

[SECURITY.md](../../../SECURITY.md) now separates snapshot trust boundaries and network traffic by feature. It covers literal monitoring targets, hostname/DNS/PTR disclosure, ICMP permissions, local plaintext storage/topology, backup/retention/deletion, redaction before rendering and the limits of hashes. It distinguishes local CSV from alpha evidence bundles, notification requests from delivery and metadata-only checksum verification from producer authentication.

Raw SYN/UDP reachability, capture, ASN/remote providers and scheduled diagnostics are explicitly unavailable/blocked/deferred in the frozen alpha. Privacy for absent providers is not claimed verified. Windows unsigned/macOS ad-hoc/non-notarized/Linux-environment limits and the existing private vulnerability-reporting channel/fallback remain accurate. [Alpha privacy](../../diagnostics/PRIVACY.md) is independently source-qualified. No OS security bypass is recommended.

## README and judging navigation

[README](../../../README.md) puts the public application, Migration at a Glance, downloads and quick navigation first; the alpha has its own later section. [JUDGES_GUIDE.md](../../competition/JUDGES_GUIDE.md) offers a five-minute path, genuine before/after images, A2/A5/C2 links, the unchanged parity calculation, exact public test counts and integration limits. It does not mix alpha screenshots, counts or package hashes into the public release. Cross-platform/readiness/log/evidence documents clearly identify historical checkpoints.

The default branch `feat/avalonia-migration` receives the public-facing documentation through an isolated worktree; alpha documentation links there explicitly cross to `feat/network-diagnostics`. The diagnostics branch keeps local alpha references. Neither branch's documentation changes modify application behavior. Paused application files remain byte-identical to their recorded starting hashes and are not staged in documentation commits.

## Checks and reproduction

- `python3 eng/check-documentation.py --output REPORT.json` checks all repository documentation links/images/anchors, required pages, 70 immutable source paths, the exact 18-row weights, attribution, release URL/version separation and unsupported-feature claims. Both branch variants pass; machine-readable final counts are retained with the receipts.
- `python3 -m unittest discover -s eng/tests -v`: **19 passed** on the diagnostics branch; **18 passed** on the public branch (the isolated multihop guard exists only on the diagnostics branch). New tests reject broken links/anchors, changed tag/asset hashes and inconsistent claims; existing archive/namespace tests still pass.
- `dotnet test Synups.slnx -c Release --no-build --no-restore`: existing compiled suite **243 passed, 0 failed, 8 gated skips / 251**. This does not rebuild binaries or certify the paused source edits; source-specific release/alpha CI remains the authority for those snapshots.
- `python3 docs/release/v0.1.1/verify_assets.py artifacts/release-alignment/v0.1.1-rc4`: all **15** reviewed assets passed; archive hashes and original MIT bytes unchanged.
- `git diff` and starting-file hashes verify no application behavior/source/binary change was introduced by this task. Documentation commits use `[skip ci]` to avoid triggering application rebuilds for this documentation-only request.

## Exact edited paths

Common public documentation/tooling:

- `README.md`, `SECURITY.md`, `docs/PARITY.md`.
- `docs/competition/JUDGES_GUIDE.md`, `docs/evidence/README.md`.
- `docs/migration/FEATURE_PARITY.md`, new `FEATURE_PARITY_HISTORY.md`, `ARCHITECTURE.md`, `CROSS_PLATFORM.md`, `MIGRATION_LOG.md`, `RELEASE_READINESS.md`.
- `docs/release/v0.1.1/RELEASE_BODY.md`, `PUBLICATION.md`, and this `DOCUMENTATION_AUDIT.md`.
- `eng/check-documentation.py`, `eng/audit-release-description.py`, `eng/tests/test_documentation.py`.
- `docs/evidence/verification/2026-10-23-documentation-audit/` receipts, check reports and checksums.

Diagnostics-branch documentation additionally updates `docs/diagnostics/ARCHITECTURE.md` and `docs/diagnostics/PRIVACY.md`. The frozen `ASSET_MANIFEST.json`, all 15 downloadable assets, original source/licence and both application snapshots remain unchanged.

## Remaining evidence limits

This task makes no new physical-desktop, runtime performance, security-certification, signing or binary-verification claim. The existing source-specific native evidence is reused with its qualifications. Mermaid diagrams are supplied as repository-native text; physical judge/browser rendering is not a new application acceptance test. GitHub's private-reporting route is offered when available, with a non-sensitive issue fallback; availability is not guaranteed for every account. The feature expansion remains paused.
