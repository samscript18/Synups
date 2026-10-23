# Migration measurements — frozen public application

The migration began **October 16, 2026** and the application froze **October 22, 2026**. These are elapsed calendar dates, not measured developer hours. Work was AI-assisted. Public **v0.1.1** contains unchanged internal **0.1.1-rc.4** at `6c812fde9a29eeb8cfd6399a2f1a75e333e81e03`; no alpha code or later documentation commits enter this measurement.

## Exact Git scope

Untouched upstream baseline: `61ae14391151ca9f209223e62c98fae3df5482b8`. The following range contains **39 commits**, including all commits reachable from the release but not the baseline. It is not a count of coding sessions or contributor hours.

```sh
git rev-list --count 61ae14391151ca9f209223e62c98fae3df5482b8..6c812fde9a29eeb8cfd6399a2f1a75e333e81e03
python3 eng/measure-migration.py --output /tmp/synups-migration-measurements.json
```

## Consistent source-line method

[Measurement script](../../eng/measure-migration.py) reads exact Git blobs, decodes UTF-8 with optional BOM and counts `splitlines()`. Physical lines include blank lines and comments; nonblank lines omit whitespace-only lines but retain comments. These are **physical source lines, not logical statements or effort**. CRLF and LF produce the same count. C# includes `.cs`; XAML-family includes both `.xaml` and `.axaml`.

| Scope | Tracked files | C# physical | XAML physical | Total physical | Nonblank |
|---|---:|---:|---:|---:|---:|
| Original production: `src/WirePeep`, `src/WirePeep.Common` at baseline | 31 | 3,719 | 655 | **4,374** | 3,600 |
| Synups production: `synups/Synups.Core`, `synups/Synups.Desktop` at public source | 42 | 4,093 | 376 | **4,469** | 4,318 |
| Synups tests: `synups/Synups.Tests` at public source, separate | 21 | 2,756 | 0 | **2,756** | 2,618 |

Exclusions apply to both trees: generated `*.Designer.cs`, `*.g.cs`, `*.g.i.cs`; artwork under `Images`/`Assets`; all other extensions including XML profile data, `.resx`, SVGs, project/lock files, documentation and scripts. Two original generated resource wrappers total 161 physical / 143 nonblank lines; two third-party Material Design icon XAML resources total two lines. No generated wrapper exists in the selected Synups tracked scope. External NuGet/runtime sources (including Menees, Avalonia and SQLite) are not vendored source in these counts. Build outputs and dependencies outside the selected tracked trees are excluded. Handwritten assembly metadata remains included.

Tests are reported separately; linked-original characterization source is counted once in the original tree and not duplicated in the new tests. The desktop installation-check harness is included because it is tracked and ships inside the public desktop project. The preserved original tree in the port repository is not counted again as new code. Development diagnostics and paused working-tree changes are excluded by the frozen revision.

The project scopes differ: Synups replaces WPF composition, adds bounded asynchronous monitoring, durable SQLite incidents, import/backup and cross-platform adapters. Similar totals do not establish a rewrite percentage, equivalent functionality or relative development cost. [Full file-level output, including exclusions](../evidence/verification/2026-10-23-readme-presentation/migration-measurements.json).

## Verification and parity

The [public-source native run](https://github.com/samscript18/Synups/actions/runs/39850198899) records 159 default .NET passes on Windows/macOS and 160 on Linux, three separately enabled real/native passes plus one audio-request/fallback pass on each, and eight Python checks per OS. **164 unique .NET cases** span platforms/invocations; intentional skips are not failures or universal execution. Six extracted-apphost stages per OS include five normal zero exits and a deliberate process kill. [Retained evidence](../evidence/verification/2026-10-22-desktop-completion.json).

Weighted original parity is **(15 complete + 3 partial × 0.5) / 18 = 91.7%**. This is the [fixed functional rubric](FEATURE_PARITY.md), not a line-count score, effort percentage or physical-desktop certification. Alerts, preferences and placement/tray remain partial. [Architecture and scope](ARCHITECTURE.md) · [historical migration log](MIGRATION_LOG.md).
