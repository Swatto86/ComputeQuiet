#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$directory=Join-Path $env:LOCALAPPDATA 'Programs\GameQuiet'
$binary=Join-Path $directory 'GameQuiet.exe'
if (Get-Process | Where-Object { $_.Path -ieq $binary }) { throw 'Use Restore and quit in ComputeQuiet before uninstalling.' }
$state=Join-Path $env:LOCALAPPDATA 'GameQuiet\state.json'
if (Test-Path -LiteralPath $state) {
    $saved=Get-Content -LiteralPath $state -Raw | ConvertFrom-Json
    if ($saved.active -or $saved.recovery.Count) { throw 'Recovery is pending. Open ComputeQuiet and restore your workloads first.' }
}
foreach ($file in @($binary,(Join-Path ([Environment]::GetFolderPath('Programs')) 'GameQuiet.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'ComputeQuiet.lnk'))) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
Write-Output 'ComputeQuiet removed. Settings, activity, and previous-version backups were preserved.'
