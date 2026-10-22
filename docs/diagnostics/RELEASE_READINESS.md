# Diagnostics release readiness

**CONDITIONAL GO for Synups0.2.0-alpha.1's isolated core diagnostics candidate**, application commit `e16e3cd4209ec12bf2150a194373c30efcdf744f`.

All six source-specific [CI jobs](https://github.com/samscript18/Synups/actions/runs/39869158253) passed. Windows x64 and macOS arm64 each passed225/233 deterministic cases with8 gated skips; Linux x64 passed226/233 with7 gated skips. Each passed6 separately enabled native network checks and all six extracted-executable stages, including genuine diagnostics, exports, process interruption, read-only storage failure and restart. Linux additionally passed the controlled real ICMP incident→Diagnose→preserve→recovery→restart→export workflow. New Core line coverage is86.20%. Additional linux-arm64/osx-x64 targets compiled/published; their runtime is untested.

Downloaded CI artifacts and their package manifests/source identity/checksums were verified independently. Eleven redacted export bundles and all29 PDF pages passed independent parse/bounds/hash checks; representative rendered pages were visually inspected. Genuine copiedv0.1.1 incident and macOS data upgrades preserved original data and created verified original-schema backups.

This is **not completion of the full31-feature expansion**. The [matrix](FEATURE_MATRIX.md) conservatively reports4 VERIFIED documentation features,19 PARTIALLY_VERIFIED,1 BLOCKED,7 DEFERRED. Core native scenarios have direct evidence; broader feature scopes, multi-hop/IPv6/physical input, complete fault simulation, raw protocol adapters, capture/external/scheduler/alerts/charts and package-manager work are not claimed production-ready. Unsupported functionality remains unavailable.

The [final report](FINAL_REPORT.md) records exact source revisions, all31 rows, observed demonstrations, separate OS results, package hashes, regression safety and remaining risks. [TEST_REPORT.md](TEST_REPORT.md) preserves exact commands and failures/fixes. Physical desktop input/native picker/notification/login certification and trusted signing/notarization remain separate gates.

Publicv0.1.1 remains untouched and available as the competition/release baseline. All15 protected assets retain their verified hashes. No public candidate release, external package submission or competition entry modification occurred. Any publication or entry change requires separate authorization for the concrete reviewed payload.

Rollback uses unchanged publicv0.1.1 and a COPY of the automatically verified pre-migration schema1 backup, plus original configuration/preferences, in an isolated workspace. The older application cannot read schema2. Never overwrite the only data copy. See [SQLITE_MIGRATION.md](SQLITE_MIGRATION.md).
