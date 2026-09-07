# Context

- Product: GameQuiet, a Windows tray utility to free gaming resources and restore the
  workloads afterwards. Name chosen for the initial implementation; version 0.1.0.
- Origin is the only remote: `https://origin.cursor.com/swatto/gamequiet.git`. Work on
  `main`, commit and push verified units. No GitHub repository or release assets.
- Use Codex/Claude CLI cloud authentication in the same manner as Eir; do not extract
  tokens. AI advice cannot execute commands or bypass deterministic safeguards.
- Initial scope includes process measurements, CLI assessment, cached advice, per-app
  choices, Ollama stop/reload, graceful app closure, tray toggle and durable recovery.
  Global service termination, driver/security tweaks and forced closure of arbitrary
  apps are deliberately excluded because their restoration/safety cannot be established.
- Current build: Rust 1.98.1 MSVC, Tauri 2.11.5, static frontend; PowerShell 7 and
  Evergreen WebView2 are external prerequisites. No npm production frontend build.
- Read `ARCHITECTURE.md` for state and safety boundaries. `scripts/verify.ps1` is the
  full gate. Local release installation follows successful debug and live acceptance.
- Windows journal replacement retries only short sharing/access-denied failures (up
  to 775 ms); persistent failure restores in-memory state to the last saved journal.
  Tested with an actual file handle denying delete sharing. Subprocess stdin/stdout
  use temporary handles to avoid pipe inheritance and unread-stdin deadlocks.
