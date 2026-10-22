# Verified Synups 0.1.0 release assets

Frozen application source: **2973d3349fcb5af5639597920842288e6549bbd0**. [Successful run 39682197118](https://github.com/samscript18/Synups/actions/runs/39682197118). All three complete downloads independently audited on 2026-10-21. [Exact verification record](../evidence/verification/2026-10-21-release-candidate.json).

This supersedes the earlier 292b847 candidate index. Production code is unchanged between those revisions, but source metadata and bundled documents differ, so old archive hashes must not be reused. Later Phase 5 documentation commits do not replace these frozen application bytes.

**[Public Release](https://github.com/samscript18/Synups/releases/tag/v0.1.0)**: all 15 assets downloaded anonymously with HTTP 200; every hash matched the approved local files. [Publication record](../evidence/verification/synups-0.1.0-publication.json). No binary rebuild/replacement. [Consolidated checksums](https://github.com/samscript18/Synups/releases/download/v0.1.0/SHA256SUMS), [source record](https://github.com/samscript18/Synups/releases/download/v0.1.0/SOURCE_REVISION.txt), [verification evidence](https://github.com/samscript18/Synups/releases/download/v0.1.0/VERIFICATION_EVIDENCE.zip). Earlier CI artifacts are provenance, not required download access.

| RID | Public archive | SHA-256 |
|---|---|---|
| win-x64 | [Synups-win-x64.zip](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-win-x64.zip) | `94d39f3320ead8cd1b1620c2f29ab7887faca7920f352d0f3b470712a03ee2d6` |
| osx-arm64 | [Synups-osx-arm64.tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-osx-arm64.tar.gz) | `40cc8ccdcc8cfde7bea19e6c9759b0502313182e9c1b8d74f1b198a4e9cc3bcc` |
| linux-x64 | [Synups-linux-x64.tar.gz](https://github.com/samscript18/Synups/releases/download/v0.1.0/Synups-linux-x64.tar.gz) | `cf072e7b31c55e16ee39e544882453c926204338a86cfe6df7553b62b1477222` |

```text
94d39f3320ead8cd1b1620c2f29ab7887faca7920f352d0f3b470712a03ee2d6  Synups-win-x64.zip
40cc8ccdcc8cfde7bea19e6c9759b0502313182e9c1b8d74f1b198a4e9cc3bcc  Synups-osx-arm64.tar.gz
cf072e7b31c55e16ee39e544882453c926204338a86cfe6df7553b62b1477222  Synups-linux-x64.tar.gz
```

Windows is unsigned. macOS passes extracted deep/strict integrity verification but Gatekeeper rejects trust (exit 3); no Developer ID/notarization identity was available. Linux requires graphical/native libraries. See [installation](INSTALLATION.md), [publication handoff](PUBLICATION.md), [prepared release text](RELEASE_BODY.md) and [submission information](SUBMISSION.md). The assembler preserves these exact archives and creates consolidated checksums and retained CI verification assets.
