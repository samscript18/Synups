# Monitoring characterization — recorded before Phase 2 implementation

Original SHA: 61ae14391151ca9f209223e62c98fae3df5482b8. Copyright Bill Menees, MIT. Source paths refer to src/.

| Case | Original exact behavior | Synups decision |
|---|---|---|
| Success | Pinger maps IPStatus.Success to true; RTT from PingReply. LocationState => Connected. PeerGroup stops at first connected target. | Preserve; RTT is nullable otherwise. |
| Non-success reply | Every other IPStatus => false => Disconnected. | Preserve, classify timeout/unreachable separately. |
| Probe exception | Pinger catches PingException, returns null => Unavailable. Other exceptions reach timer catch/log. | Typed unavailable/error, catch at service boundary; cancellation is control flow, not failure. |
| Initial state | Connection enum default Unavailable, group counter -1, location -2. | Explicit NotChecked, no zero latency or implied status. |
| First failure | Group ConnectionChanged set at requested poll UTC; IsFailed remains false until Fail elapses. | Preserve grace period; measured at completed poll using monotonic time. |
| Consecutive failure | Same Disconnected state keeps ConnectionChanged; threshold >= starts failure. | Preserve, one outage-start event. |
| Success before threshold | Clears pending failure, no log record. | Preserve. |
| Success after threshold | Clears IsFailed; WPF ends active LogRow and emits recovery. | Preserve, one recovery event. |
| Unavailable | Clears IsFailed immediately, even during an outage; WPF therefore logs an apparent recovery without a successful probe. | Intentional correction: resets unconfirmed failure streak; confirmed outage stays unresolved with reduced confidence/gap until success. |
| Mixed failure/error | Last visited updated target wins if no target succeeds; rotation can change group outcome. | Intentional correction: any error and no success => Unavailable, independent of order; cannot confirm all targets disconnected. |
| Rotation | StateManager sorts target states by location Name; group increments counter each eligible poll, starts at counter modulo count, wraps, stops at success. | Preserve name-sorted round robin and stop-on-success. |
| Freshness | Only target states with UpdateCounter == current group counter are current; earlier successful samples may be stale. | Preserve explicit per-cycle freshness and last-check/last-success timestamps. |
| Intervals | PeerGroup.CanPoll is elapsed > Poll - 20ms, using UTC. LocationState independently checks same rule. Timer calls every second. | Target guard retains 20ms tolerance; scheduling uses monotonic exact deadlines without the coarse 1s tick. |
| Empty group | StateManager map built only from locations; empty configured groups are never polled/published. | Disabled empty groups are editable; enabled empty groups rejected. Defaults with discovery-only groups are disabled, no discovery performed. |
| Concurrent groups | Parallel.ForEach CPU count / 2; groups synchronous, global timer Interlocked guard; single-core bug gives zero parallelism. | Asynchronous independent group loops, semaphore bounded to four polls; no overlapping group poll. |
| Shutdown | Timer.Dispose does not await running callback; no cancellation; logging/UI callbacks can race exit. | Explicit cancellation, finite per-probe timeout <=5s, await all session tasks before stop/restart/close; never emit recovery on stop. |
| Duration/events | LogRow starts at IsFailedChanged (confirmation), not first missed ping. WPF calculates durations from UTC and logs summaries on close. | Preserve confirmation start; record first failed observation separately; monotonic elapsed duration, UTC event timestamps. Gaps explicitly marked. No fabricated uptime/summary percentages. |
| Pause/gap | Wall-clock changes, sleep and delayed callbacks can confirm failure across unobserved time. | Reset pending streak across large scheduling gaps; mark existing outages as containing gaps, never call it continuous observed downtime. |
| Configuration edits | Live observable collections update maps without waiting for existing callbacks. | Stop before editing. Snapshot immutable configuration for each session; IDs retained for edits/moves. |

Direct deterministic characterization: tests link the original PeerGroupState, LocationState, StateSnapshot and Enumerations source into the test assembly. Test-only minimal PeerGroup/Location/Pinger collaborators isolate the unmodified state-transition/rotation code without network, Windows, Menees settings or a production simulator. The legacy poll predicate is copied verbatim with attribution in that test harness. Pinger, Profile and WPF orchestration findings above are source-based, not original runtime claims. Equivalent migrated-engine tests cover intentional differences.
