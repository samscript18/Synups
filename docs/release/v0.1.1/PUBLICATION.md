# v0.1.1 publication checklist — rc.4 alignment

**GO to publish this disclosed unsigned portable build after separate authorization.** This is not a trusted-signing, complete-desktop-acceptance or competition-submission approval. No public Release/tag/form is created by this checklist.

- Proposed title: **Synups 0.1.1 — verified rc.4 build**.
- Proposed tag: `v0.1.1` at **6c812fde9a29eeb8cfd6399a2f1a75e333e81e03**, never the later documentation HEAD.
- Internal application version: **0.1.1-rc.4**, unchanged and disclosed in the release body. No rebuild, resign or version patch.
- Exact-source [CI](https://github.com/samscript18/Synups/actions/runs/39850198899), [candidate](../DESKTOP_COMPLETION_CANDIDATE.md), [release body](RELEASE_BODY.md), [upload manifest](ASSET_MANIFEST.json).
- Prepared directory: `artifacts/release-alignment/v0.1.1-rc4/`. The older rc.3 preparation under `artifacts/presentation/v0.1.1-ready/` is historical and **must not be uploaded** for this release.

The README inside each frozen archive is the original build-time documentation snapshot and still discusses the older rc.3 proposal (88.9%). It is intentionally preserved byte-for-byte; use the accompanying current RELEASE_NOTES.md, INSTALLATION.md and VERIFICATION.json for this rc.4 payload (91.7%). Application identity is established by Help/About and ARTIFACT_MANIFEST.json, both identifying rc.4 and its exact source.

## Exact files to upload

Upload all 15 files below from the prepared directory, no files from a different candidate. The manifest lists the 14 payload files plus the separate SHA256SUMS hash; SHA256SUMS covers those 14 files and cannot hash itself. The review manifest and this source upload list are not additional release assets.

```text
Synups-win-x64.zip
Synups-win-x64.zip.sha256
Synups-osx-arm64.tar.gz
Synups-osx-arm64.tar.gz.sha256
Synups-linux-x64.tar.gz
Synups-linux-x64.tar.gz.sha256
LICENSE
INSTALLATION.md
PUBLICATION.md
SUBMISSION.md
RELEASE_NOTES.md
SOURCE_REVISION.txt
VERIFICATION.json
VERIFICATION_EVIDENCE.zip
SHA256SUMS
```

## Before authorization / publication

1. Re-run `python3 docs/release/v0.1.1/verify_assets.py artifacts/release-alignment/v0.1.1-rc4` from the repository root. Require all asset hashes, archive CRC/full manifest/RID/CPU/native SQLite/permissions/licence/notices and exact source/version checks to pass. Never replace the frozen rc.4 input or output to fix a mismatch.
2. Review the unchanged internal rc.4 identification, 91.7% rubric and all three partial areas. Windows unsigned, Mac ad-hoc/not notarized and physical/login/OS-delivery blockers must remain visible. Do not claim universal judge launch on managed systems.
3. Obtain explicit authorization for this exact payload, tag commit and title. Recheck that `v0.1.1` tag and Release do not exist. If either exists or points elsewhere, stop and inspect; never overwrite, delete or force-push.
4. Create the tag at the exact application SHA above and the public GitHub Release using RELEASE_BODY.md verbatim, with these 15 assets. Keep public v0.1.0 and all candidate archives unchanged. Do not tag the documentation commit or upload the outer CI artifact ZIPs.

## After separately authorized publication

5. Resolve the published tag to the full application SHA. Anonymously download every asset through its public Release URL, compare SHA-256 and byte counts to ASSET_MANIFEST.json/SHA256SUMS, inspect archive integrity and verify Release visibility/title/body/version disclosure. Record actual URLs, UTC times and outcomes. A successful upload alone is insufficient.
6. Only after all anonymous checks pass, switch README, INSTALLATION, SUBMISSION and JUDGES_GUIDE public download links from v0.1.0 to v0.1.1; preserve the rollback link and signing/physical limits. Do not let draft expected URLs masquerade as working downloads.
7. Verify public before/after images, migration report, source-specific evidence and official entry form in an ordinary browser. The form is linked by the official article but inaccessible through the current HTTP/browser tools; no usable-form pass or receipt is claimed.
8. Obtain **separate competition-submission authorization**, review entrant/contact/team information and retain the actual receipt. A public Release is not a submitted entry.

Expected future URLs are proposals until verified:

```text
https://github.com/samscript18/Synups/releases/tag/v0.1.1
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS
```

NO-GO if source/hash/manifest checks fail, assets are mixed, the tag conflicts, authorization is absent or release disclosures are removed. Unsupported native-delivery/physical/login claims are not acceptable substitutes for evidence. Signing and optional feature work are outside this alignment task.
