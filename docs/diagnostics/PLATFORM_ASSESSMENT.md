# Platform and publish assessment

This is a repository assessment of the diagnostics development candidate, dated October 22, 2026. It introduces no build, runtime or physical-desktop certification. Exact candidate commands, counts and native observations belong to [TEST_REPORT.md](TEST_REPORT.md); a configured workflow is not a completed CI run. Historical public v0.1.1 evidence in [CROSS_PLATFORM.md](../migration/CROSS_PLATFORM.md) applies to its frozen source, not automatically to new diagnostics.

## Targets and evidence boundaries

The desktop project declares `win-x64;osx-arm64;linux-x64;linux-arm64;osx-x64`; the workflow currently schedules Windows, Apple-silicon macOS and Linux x64. Two additional architectures now have isolated build-only CI jobs and locked RID assets; their runtime execution still requires matching hardware. This document distinguishes that configured scope from completed evidence.

| Runtime identifier | Current source/pipeline choice | Diagnostics verification required before certification |
|---|---|---|
| `win-x64` | Declared RID; `windows-latest` matrix; self-contained directory publish | Candidate CI build/tests; extracted apphost; real ICMP and TTL path permissions, resolver/snapshot behavior; actual desktop interaction separately |
| `linux-x64` | Declared RID; `ubuntu-24.04` matrix; self-contained directory; Xvfb/Openbox executable checks | Candidate CI build/tests; native SQLite and display libraries; ICMP under actual unprivileged deployment policy; TTL-expired responses; native `ip` command; real desktop interaction separately |
| `osx-arm64` | Declared RID; `macos-15` matrix; self-contained app packaging | Local/CI candidate observations must retain exact host/SDK/revision; loopback is not multi-hop tracing certification; physical tray, Save-panel, display and login gaps remain separate |
| `linux-arm64` | Declared additional target with build-only CI job; no native execution | Explicit RID restore/publish, native dependency asset inspection, arm64 native runner/hardware, real display/SQLite/ICMP/DNS/snapshot and extracted-app checks; cross-compilation alone proves none of these runtime gates |
| `osx-x64` | Declared additional target with build-only CI job; no native execution | Explicit RID restore/publish and Intel native dependency inspection; native Intel macOS executable and network checks; emulation, if used, must be identified separately |

For every target, report build, deterministic tests, native provider execution, extracted executable execution, native UI launch and physical desktop interaction independently. A successful `dotnet publish` cannot stand in for the remaining columns. No Windows/Linux diagnostics runtime or extra-architecture execution is certified by this document.

## Providers and privilege limits

| Capability | Actual provider | Boundaries and unsupported behavior |
|---|---|---|
| ICMP quality | Existing .NET `Ping` through `IcmpProbe` | Explicit timeout and sample cap; permissions/support differ by OS policy. Empty payload supports the existing Linux utility fallback. Capability failures are typed; they do not count as completed packet-loss measurements. |
| ICMP path | .NET `Ping` with explicit `PingOptions` TTL/hop limit | Genuine ICMP only; no TCP/UDP tracing fallback. Native TTL-expired reply visibility and IPv6 behavior need OS-specific real-path evidence. Loopback proves a destination echo, not intermediate path discovery. Permission denial and unsupported APIs retain typed results and partial observations. |
| TCP connection | .NET socket connection to one approved host/port | Application port refusal, timeout and success are separate observations. No port range scanning or ping substitution. |
| Explicit DNS and optional PTR | Bounded DNS wire queries; session-scoped reverse cache | Configured resolver discovery can fail; explicit alternatives require recorded approval. Resolver and transport provenance belong in evidence. PTR failure preserves raw IP; PTR names do not establish host identity. |
| Interface snapshot | Read-only .NET `NetworkInterface` | Adapter/address/prefix/gateway/DNS enumeration catches individual failures. Synchronous OS enumeration is drained, so cancellation can wait for an in-flight native call. No settings changes. |
| Default-route snapshot | Existing `GatewayDiscovery`: Windows PowerShell `Get-NetRoute`; macOS `/sbin/route`; Linux native `ip` JSON | Bounded read-only child processes with cancellation/draining. Missing tools, denied access and unsupported providers remain explicit. Only default gateway candidates are collected: this is not a full route table or proof of destination routing. |
| Raw SYN / UDP probe | No production protocol-specific adapter in this candidate | Typed `Unsupported`; raw socket permission does not turn an unimplemented adapter into a supported feature. No security protection changes are requested. |

Intermediate nonresponse cannot establish forwarding loss. TTL response suppression, asymmetric paths, load balancing, filtering and rate limiting remain possible explanations. Windows/macOS/Linux privilege and actual multi-hop TTL behavior are verification questions, not universal support claims.

## Dependencies and trimming/AOT

Inspected project files target .NET 10. The desktop directly references Avalonia, Avalonia.Desktop, Fluent and Inter fonts at 12.1.4; Core references Microsoft.Data.Sqlite 10.0.13. The checked lock files include SQLitePCLRaw 2.1.13, SkiaSharp 3.119.4 and HarfBuzzSharp 8.3.1.3 with native assets. Per-RID native assets must be checked in each produced archive, including SQLite and graphics/text libraries. Presence of a managed dependency in a lock file is not proof that a runnable native asset was selected for a new RID.

No `PublishTrimmed` or `PublishAot` setting appears in the inspected projects, and the existing workflow publishes self-contained directories. The candidate uses runtime JSON serialization for persisted configuration/diagnostics/evidence, assembly metadata reflection, Avalonia resources and native libraries. These are concrete review surfaces for an AOT/trimming experiment; this assessment does not claim that any particular package is either compatible or incompatible.

Preserve the self-contained **untrimmed, non-AOT** publishing path for the candidate. Make its intended settings explicit in any future publish experiment, for example `--self-contained true -p:PublishTrimmed=false -p:PublishAot=false`; retain complete runtime/native dependencies, licences, manifest and checksums. No trimmed/AOT executable has been validated by this assessment. Never promote an experiment by suppressing linker/AOT warnings. Before supporting either mode, inventory reflection/serialization requirements, resolve warnings, verify every retained resource/native library, run full tests and exercise extracted executables on each native target. Keep experimental artifacts separate from the release candidate.

## Suggested CI extension

Isolated `linux-arm64` and `osx-x64` restore/build/publish jobs now use declared locked runtime assets. Start with build-only evidence if native runners are unavailable and label that limitation. Promote each to native execution only when the runner/hardware exists. Keep the existing three native jobs and untrimmed artifacts intact. For every target retain zero-warning Release output, dependency audit, deterministic tests, explicitly enabled networking/native provider tests, SQLite restart/upgrade checks, extracted executable checks, package/native-library integrity and source revision. Additional build targets do not authorize publication or imply physical desktop certification.
