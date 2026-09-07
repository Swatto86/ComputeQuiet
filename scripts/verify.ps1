#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    & "$PSScriptRoot/fastcheck.ps1"
    cargo clippy --locked --workspace --all-targets -- -D warnings
    if ($LASTEXITCODE) { throw 'Clippy failed' }
    cargo test --locked --workspace --all-targets
    if ($LASTEXITCODE) { throw 'Rust tests failed' }
    & "$PSScriptRoot/../tests/ollama-adapter.test.ps1"
    npx tauri build --debug --no-bundle
    if ($LASTEXITCODE) { throw 'Debug application build failed' }
    node tests/e2e.mjs
    if ($LASTEXITCODE) { throw 'Real-webview end-to-end checks failed' }
    Write-Output 'GameQuiet full verification passed'
} finally { Pop-Location }
