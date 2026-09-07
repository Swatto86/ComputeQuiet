#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    cargo fmt --check
    if ($LASTEXITCODE) { throw 'Rust formatting failed' }
    node --check ui/app.js
    if ($LASTEXITCODE) { throw 'Frontend syntax failed' }
    foreach ($script in Get-ChildItem $PWD -Recurse -File -Filter '*.ps1' | Where-Object FullName -NotMatch '\\(node_modules|target)\\') {
        $tokens=$null; $errors=$null
        [Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors)|Out-Null
        if ($errors) { throw ($errors|Out-String) }
    }
    cargo check --locked --workspace --all-targets
    if ($LASTEXITCODE) { throw 'Rust check failed' }
} finally { Pop-Location }
