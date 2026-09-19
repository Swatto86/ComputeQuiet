# ComputeQuiet — working context

Read this first. `ARCHITECTURE.md` explains the structure; this file records
the decisions and constraints that are not visible in the code.

## Decisions

- **2026-09-19: rewritten as Rust + Tauri 2, cross-platform.** The previous
  C#/WPF Windows-only app in this repository's history was replaced in full.
  Vanilla TypeScript + Vite frontend, no framework; three-crate workspace.
- **Unelevated by default on Windows.** Services and the memory purge need
  administrator rights, but requiring elevation at launch would block a
  prompt-free autostart and make the WebDriver suite un-runnable from an
  unelevated shell. The app runs unelevated, plans around its capabilities,
  and offers *Relaunch as administrator*; the logon task is created elevated
  only when the creating process is elevated.
- **Suspend is the default process action; Close is opt-in.** Suspending
  keeps a program's state and is fully reversible; closing frees its memory
  but loses unsaved state, so it is chosen per target.
- **Fake platform behind a cargo feature** for the e2e suite. The suite drives
  the real binary; only the OS adapter is swapped. `verify.sh` asserts the
  feature is not a default and not in `tauri.conf.json`.
- **2026-09-19: GitHub is the source of truth; Origin is an inbound mirror.**
  Origin has no runners or releases of its own (CI there needs Depot or
  Buildkite, Linux-only or self-hosted), so `Swatto86/ComputeQuiet` on GitHub
  runs the workflows and hosts the releases, and `swatto/ComputeQuiet` on
  Origin is a mirror of it. Push to GitHub (`origin` remote); never push to
  the mirror.
- **No auto-updater yet.** Now possible because releases live on GitHub:
  a `latest.json` from GitHub Releases, a minisign key, `createUpdaterArtifacts`,
  and `tauri-plugin-updater` driven from Rust.
- **Linux elevation is per action through polkit** (`systemctl` for system
  units, `pkexec` for the cache drop), never a root relaunch of the GUI.
  macOS reports power and memory actions as unavailable rather than
  half-doing them.

## Workflow

- Single branch `main`; commit and push verified units.
- Inner loop: `npx tauri dev`; `scripts/fastcheck.ps1` / `.sh`.
- Full gate: `scripts/verify.ps1` / `.sh` (fmt, clippy, tests, frontend, debug
  build with the fake platform, WebDriver suite). Windows needs
  `scripts/setup-e2e.ps1` once per WebView2 update.
- Release: bump the version in `Cargo.toml`, `src-tauri/tauri.conf.json` and
  `package.json` (the gate checks agreement), `AGENT_RELEASE=1 npx tauri build`
  for the local install, wait for `verify` to pass on GitHub for that commit,
  then push tag `vX.Y.Z` to GitHub to publish the release.

## Known limits

- A program closed and relaunched inherits ComputeQuiet's elevation if it was
  relaunched from an elevated instance.
- Programs that respawn themselves (updater schedulers) are suspended, not
  closed, by default for that reason.
- The Linux process name from the kernel is 15 bytes; matching also uses the
  executable's file stem and a prefix rule.
- The e2e suite does not run on macOS (`tauri-driver` has no macOS backend).
