# Genuine WirePeep settings fixture

`WirePeep-5.1.2-windows.stgx` is a byte-identical settings file written by the **actual untouched original WirePeep executable** after normal native Windows launch and clean close in [run39840176062](https://github.com/samscript18/Synups/actions/runs/39840176062). Original source61ae14391151ca9f209223e62c98fae3df5482b8; .NET Framework4.8 on Windows Server2025, original Menees package5.1.2. Original filename`WirePeep-runnervm0xl3a-runneradmin.stgx` identifies a disposable CI account, not a user's private settings.

SHA-256`37b66a46f1fffaee7912b2733d00d76ef9fb06589635bdb7f8b6e7020d0ed7c1`,6642bytes. Only the filename changed; no XML fields/IDs/values were edited or invented. It contains original default configuration, not recorded connectivity measurements. Startup discovery/probes were blocked by a temporary application-scoped firewall rule in the disposable runner; no host/user network was modified. Empty HKCU Run infrastructure was created if absent, with no startup command installed, and cleaned when appropriate. Original MIT attribution remains Bill Menees'.

Importer tests preserve exact bytes and stable group/target IDs and exercise explicitly test-only malformed variants. Windows-only alert/startup/placement options are not blindly applied by a cross-platform profile import.
