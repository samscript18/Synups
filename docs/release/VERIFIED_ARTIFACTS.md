# Verified candidate artifacts

Source: **292b847bffa96d2a23d771c3ba0ce4648c06844b**. [Successful run 39678991269](https://github.com/samscript18/Synups/actions/runs/39678991269). These hashes identify that exact reviewed candidate, not every later documentation build.

Each outer GitHub artifact includes reports/logs and a packages/ directory. Download the matching artifact, then use the inner archive and adjacent sidecar. A GitHub login may be needed; CI artifacts expire after 30 days. No public Release has been created.

| RID | GitHub artifact | Inner archive SHA-256 |
|---|---|---|
| win-x64 | [Artifact 12383576376](https://github.com/samscript18/Synups/actions/runs/39678991269/artifacts/12383576376) | `ef67c71cf06506f86a753db6ea38913fccb4077c6606c5637b54a2f1f2a0ee0a` |
| osx-arm64 | [Artifact 12381229816](https://github.com/samscript18/Synups/actions/runs/39678991269/artifacts/12381229816) | `bb213fef3bf9ee86d8e386bc88ca987c188f2075a12eb6f8e77bee2a877b10b9` |
| linux-x64 | [Artifact 12382769304](https://github.com/samscript18/Synups/actions/runs/39678991269/artifacts/12382769304) | `da5be6b28320031e570ac18bf7eedc23b12cf4a89b12a8d9cfd2a947299c9097` |

Inner checksums (run in a folder containing these exact archives):

```text
ef67c71cf06506f86a753db6ea38913fccb4077c6606c5637b54a2f1f2a0ee0a  Synups-win-x64.zip
bb213fef3bf9ee86d8e386bc88ca987c188f2075a12eb6f8e77bee2a877b10b9  Synups-osx-arm64.tar.gz
da5be6b28320031e570ac18bf7eedc23b12cf4a89b12a8d9cfd2a947299c9097  Synups-linux-x64.tar.gz
```

Windows is unsigned; macOS has an ad-hoc integrity signature only and Gatekeeper rejects trusted delivery; Linux needs native graphical libraries. See [installation](INSTALLATION.md), [candidate notes](RELEASE_NOTES.md) and [release gates](../migration/RELEASE_READINESS.md). Native signatures/checksums do not certify ordinary desktop interactions or override OS policy.
