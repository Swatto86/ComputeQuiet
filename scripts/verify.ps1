#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    & "$PSScriptRoot/fastcheck.ps1"
    cargo clippy --locked --workspace --all-targets -- -D warnings
    if ($LASTEXITCODE) { throw 'Clippy failed' }
    # The application manifest requires administrator, and Cargo applies it to the test
    # harness too, so an ordinary shell cannot launch it. These tests cover pure logic and
    # temporary files only; running them as the invoker skips no test. The end-to-end suite
    # below still drives the real elevated application, so the variable is cleared first.
    $env:__COMPAT_LAYER = 'RunAsInvoker'
    try { cargo test --locked --workspace --all-targets } finally { $env:__COMPAT_LAYER = $null }
    if ($LASTEXITCODE) { throw 'Rust tests failed' }
    & "$PSScriptRoot/../tests/ollama-adapter.test.ps1"
    npx tauri build --debug --no-bundle
    if ($LASTEXITCODE) { throw 'Debug application build failed' }
    $administrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $administrator) { throw 'The end-to-end suite launches the real application, which requires administrator. Re-run scripts/verify.ps1 from an elevated PowerShell.' }
    # A resident copy makes the single-instance guard hand every driver-launched instance to
    # it, which WebDriver reports only as a blank document. Report the conflict; never
    # terminate the user's application.
    if (Get-Process -Name gamequiet -ErrorAction SilentlyContinue) { throw 'GameQuiet is running. Use Restore and quit in the application, then re-run verification.' }
    node tests/e2e.mjs
    if ($LASTEXITCODE) { throw 'Real-webview end-to-end checks failed' }
    Write-Output 'GameQuiet full verification passed'
} finally { Pop-Location }
