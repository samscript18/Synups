# Feature inventory and parity

All legacy paths below are relative to `src/`. The legacy baseline was source-audited; original WPF runtime has not been exercised on this Mac. The original inventory below records Phase 1; the Phase 2 matrix and scoring that follow are current.

| Feature | Actual implementation and dependencies | Limitation / migration work | Synups Phase 1 |
|---|---|---|---|
| Startup / shutdown | WirePeep/App.xaml{,.cs}: WPF Application, executable-path Mutex, /Minimize, settings save on exit | Rewrite desktop lifetime; decide single-instance behavior and session ending per OS | Avalonia native desktop lifetime; no single-instance enforcement |
| Main status UI | WirePeep/MainWindow.xaml{,.cs}: ExtendedWindow, grouped DataGrid, stale sample indicators, response ms, monitored time | WPF triggers, collection views, clipboard and commands need rewriting | Honest setup overview only |
| ViewModels / state | WirePeep/StatusRow.cs, LogRow.cs and *RowCollection.cs; Menees PropertyChangeNotifier | MainWindow mixes orchestration with UI; split services and observable presentation | Small dedicated MainWindowViewModel |
| Named IP targets | Common/Location.cs, WirePeep/LocationDialog.xaml{,.cs} | Literal IPAddress only; no hostname/DNS check. Dialog can resolve names to IP at edit time; retain validation and IDs | Original literal defaults displayed read-only |
| Groups and timing | Common/PeerGroup.cs, Profile.cs; WirePeep/PeerGroupDialog.xaml{,.cs} | Stable Guid IDs, positive fail/poll/wait values; UI clamps limits; membership changes replace locations | Typed read-only defaults with timing |
| Default profile | Common/DefaultProfile.xml, Profile.LoadDefaults, Properties/Resources.* | Internet resolver addresses plus conditional router/modem/transceiver discovery; addresses are historical configuration, not verified working endpoints | Reuse XML resource; static literal locations only, discoveries explicitly omitted |
| Discovery | Common/Profile.cs, Pinger.GetAddressAtTtl | Gateway via TTL=1 ping to 8.8.8.8; candidate modem/transceiver pings. Can block startup; permissions/routing differ | Pending; no automatic scans |
| Connectivity | Common/Pinger.cs, LocationState.cs | Sync Ping.Send, tri-state bool, RTT only meaningful on success; PingException logged through Menees.Log. ICMP filtering is not proof of outage | Pending |
| Poll orchestration | Common/StateManager.cs, PeerGroupState.cs, WirePeep/MainWindow.BackgroundTimerCallback | 1s timer; 20ms poll tolerance; per-group rotation stops at first success, so not all locations are current; parallelism CPU/2 | Pending; cancellable loop required |
| Group failure / recovery | Common/PeerGroupState.cs, StateSnapshot.cs | Continuous disconnected duration >= Fail; connected OR unavailable clears failure; timestamps are UTC wall clock. UI log starts at confirmed failure time, not first missed ping | Pending; characterize exact semantics and agree error handling |
| Outage list and comments | WirePeep/MainWindow.UpdateLogRows/EditComment, LogRow.cs | Session-only list, per-group IDs, duration, since prior, mutable comment | Pending |
| Text logs / statistics | Common/Logger.cs, CommonOptions.cs; MainWindow.OpenLogger/CloseLogger | CSV-formatted text records start, failure, comments, end, summaries; summaries calculated at close; no database or history reload. File I/O from UI orchestration; errors need user feedback | Pending |
| Export | MainWindow.ExportLogExecuted, Logger.AddSimpleEntry | SaveFileDialog, chronological CSV; no data means export disabled | Pending |
| Alerts | WirePeep/AlertOptions.cs, OptionsDialog.xaml{,.cs} | Show/activate window, WinForms balloon, WPF taskbar color, MediaPlayer with Windows sound paths | Pending; per-OS adapters and capability fallback |
| Preferences | WirePeep/AppOptions.cs, Common/CommonOptions.cs, OptionsDialog.xaml{,.cs} | Run-at-login Registry HKCU Run, minimize/tray, topmost, confirm-close, fail/reconnect alert options, log folder/name format | Pending; versioned typed config planned |
| Settings persistence | App.Load/SaveNonWindowSettings, Common/Profile/Location/PeerGroup.Save/TryLoad | Menees ISettingsStore/ISettingsNode via ApplicationInfo.CreateUserSettingsStore; backing store implementation is external dependency, not defined in repo | Pending; explicit import, avoid assuming registry format |
| Placement / tray | MainWindow, Menees.WindowSaver; WinForms NotifyIcon, System.Drawing icon | Native Windows handles/placement, split sizes, tray View/Exit; dependencies and semantics require replacement | Native normal window; tray/placement pending |
| Menus / dialogs | MainWindow.xaml; LocationDialog, PeerGroupDialog, OptionsDialog, SimulateDialog; Commands.cs | Routed commands, input dialogs, about/help and file/folder dialogs depend on WPF/Menees | Standard Avalonia navigation; remaining dialogs pending |
| Simulation | SimulateDialog, LocationState.Update, MainWindow.simulateConnection | Can override measurements; menu exposed in debug or release with modifier keys | Excluded from new production code; original retained as reference |
| Tests / builds | WirePeep.sln, Directory.Build.props, eng/build.ps1, Build.cmd, .github/workflows/build.yml | No tests; two Windows target frameworks; signed assemblies; Windows publishing profiles Framework/Core | Independent solution and tests; cross-OS CI configured separately |

## Product goals versus port work
Network overview, targets/groups, connectivity, outage/recovery information, configuration, session history and preferences already have legacy counterparts. A durable searchable history, redesigned overview, robust cancellation, explicit probe-error state, import workflow and platform-specific notification implementations are enhancements or substantial redesigns. No fabricated uptime, jitter, bandwidth, loss percentage or long-term statistics exist in this foundation.


## Phase 2 parity — current implementation

The score measures the original inventory, not new features or platform certification. Complete = 1 point, partial = 0.5, pending = 0. Simulation is intentionally excluded from production, and tests/builds are an engineering gate, so those two rows are excluded from the denominator. No baseline percentage was measured in Phase 1; this is the first explicit rubric.

| Original inventory item | Phase 2 evidence | Score |
|---|---|---|
| Startup / shutdown | Explicit start, cancellation, awaited native close and restart; single instance /Minimize still absent | Partial 0.5 |
| Main status UI | Live overview, group status, target RTT/check/success times and stale labels; legacy clipboard/status details not all ported | Partial 0.5 |
| ViewModels / state | UI-free snapshots and observable presentation with safe synchronization | Complete 1 |
| Named IP targets | Add/edit/remove/move literal IPv4/IPv6 targets, IDs retained, validation | Complete 1 |
| Groups and timing | Create/edit/remove/enable groups, membership, poll/fail/timeout controls | Complete 1 |
| Default profile | Restores real XML literal targets/timings; discovery-only groups disabled | Partial 0.5 |
| Discovery | No gateway/modem/transceiver discovery is performed | Pending 0 |
| Connectivity | Real cancellable .NET ICMP, RTT, typed errors and configurable finite timeout | Complete 1 |
| Poll orchestration | Async bounded groups, no overlaps, name-sorted rotation, stop on success, freshness | Complete 1 |
| Failure / recovery | Tested grace/confirmation/recovery with documented error/monotonic corrections | Complete 1 |
| Outage list/comments | Actual session outages, comments, since-previous, elapsed/gaps, retain across start/stop | Complete 1 |
| Text logs / statistics | Session activity and CSV export exist; original automatic text log and close summaries absent | Partial 0.5 |
| Export | CSV quoting, occurrence order, stable UTC, unknown recovery blank, file errors reported | Complete 1 |
| Alerts | No OS notifications, alert sounds, taskbar color or focus-on-failure | Pending 0 |
| Preferences | Group enablement is new; original login/tray/topmost/confirm-close/alert preferences absent | Pending 0 |
| Settings persistence | Synups profile persists atomically with recovery; legacy settings import and app preference migration absent | Partial 0.5 |
| Placement / tray | Native window only; placement persistence and tray behavior pending | Pending 0 |
| Menus / dialogs | Functional editors, confirmations and native CSV picker; original clipboard/about/help/menu parity incomplete | Partial 0.5 |

**Calculation: (8 complete × 1 + 6 partial × 0.5 + 4 pending × 0) / 18 = 11/18 = 61.1%.** This is a transparent coarse inventory score, not a claim that 61.1% of source lines, engineering effort or cross-platform work is finished.

Engineering gates: linked-original characterization tests, deterministic engine/persistence/editor/headless tests, opt-in real loopback checks and a native macOS real-result smoke harness. Hosted CI and native Windows/Linux validation remain pending. New durable history, analytics, accounts and cloud features are not claimed.

## Semantics and scope
[LEGACY_SEMANTICS.md](LEGACY_SEMANTICS.md) records exact original transitions and deviations. Probe errors now reduce confidence instead of falsely recovering outages. Groups with mixed failure/error and no success are indeterminate. UTC is stored as evidence; monotonic intervals handle elapsed time. Stop never reports recovery. Configuration editing requires stop and applies only after successful save. Original user settings remain untouched; no importer is implied.
