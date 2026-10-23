# Diagnostics stability and recovery

This is a description of implemented behavior and known limits, not a statement that every failure mode has passed native acceptance. Feature/test/platform status is recorded separately in [FEATURE_MATRIX.md](FEATURE_MATRIX.md). Existing release evidence does not certify this diagnostics candidate.

The runner bounds concurrent diagnostic sessions and rejects excess work. Target approval, finite probe/session limits and alternative-resolver consent are validated before execution. Timings use monotonic elapsed measurement, while evidence timestamps use UTC. Timeout, cancellation, Unsupported and permission-denied outcomes are distinct from successful measurements. Missing probes do not become zero RTT or invented loss samples.

Cancellation propagates to owned socket/PTR/ICMP operations and adapter loops. Some OS interface enumeration and native resolution behavior cannot be guaranteed to interrupt immediately. The owned operation must drain; cancellation is not a universal 250 ms guarantee. Runner shutdown waits up to seven seconds for owned diagnostic tasks; if an operation remains, shutdown reports an error and must be retried. A timed-out shutdown does not mean the worker was detached or disposed successfully.

## Failure modes

| Failure | Detection | Visible evidence or effect | Recovery and limit |
|---|---|---|---|
| Invalid target, consent or limits | Request validation before execution | Validation/confirmation error; no diagnostic session starts | Correct the exact target/parameters and confirm again |
| Global or same-target capacity occupied | Runner admission checks | Busy/already-running error | Wait for or cancel the existing run; no pending queue is accumulated |
| DNS silence or unreachable resolver | Finite UDP deadline or socket error | Timeout, Unreachable or NetworkError; no answer invented | Check resolver/reachability; rerun explicitly |
| DNS refusal/NXDOMAIN/SERVFAIL | Validated response RCODE | Actual resolver, RCODE and typed outcome | Investigate configuration; negative replies are observations, not outage cause |
| Malformed or truncated DNS | Bounded transaction/question/name/record validation or TC flag | MalformedResponse for invalid data; one TCP retry for validated UDP TC | Same approved resolver and total deadline; cap exhaustion or repeated TCP TC remains Unsupported |
| PTR failure, timeout or capacity | Finite lookup budget and bounded cache/task admission | Literal hop address remains; name is absent | Continue without names; optional PTR lookup must not indefinitely block the session |
| TCP refusal or timeout | Actual connect exception/deadline | Refused/Timeout with endpoint and method | Check service/network using authorized tests; do not infer firewall policy |
| ICMP permission denial or unsupported API | OS/provider error mapping | PermissionDenied/Unsupported result | Use available diagnostic types; Synups does not elevate automatically |
| Intermediate hops do not answer | Completed TTL probes without usable response | Unanswered counts and explicit path limitations | Keep partial path; filtering, ECMP and return paths prevent a definitive loss/route diagnosis |
| VPN, multiple interfaces or split DNS | Snapshot lists available interface/default-route data; resolver recorded | Provider limitations disclose incomplete route/DNS policy visibility | Review actual queried resolver and native routing; full destination-route or split-policy inference is not implemented |
| Network changes during a session | Individual adapter observations/outcomes at their timestamps | Mixed successes, errors or partial results | Retain the window honestly and run another explicit session; no atomic whole-network snapshot is claimed |
| User cancellation or session deadline | Linked cancellation token | Cancelled/Partial session, retained returned results and incomplete-attempt counts where provided | Reopen saved evidence; unattempted measurements stay absent |
| Forced termination or crash | Pending/Running checkpoint found during next initialization | Interrupted state, original timestamps, no invented completion/recovery | Reopen saved checkpoint. Only between-test saves are durable; current in-flight adapter samples can be lost |
| SQLite busy or file already owned | Connection lease or SQLite timeout/error | History/diagnostic operation fails visibly; no successful save claim | Close the other owner or resolve contention and retry. Do not delete a live lock/database |
| SQLite corruption or unknown schema | `quick_check`, schema-version and typed-payload validation | History unavailable/corrupt evidence error; original data preserved | Preserve files, use a verified backup and investigate; no automatic reset/reconstruction |
| SQLite full or configured capacity reached | Page limit / SQLite write failure | Save error; previously committed checkpoints remain | Free appropriate disk space or explicitly adjust supported storage capacity. Do not assume a failed current test was persisted |
| Read-only or denied storage/export location | File/SQLite permission error | Save, migration or export failure | Choose an authorized writable location or repair permissions; no silent relocation or evidence deletion |
| Migration backup or schema migration fails | SQLite backup `quick_check`, file publication and transactional migration | Startup/history error | Preserve the original and any published verified backup; repair disk/permission issues and retry. Existing v1 history is backed up before schema change |
| Optional retention encounters linked evidence | Foreign key and retention exclusion | Linked monitoring incident/session stays present | Protection is intentional. Confirmed diagnostic deletion is FK-safe; opt-in retention protects linked, referenced and unfinished records. Shared capacity remains bounded |
| Unreadable saved custom profiles | Size, JSON and request validation | Custom-profile error; existing file preserved | Repair or preserve the file before saving replacements; built-in profiles remain available |
| Raw SYN, UDP, enrichment, remote providers, scheduler or capture requested | Missing capability / explicit Unsupported adapter | No substitute protocol or external result is fabricated | These capabilities are not implemented. There is no automatic provider, credential prompt or elevation fallback |
| Capture privilege denied | Capture is disabled; no capture provider is invoked | No packets/payloads are collected by a capture feature | Adding capture requires a separate implementation, consent and native acceptance; this is not a tested denied-capture path |
| Export cancellation, disk error or partial write | Staged writes and final directory publication | Export failure; completed destination is not published until staging succeeds | Ordinary failures attempt staging cleanup; forced termination can leave a hidden `.partial` directory for inspection/removal |
| Export tampering | Manifest SHA-256 recomputation | Bytes no longer match their manifest entry | Retain original evidence. A regenerated manifest can conceal alteration; hashes do not authenticate origin |
| Unsupported desktop/platform behavior | Native provider errors or platform capability checks | Typed unavailable result or visible UI error | Windows/macOS/Linux are intended targets; exact runtime, architecture, display, permissions and package must be independently accepted |

## Durability and verification boundaries

SQLite writes are serialized off the UI thread with no transaction held across a network call. Each returned adapter result is checkpointed before the next test starts. A final persistence failure is an error, even when probes ran; use reopened history to determine what was actually committed. Linked diagnostics do not alter monitoring recovery semantics.

Exports use unique names, a staging directory and a manifest over actual JSON/Markdown/PDF bytes. JSON canonicalization is the documented sorted-member algorithm, not RFC 8785. The built-in PDF text renderer wraps and paginates text and escapes non-ASCII visibly; native visual/layout and artifact parsing acceptance must be recorded separately. Export preview is not a general database backup.

Deterministic fixtures, opt-in network tests, native desktop acceptance and published-artifact validation establish different things. No document here upgrades a build, test source, CI workflow definition or historical release result into a passed candidate runtime check. See [architecture](ARCHITECTURE.md), [privacy](PRIVACY.md), and the current feature/release evidence for outstanding gates.

System hostname lookups for TCP/ICMP are bounded by BoundedHostnameResolver (two native workers, no waiting queue). Caller cancellation can return promptly while the original OS lookup still owns its slot. Further lookups fail explicitly when those slots are occupied; literal IP requests avoid DNS. Shutdown also drains retained native tasks within the remaining seven-second target; a hung OS resolver keeps ownership and requires retry, rather than abandoning work. Direct DNS/PTR use owned cancellable UDP sockets.
