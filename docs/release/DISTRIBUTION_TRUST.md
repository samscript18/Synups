# Distribution trust and signing findings

Checked22 October2026. Public0.1.0 and frozen rc.3 archives are unchanged. Signing changes executable/archive bytes and requires a separately identified build and verification; none was silently applied.

## What is available now

- **Windows:** self-contained x64 ZIP, not an installer. No publisher signature or usable signing credential was found in the checked project/environment. The repository Actions-secret metadata currently lists zero repository secrets; this does not prove that an owner has no external certificate or organization-level service.
- **macOS:** arm64 `.app` inside tar.gz. Strict/deep ad-hoc signature integrity passes. It is **not Developer ID signed or notarized**; Gatekeeper assessment rejects it. A fresh local code-signing identity query found no Developer ID Application/Installer identity. No private keys were exported and no certificates were created or modified.
- **Linux:** self-contained x64 tar.gz with executable modes retained. System desktop/font/graphics libraries are still required. Hashes establish byte integrity, not publisher trust or compatibility with every desktop.

[Exact rc.3 packages/source/hashes](LEGACY_COMPLETION_CANDIDATE.md) · [Installation and removal](INSTALLATION.md) · [Runtime evidence](../migration/CROSS_PLATFORM.md)

## SignPath Foundation assessment

[Official eligibility conditions](https://signpath.org/terms) require an actively maintained, released, documented OSI-licensed project without proprietary components. Synups's MIT source, existing release and open dependencies appear compatible with those initial requirements; **this is an assessment, not acceptance**. Foundation approval also requires repository control/provenance review, MFA and assigned signing/review roles, an appropriate signing policy and manually approved signing requests. Upstream binaries have separate signing restrictions. The foundation decides eligibility, including its fork/ownership requirements.

No application was submitted, service connected, signing-policy sponsorship claimed or certificate issued. The owner should review eligibility and apply through [SignPath's official site](https://signpath.org/) if desired. Onboarding is not a deadline dependency, and approval must not be promised.

## Apple distribution

[Apple's Developer ID guidance](https://developer.apple.com/developer-id/) distinguishes publisher signing and notarization from ad-hoc integrity. Legitimate distribution requires the owner's eligible developer credentials, signing and notarization, followed by testing the actual newly signed archive. No such credential is available in the checked scopes. Existing signatures do not establish Apple approval.

## Safe installation behavior

Download only the intended source/release, verify its SHA256SUMS and retain the complete extracted package. On Windows, follow organizational policy and inspect publisher warnings. On Mac, **do not disable Gatekeeper, strip quarantine or fabricate a trusted-signing pass**. A user who independently trusts the source can follow Apple's documented per-app approval procedure when the OS/policy offers it; managed systems requiring a trusted signature should stop and ask their administrator, or build reviewed source under their own policy. [Apple's guidance for opening identified/unidentified developer apps](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac).

ZIP/tar builds do not install a system service, driver or cloud agent. Extract to a user-writable location. Remove the extracted app to uninstall; preserve/export local history first. Startup registration is an explicit separate action and must be removed explicitly if enabled. No security or real-user startup setting was changed during this assessment.
