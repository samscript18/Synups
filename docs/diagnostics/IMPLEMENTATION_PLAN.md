# Diagnostics implementation plan

Starting branch: `feat/avalonia-migration`. Starting HEAD: `bbd92ef7395ae4cd20534920398b317353e41371` (clean).
Protected public application: `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`, v0.1.1, internal 0.1.1-rc.4. Publication evidence is in `docs/release/v0.1.1/`; no local tags exist. Development branch: `feat/network-diagnostics`. Rollback: return to the starting branch/commit; never overwrite release assets. New diagnostics schema is additive and must be backed up before upgrading genuine user data.

## Reconnaissance

Three projects, net10.0, SDK 10.0.402, Avalonia 12.1.4, Microsoft.Data.Sqlite 10.0.13. Explicit composition, no DI container. Monitoring owns independently bounded ICMP workers. GroupMonitor semantics are characterized by linked-original tests; leave those algorithms intact. MainWindowViewModel uses partial files and typed observable rows; MainWindow uses asynchronous event handlers and native storage pickers. HistoryStore owns a lease, one serialized off-thread connection, user_version=1, capacity policy, backup, opt-in retention and interrupted monitoring-session recovery. Config and preferences are distinct versioned atomic JSON files. History is read in bounded pages. Native route discovery already has read-only bounded process adapters. Tray, login, sound and notifications already isolate platform integrations. Existing Actions have native Windows/Linux/macOS package tests, separate real loopback, namespace incident tests and coverage. Frozen artifact evidence must not be attributed to this candidate.

SDK is installed at /private/tmp/synups-dotnet, outside PATH. Original sandbox build failed because Avalonia build services writes outside the workspace; rerun with approved build access. Baseline Release build: zero warnings/errors. Original suite: 164 total, 159 passed, 5 explicitly gated skips, 0 failures. Exact baseline TRX retained in artifacts/diagnostics/baseline.

## Dependency order and risks

1. Establish regression baseline; preserve source and release checksums (critical).
2. Typed diagnostic contracts, validation, bounded separate runner and additive SQLite migration using existing worker/lease/capacity/backup (critical). Persist partial results between calls, never hold a transaction during networking.
3. Real TCP, explicit bounded DNS wire adapter, ICMP quality, native configuration, ICMP TTL path, reverse DNS cache and cautious analysis. Independent adapters/tests may be delegated behind stable contracts as explicitly requested (high).
4. Dedicated workspace, visible parameter preview, confirmation, cancellation, progress, history and incident association; monitor remains independent (high).
5. Immutable redacted evidence JSON/Markdown/PDF and SHA-256 manifests; baselines and compatible comparisons (high). Gate advanced work on real core workflow results.
6. Document every remaining advanced/UX/distribution feature independently. Disable any capability that cannot meet privacy, native or release gates. Never pretend a stub implements a protocol (high).
7. Regression suite, deterministic network fixtures, opt-in native tests, package smoke, evidence, architecture/privacy/stability, honest GO/NO-GO (critical).

No new dependency planned initially. Reuse SQLite, .NET sockets/Ping, existing route providers and Avalonia controls. DNS parser requires strict bounds and fixture tests; PDF needs pagination, text bounds and artifact parsing verification. Advanced capture, enrichment, remote providers, physical desktop acceptance and additional architecture certification require separate evidence; do not claim completion from compilation.

## Traceability

The complete 31-item matrix lives in [FEATURE_MATRIX.md](FEATURE_MATRIX.md) and is updated with exact production/UI/test paths and limitations. Only VERIFIED means all feature acceptance gates passed; platform-untested paths stay PARTIALLY_VERIFIED. Essential end-to-end gaps mean NO-GO regardless of feature count.
