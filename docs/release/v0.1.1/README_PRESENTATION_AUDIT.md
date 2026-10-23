# Judge-first README presentation audit — October 23, 2026

Scope: documentation, source measurements and documentation checks only. Public v0.1.1 remains internal 0.1.1-rc.4 at `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`. The prior [documentation audit](DOCUMENTATION_AUDIT.md) and all historical runtime evidence remain unchanged. Development diagnostics are separate; paused application changes were preserved.

## Presentation changes

The README now leads with one value proposition and the genuine side-by-side comparison, then the four migration answers, migration table, direct downloads/installation, capabilities, three-platform execution summary, limitations and detailed documentation. Duplicate overview images and repeated adjacent parity/migration sections were removed. No accurate legacy evidence was replaced with promotional claims.

The four answers distinguish measured work from hours, inherited semantics from redesign, and observed engineering failures from general claims. [Source measurement method and exact Git range](../../migration/MIGRATION_MEASUREMENTS.md) prove **39 commits**, **4,374 original / 4,469 new production physical C#/XAML lines**, and **2,756 new test lines** separately. Third-party icon artwork and generated wrappers are excluded consistently; comments and blanks remain in physical counts. These figures are not effort or rewrite percentages.

README before this pass: **161 lines / 1,998 whitespace-delimited words / 18,289 UTF-8 bytes**. After: **86 lines / 1,407 words / 12,953 bytes**, measured with `wc -l -w -c README.md` on the diagnostics branch. Public-default README retains the same line/word counts: **18,561 → 13,089 bytes**, with explicit cross-branch alpha URLs. The first 90 seconds prioritize comparison, migration substance and download discovery; the deeper evidence remains linked.

## Visual evidence

**Five unique existing screenshots**, each shown once in the README, retain exact original bytes. [Per-image platform/revision/context/provenance](../../competition/SCREENSHOTS.md) and [hash manifest](../../evidence/verification/2026-10-23-readme-presentation/screenshots.json) cover original Windows, extracted rc.4 Windows overview/setup/import action, and the original rc.3 Linux incident timeline. No screenshot was fabricated, replaced or newly recorded; no optional GIF was produced.

A complete original-settings preview screenshot does **not** exist among the retained captures: the import-named image shows a preview-loaded banner above gateway controls. The README labels this exactly; native action reports verify the behavior separately. The incident remains a source-component rc.3 demonstration, not rc.4 packaged runtime evidence.

## Verification and protected state

[Documentation link/image/anchor/source/version report](../../evidence/verification/2026-10-23-readme-presentation/documentation-check.json) and [validation summary](../../evidence/verification/2026-10-23-readme-presentation/validation.json) record exact check counts, source integrity, package hashes and release comparison. All **22 diagnostics-branch / 21 public-default documentation/safety tests** passed, including focused measurement tests, without compiling or rebuilding the application. Local checks found **zero broken links** across **832 / 773 targets**, checked **62 image references** on each branch and **70 pinned source paths**; **12/12 external HEAD checks** passed. Native test numbers in README are the retained public-source CI results, not a new execution claim.

README, judge’s guide, parity audit, architecture and live release description distinguish public v0.1.1 / internal rc.4 from development v0.2.0-alpha.1, and compiled, executed and physically accepted behavior. The already-correct public release body needs no edit. External-link checks cover README/judge/selected-provenance web destinations; protected release downloads are separately covered by live asset metadata and the retained full public-download receipt. Failures, if any, are reported rather than silently treated as passes.

No production source edits by this pass; no executable rebuilds/changes, image replacements, release asset uploads, tag changes, new release publication or competition submission. Local frozen release archives were verified read-only. Windows remains unsigned, macOS lacks Developer ID/notarization, physical desktop/native notification acceptance remains incomplete, and broader IPv6/VPN/environment validation remains limited. Weighted legacy parity stays **91.7%**.
