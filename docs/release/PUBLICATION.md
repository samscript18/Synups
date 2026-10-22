> Historical v0.1.0 materials. The current **unpublished** v0.1.1 proposal uses frozen rc.4; see the [aligned checklist](v0.1.1/PUBLICATION.md) and [release body](v0.1.1/RELEASE_BODY.md). Existing public v0.1.0 assets are unchanged.

# Synups 0.1.0 publication — completed

The user explicitly authorized publication of the frozen reviewed assets. **[Synups 0.1.0](https://github.com/samscript18/Synups/releases/tag/v0.1.0)** was published at `2026-10-21T22:34:54Z`. Tag `v0.1.0` points exactly to 2973d3349fcb5af5639597920842288e6549bbd0. All 15 public assets were downloaded without authentication; SHA-256 and archive integrity checks passed. [Exact publication/download record](../evidence/verification/synups-0.1.0-publication.json). No rebuild/replacement, force-push, security bypass or competition submission occurred.

## Frozen application bytes

Candidate source: **2973d3349fcb5af5639597920842288e6549bbd0**. [Successful native three-OS CI](https://github.com/samscript18/Synups/actions/runs/39682197118). Last production-source correction: `292b847bffa96d2a23d771c3ba0ce4648c06844b`; subsequent changes through this candidate are documentation/evidence only. Archives still differ because source/build metadata and bundled documentation differ. The new hashes supersede the old candidate index; old evidence remains unchanged.

The Phase 5 documentation/tool checkpoint may be newer than the application revision. Release the frozen reviewed archives, with the manifest's candidate revision as the tag target. Do not attach newly rebuilt documentation-checkpoint binaries and reuse this evidence.

## Historical handoff preparation

Download the **complete outer artifacts** listed in [VERIFIED_ARTIFACTS.md](VERIFIED_ARTIFACTS.md), saving them as `win-x64.zip`, `osx-arm64.zip`, `linux-x64.zip` in a local ignored directory. Then, from the repository root:

```sh
python3 eng/prepare-release.py \
  --evidence docs/evidence/verification/2026-10-21-release-candidate.json \
  --artifacts artifacts/phase5/downloads \
  --output artifacts/phase5/final-release-candidate
```

The assembler requires a new destination, checks all outer/inner/file hashes, exact source/RID, licence bytes and actual apphost stage results, then copies original archives without rebuilding. It adds sidecars, consolidated `SHA256SUMS`, source revision, reviewed release/installation/submission notes and a verification ZIP of CI logs/TRX/reports/captures. It excludes temporary databases/settings and development binaries. Dependency notices remain inside each complete original archive. Supplemental instructions are newer than the bundled Phase 4B instructions; their own hashes are in `SHA256SUMS`.

Inspect all assets before publishing; verify the consolidated hashes with `shasum -a 256 -c SHA256SUMS` on macOS or `sha256sum -c SHA256SUMS` on Linux. On Windows compare each named file using `Get-FileHash -Algorithm SHA256`. Keep original CI downloads/logs separately; the verification ZIP is not a backup of user history.

## Historical reviewed publication command

Review [release text](RELEASE_BODY.md), signing/desktop limitations and [submission information](SUBMISSION.md). The proposed name/tag is `Synups 0.1.0` / `v0.1.0`. Stop if that tag/release already exists; do not overwrite it. An authorized operator with GitHub CLI can create the reviewed release with an explicit immutable target and all reviewed assets:

```sh
gh release create v0.1.0 \
  --repo samscript18/Synups \
  --target 2973d3349fcb5af5639597920842288e6549bbd0 \
  --title 'Synups 0.1.0' \
  --notes-file docs/release/RELEASE_BODY.md \
  artifacts/phase5/final-release-candidate/*
```

GitHub CLI was unavailable, so publication used the authenticated GitHub API after explicit user authorization: verified tag/release absence, created the exact tag, staged a draft, uploaded all 15 unchanged assets, compared GitHub SHA-256 values, then made the release public. The command above remains an illustrative equivalent; **do not rerun it or replace published assets**. Original archive/checksum bytes remain unchanged. Supplemental handoff documents inside published assets intentionally retain pre-publication wording.

The completed independent verification used an unauthenticated client to fetch all three archives, sidecars, `SHA256SUMS`, installation/notes, `VERIFICATION.json` and verification ZIP. All hashes and archive integrity checks passed. README now uses these verified permanent links and retains signing warnings. Actual status/source/hashes/URL are recorded. Competition submission remains prohibited until separately authorized. If a new binary correction is required, repeat exact-source CI/apphost/download audit and prepare a new immutable candidate instead of silently replacing these files.
