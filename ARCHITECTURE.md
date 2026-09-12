# ComputeQuiet

Windows-only Tauri v2 tray app with static HTML/CSS/JavaScript. Rust owns policy,
settings, CLI requests and a write-ahead recovery journal. The interface has no shell
or filesystem permissions. Blocking operations run off the UI thread, serialised per
application instance. The tray remains idle between user actions, and its menu names the
recorded session outcome and the action separately so a toggle is never ambiguous.

Starting a quiet session removes each workload confirmed stopped or already exited from
the snapshot immediately, including if a later journal write fails. Remaining measurements
retain their timestamp. A fresh scan updates that timestamp and clears the previous cloud
summary. Restore and manual recovery clear the old snapshot and advice. No continuous
measurement runs. Failed stops retain recovery and are labelled unconfirmed: errors can
mean a partial action or an exited process, so they cannot establish current liveness.
The window and tray share a derived session label; the persisted `active` flag means
restoration is pending, not that every requested stop succeeded. Busy events disable
conflicting UI actions, including operations initiated from the tray.

The 0.2.0 product rename keeps the original executable, Tauri identifier, data directory,
environment variable and version-1 journal format. Installation replaces the owned old
Start shortcut with ComputeQuiet and retains a backup of the previous executable.

`model.rs` is the IPC/state contract. `engine.rs` validates preferences, caches advice
for 24 hours by executable path + SHA-256, and records each stop before executing it.
Atomic replacement keeps the previous state intact on write failure. Unknown state
versions and malformed state fail visibly without overwriting the journal.

`windows.ps1` is an embedded, bounded PowerShell 7 adapter. It samples current-session
processes and Windows CPU/GPU/I/O counters. It revalidates PID, start time, owner, path
and hash before actions. Ordinary apps receive CloseMainWindow, never forced termination.
Processes with launch arguments, protected names, other owners/sessions, Windows paths,
or no normal close interface are left running. Product/publisher strings are metadata,
not proof of trust. Ollama has a dedicated, separately consented adapter restricted to
the standard per-user installation. It records loaded models, stops its verified
processes, then restores the server and model availability. Active requests are not
resumable. Restoring a normal app reopens its executable; app-specific document/session
recovery remains that app's responsibility.

`ai.rs` invokes native Codex/Claude executables under the signed-in user, using their
own authentication. CLI shell tools, MCP integrations and customisations are disabled
for assessment. Only allowlisted process metadata crosses the cloud boundary. Structured
results cannot name an unknown target or override local protection. The AI never supplies
commands and never decides what is stopped: only an explicit, hash-bound user Allow
rule selects a workload. No AI is needed to stop or restore, and choices work offline.
Child processes are hidden and bounded by time/output limits.

State is `%LOCALAPPDATA%\GameQuiet\state.json`. `GAMEQUIET_DATA_DIR` selects a separate
state directory for portable use and testing. Closing the window hides it; Restore and
quit refuses to exit while recovery remains. A forced termination or reboot leaves the
journal for explicit restoration on next launch. Single-instance enforcement protects
the installed session. The Windows executable manifest requires administrator elevation
before launch (debug and release); child processes inherit elevation. Existing process
ownership and protection rules still apply. No system service, autostart or updater is installed
in this initial local release; source is hosted on Cursor Origin, not GitHub.

Verification uses Rust policy/persistence tests and WebdriverIO through tauri-driver on
the elevated debug build. Debug builds forward WebDriver's browser arguments and data
directory through the WebView2 API because elevated WebView2 ignores environment
overrides. This forwarding is compiled out of release builds; release acceptance uses
the embedded manifest and a normal elevated launch.
The UI and process restoration tests run on
the real Windows WebView2 binary. Disposable executables exercise the normal close and
restore adapter. The Ollama test uses a disposable server executable in an isolated
installation directory and mocks only HTTP; the production process checks and stop/
restart paths run for real. Cloud acceptance uses the real CLI login and isolated state.
