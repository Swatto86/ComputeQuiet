#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('gamequiet-ollama-' + [guid]::NewGuid())
$directory=Join-Path $root 'Programs\Ollama'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$binary=Join-Path $directory 'ollama.exe'
$log=Join-Path $root 'probe.log'
$originalLocal=$env:LOCALAPPDATA
$originalLog=$env:GAMEQUIET_PROBE_LOG
$originalInput=[Console]::In
$loads=[Collections.Generic.List[object]]::new()
# Fake only the HTTP boundary. Processes, identity checks, termination, relaunch and
# model reload request construction all execute through the production adapter.
function Invoke-RestMethod {
    [CmdletBinding()]
    param([string]$Uri,[int]$TimeoutSec,[string]$Method,[string]$ContentType,[string]$Body)
    if ($Uri -eq 'http://127.0.0.1:11434/api/ps') { return @{models=@(@{name='fixture:model';context_length=4096;expires_at=[DateTimeOffset]::UtcNow.AddYears(100).ToString('o')})} }
    if ($Uri -ne 'http://127.0.0.1:11434/api/generate' -or $Method -ne 'Post') { throw 'Unexpected HTTP request' }
    $loads.Add(($Body | ConvertFrom-Json))
    return @{done=$true}
}
function Invoke-Adapter([string]$Operation,[string]$InputJson='') {
    [Console]::SetIn([IO.StringReader]::new($InputJson))
    $raw=& (Join-Path (Split-Path $PSScriptRoot) 'src-tauri/windows.ps1') $Operation
    return ($raw | ConvertFrom-Json)
}
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$binary" (Join-Path $PSScriptRoot 'OllamaProbe.cs')
    if ($LASTEXITCODE) { throw 'Fixture compile failed' }
    $env:LOCALAPPDATA=$root
    $env:GAMEQUIET_PROBE_LOG=$log
    $first=Start-Process -FilePath $binary -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 600
    $snapshot=Invoke-Adapter scan
    if ($snapshot.error) { throw $snapshot.error }
    $workload=$snapshot.workloads | Where-Object id -eq 'ollama'
    if (-not $workload -or $workload.exe -ine $binary -or $workload.targets.Count -ne 1) { throw 'Fixture discovery must select only the disposable Ollama process' }
    $json=$workload | ConvertTo-Json -Depth 12 -Compress
    $invalid=$workload | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $invalid.targets[0].started='invalid'
    $denied=Invoke-Adapter stop ($invalid | ConvertTo-Json -Depth 12 -Compress)
    if (-not $denied.error -or $first.HasExited) { throw 'Stale process identity must be rejected before termination' }
    $stopped=Invoke-Adapter stop $json
    if ($stopped.error -or -not $stopped.changed -or -not $first.WaitForExit(3000)) { throw 'Ollama adapter did not stop its validated target' }
    $restored=Invoke-Adapter restore $json
    if ($restored.error) { throw $restored.error }
    $running=@(Get-Process ollama -ErrorAction SilentlyContinue | Where-Object Path -EQ $binary)
    if ($running.Count -ne 1) { throw 'Restore must start exactly one server' }
    if ($loads.Count -ne 1 -or $loads[0].model -ne 'fixture:model' -or $loads[0].options.num_ctx -ne 4096 -or $loads[0].keep_alive -ne -1) { throw 'Original model and context were not restored' }
    if ((Get-Content $log -Raw) -notmatch ':serve') { throw 'CLI-only Ollama restore must use serve' }
    $restored=Invoke-Adapter restore $json
    if ($restored.error -or @(Get-Process ollama | Where-Object Path -EQ $binary).Count -ne 1) { throw 'Restore is not idempotent' }
    Write-Output "PASS: isolated Ollama discovery, stale identity denial, real process stop/restart and model reload contract ($root)"
} finally {
    foreach ($p in Get-Process ollama -ErrorAction SilentlyContinue | Where-Object Path -EQ $binary) { $p.Kill(); $p.WaitForExit() }
    $env:LOCALAPPDATA=$originalLocal
    $env:GAMEQUIET_PROBE_LOG=$originalLog
    [Console]::SetIn($originalInput)
}
