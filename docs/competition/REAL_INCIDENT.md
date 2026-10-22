# Controlled real ICMP incident protocol

This is a reproducible engineering demonstration, **not simulated measurements and not an Internet outage**. Actual production IcmpProbe and MonitoringService run against 127.0.0.1 inside a disposable Linux network namespace. A namespace-local kernel firewall rule drops actual echo requests after successful connectivity, then is removed after confirmed outage. The user’s host firewall, routes, interfaces and network configuration are never changed.

Current execution status: harness compiled locally; real Linux namespace execution pending the exact-source CI. A successful compile or deterministic test is not an incident pass. Final source/run/results and screenshots must be added after actual execution.

## Guardrails

The worker requires Linux/root, a network namespace different from PID 1, only the loopback interface and no default route. It refuses host/mixed-interface namespaces before any mutation. Four deterministic safety tests exercise these refusals. It permits only its own new output directory, explicit loopback configuration and finite observation deadlines. It never scans or probes external addresses. The one filter has a unique comment and exact add/remove arguments; finally removes it and terminates only the owned child. Namespace teardown removes its networking state.

No privileged worker belongs in the shipped application. `eng/incident-smoke.cs` is compiled as a separate source-component native verifier, not included in production projects. Programmatic UI actions/captures are distinct from physical interaction and actual published-apphost checks.

## Runner reproduction

Use a disposable Ubuntu 24.04 runner/VM with .NET 10, Python 3, Xvfb, iproute2, iptables and Avalonia native display dependencies. Build before isolating networking:

```sh
python3 eng/incident-smoke.py --prepare artifacts/incident-build
xvfb-run -a -s '-screen 0 1440x1000x24' bash -c 'sudo env "PATH=$PATH" "DOTNET_ROOT=$DOTNET_ROOT" "DISPLAY=$DISPLAY" "XAUTHORITY=$XAUTHORITY" "GITHUB_SHA=$GITHUB_SHA" unshare --net -- python3 eng/incident-smoke.py --worker artifacts/incident-build/bin/Release/net10.0/Synups.IncidentSmoke.dll --output artifacts/incident'
```

Do **not** remove namespace guards or run firewall commands in the host namespace. Local macOS lacks this Linux namespace facility; Docker is installed but its daemon is absent, so no local isolated incident pass is claimed. This does not authorize altering the user’s actual network.

## Required observations and exported evidence

- Real success and healthy visible UI before the controller installs a filter.
- Actual Disconnected observations before confirmation, with no incident during initial grace.
- Confirmation after configured 2s continuous failures, one stable incident ID, UTC first-failure/start and monotonic duration.
- Visible in-app unresolved incident/comment, then real success after filter removal and exactly one recovery.
- Quoted CSV with genuine incident, timestamps and context explaining the controlled environment.
- Clean Stop/Close; a separate process reloads the same incident/comment/events without generating another recovery or automatically starting.
- Native client PNGs for healthy, grace, confirmed outage, recovery and restarted history; JSON transition data, namespace/actions/hashes, CSV and console logs retained by CI even on failure.

These are observed loopback ICMP failures/recovery caused by a documented isolated filter, not a real Internet/provider outage or a physical cable/power-loss experiment. A Linux success must not be relabelled as a macOS/Windows incident run. No incident or screenshot is claimed until its actual recorded run passes.
