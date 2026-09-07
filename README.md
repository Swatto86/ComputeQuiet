# GameQuiet

A Windows tray app that makes room for your game, then restores the apps it stopped.

Windows requests administrator permission before launching GameQuiet. Cancelling UAC
leaves the app closed. Child processes, including restored apps, inherit elevation.

1. **Scan PC** measures CPU, GPU, memory and I/O.
2. **Assess with cloud AI** uses your signed-in Codex or Claude CLI. Only limited process
   metadata is sent; no full paths, command lines, window titles or file contents.
3. Review an app and choose **Close in Game Mode**. These choices apply to the exact
   executable version. Ollama also needs the interruption permission in Settings.
4. **Turn on Game Mode** rechecks identities, saves recovery and applies your choices.
5. **Turn off & restore** restarts only recorded workloads. Close the window to use the tray.

The optional high-confidence filter limits your approved list to AI recommendations
of at least 90%. Cached advice expires after 24 hours; approved-list mode works offline.
Nothing runs continuously while you game. No FPS improvement is promised: compare the
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

State: `%LOCALAPPDATA%\GameQuiet`. Override with `GAMEQUIET_DATA_DIR` for isolated testing.
Do not delete state while recovery entries remain. On startup after a crash, use Restore.
Use Restore and quit before uninstalling. `scripts/uninstall.ps1` removes the installed
launcher and binaries, preserving user state unless you remove it yourself after recovery.

Source: [Cursor Origin](https://origin.cursor.com/swatto/gamequiet). Initial local release;
no remote update service or published release channel is configured.
