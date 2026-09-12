# ComputeQuiet

A Windows tray app that frees resources for games, rendering, builds, video editing,
local AI and other demanding work, then restores the background apps it stopped.
Formerly GameQuiet; existing approvals and recovery journals remain compatible.

Windows requests administrator permission before launching ComputeQuiet. Cancelling UAC
leaves the app closed. Child processes, including restored apps, inherit elevation.

1. **Scan PC** measures CPU, GPU, memory and I/O.
2. **Assess with cloud AI** uses your signed-in Codex or Claude CLI. Only limited process
   metadata is sent; no full paths, command lines, window titles or file contents.
3. Review an app and choose **Close for session**. These choices apply to the exact
   executable version. Ollama also needs the interruption permission in Settings.
4. **Start quiet session** rechecks identities, saves recovery and applies your choices.
5. **End session & restore** restarts only recorded workloads. Close the window to use the tray.

The window and tray distinguish **Quiet session**, **Partially quiet**, and **Needs
attention** from recorded action outcomes. An unconfirmed stop is never presented as
success or proof that the process is still running. Measurements are timestamped
snapshots, not live readings; Scan PC refreshes them without closing anything. Restoring
or clearing recovery invalidates old readings. Keep the app doing your intensive work
on **Always keep**; only explicitly approved background workloads are selected.
Unavailable GPU/I/O counters are shown as unavailable and sent to cloud assessment as
unknown. Scans cover up to 80 processes above 5 MB in the current Windows session,
plus the supported Ollama workload; they are not a complete system process inventory.

Cloud advice only informs your review; your explicit choices decide what is stopped, and
they work offline. Cached advice expires after 24 hours. Nothing runs continuously while
you game or build. No FPS improvement is promised: compare the
same scene before/after, especially on systems already limited by game CPU work.

Ollama's active generations are interrupted. Its server and loaded models can be restored,
but interrupted conversations cannot. Ordinary applications receive a normal close request;
save prompts are respected. Reopening documents/tabs depends on the application's own
session restore. Protected processes and unsupported restore paths are kept running.
Workloads that respawn themselves are not repeatedly killed.

## Requirements

Windows 10/11 x64, [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows),
[Evergreen WebView2](https://developer.microsoft.com/microsoft-edge/webview2/), and an
installed, signed-in native [Codex CLI](https://developers.openai.com/codex/cli/) or
[Claude Code](https://code.claude.com/docs/en/setup) for cloud assessment. Assessment uses
your provider account's applicable limits. Core actions and restoration do not need AI.

## Development

Install Rust 1.98.1 (MSVC), the Windows C++ build tools and Node.js. Run `npm ci`, then
`npm run dev`. `scripts/verify.ps1` runs formatting, checks, tests and real-webview E2E.
Run verification from an elevated PowerShell so WebDriver can launch the administrator
executable. The E2E harness uses installed `tauri-driver` and an Edge driver matching WebView2.
Set `MSEDGEDRIVER` to its absolute path. `node tests/e2e.mjs --live` adds real cloud
acceptance. Tests use disposable state and apps, not your running workloads.

Launch **ComputeQuiet** from Start. For upgrade compatibility the executable stays at
`%LOCALAPPDATA%\Programs\GameQuiet\GameQuiet.exe`, and state stays in
`%LOCALAPPDATA%\GameQuiet`. Override with `GAMEQUIET_DATA_DIR` for isolated testing.
Do not delete state while recovery entries remain. On startup after a crash, use Restore.
Use Restore and quit before uninstalling. `scripts/uninstall.ps1` removes the installed
launcher and binaries, preserving user state unless you remove it yourself after recovery.

Source: [Cursor Origin](https://origin.cursor.com/swatto/ComputeQuiet). Local release;
no remote update service or published release channel is configured.
