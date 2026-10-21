# Reviewed publication handoff — authorization still required

No Release, tag or submission has been created by Phase 5. The user explicitly reserved final GitHub Release publication and the competition form for separate authorization. CI login/30-day artifacts do **not** meet permanent judge delivery. This remains a mandatory blocker until authorized publication and anonymous download verification.

## Frozen application bytes

Candidate source: **2973d3349fcb5af5639597920842288e6549bbd0**. [Successful native three-OS CI](https://github.com/samscript18/Synups/actions/runs/39682197118). Last production-source correction: `292b847bffa96d2a23d771c3ba0ce4648c06844b`; subsequent changes through this candidate are documentation/evidence only. Archives still differ because source/build metadata and bundled documentation differ. The new hashes supersede the old candidate index; old evidence remains unchanged.

The Phase 5 documentation/tool checkpoint may be newer than the application revision. Release the frozen reviewed archives, with the manifest's candidate revision as the tag target. Do not attach newly rebuilt documentation-checkpoint binaries and reuse this evidence.

## Reproduce the local handoff

Download the **complete outer artifacts** listed in [VERIFIED_ARTIFACTS.md](VERIFIED_ARTIFACTS.md), saving them as `win-x64.zip`, `osx-arm64.zip`, `linux-x64.zip` in a local ignored directory. Then, from the repository root:

```sh
python3 eng/prepare-release.py \
  --evidence docs/evidence/verification/2026-10-21-release-candidate.json \
  --artifacts artifacts/phase5/downloads \
  --output artifacts/phase5/final-release-candidate
```

The assembler requires a new destination, checks all outer/inner/file hashes, exact source/RID, licence bytes and actual apphost stage results, then copies original archives without rebuilding. It adds sidecars, consolidated `SHA256SUMS`, source revision, reviewed release/installation/submission notes and a verification ZIP of CI logs/TRX/reports/captures. It excludes temporary databases/settings and development binaries. Dependency notices remain inside each complete original archive. Supplemental instructions are newer than the bundled Phase 4B instructions; their own hashes are in `SHA256SUMS`.

Inspect all assets before publishing; verify the consolidated hashes with `shasum -a 256 -c SHA256SUMS` on macOS or `sha256sum -c SHA256SUMS` on Linux. On Windows compare each named file using `Get-FileHash -Algorithm SHA256`. Keep original CI downloads/logs separately; the verification ZIP is not a backup of user history.

## After explicit publication authorization only

Review [release text](RELEASE_BODY.md), signing/desktop limitations and [submission information](SUBMISSION.md). The proposed name/tag is `Synups 0.1.0` / `v0.1.0`. Stop if that tag/release already exists; do not overwrite it. An authorized operator with GitHub CLI can create the reviewed release with an explicit immutable target and all reviewed assets:

```sh
gh release create v0.1.0 \
  --repo samscript18/Synups \
  --target 2973d3349fcb5af5639597920842288e6549bbd0 \
  --title 'Synups 0.1.0' \
  --notes-file docs/release/RELEASE_BODY.md \
  artifacts/phase5/final-release-candidate/*
```

This command has **not** been executed. Publication is a separate approval; GitHub CLI is not installed in this environment. No draft/private asset is represented as public. Preserve every asset beyond CI expiration and do not edit binaries after checksumming.

After publication, use an unauthenticated client to fetch all three archives, sidecars, `SHA256SUMS`, installation/notes, `VERIFICATION.json` and verification ZIP; verify hashes and archive integrity again. Replace README's CI-only download paragraph with these real tested public links and retain signature warnings. Record actual HTTP status, source, hashes and release URL. Only then separately authorize and submit the competition form before the deadline. If a new binary correction is required, repeat exact-source CI/apphost/download audit and prepare a new immutable candidate instead of silently replacing these files.
