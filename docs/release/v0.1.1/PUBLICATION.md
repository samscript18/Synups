# Proposed v0.1.1 publication checklist — authorization required

This plan does not create a tag, draft/public release or submission. Current public links remainv0.1.0. Proposed title: **Synups0.1.1 — verified rc.3 build**. Proposed tag commit: **7ca85cccce6e3609d08d64e4f23172fa14fffb25**; never tag a later documentation/integration commit by accident.

Payload: byte-identical reviewed rc.3 archives. Internal product version remains **0.1.1-rc.3**; that fact must be approved/disclosed, not patched. [Release description](RELEASE_BODY.md) · [Asset manifest](ASSET_MANIFEST.json). No archive is rebuilt, re-signed or overwritten.

1. Review the release body, exact source, three platform run, signing/physical-acceptance limits and manifest. Obtain explicit publication authorization for this payload/tag/title.
2. Verify every prepared asset against SHA256SUMS; recheck archive/manifest/licence/RID/runtime/permissions. Confirm the proposed tag/release does not exist; if it does, stop and inspect rather than replace assets.
3. Create the tag at the exact application source above and publish only the approved reviewed assets. Never overwrite public0.1.0 or any existing candidate.
4. Anonymously download every public asset and recheck all SHA-256 values. Check actual Release/title/tag links and source commit.
5. Only after successful anonymous verification, update README/submission download links fromv0.1.0 tov0.1.1. Preserve all signing and internal-version disclosures and record results.
6. Competition form submission is a **separate authorized action**, requiring entrant/team/contact review and actual receipt. A public Release is not submission.

Expected future URLs below are **proposals, not working-download claims**:

```text
https://github.com/samscript18/Synups/releases/tag/v0.1.1
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-win-x64.zip
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-osx-arm64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.1/Synups-linux-x64.tar.gz
https://github.com/samscript18/Synups/releases/download/v0.1.1/SHA256SUMS
```
