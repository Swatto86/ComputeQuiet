#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$directory=Join-Path $env:LOCALAPPDATA 'Programs\GameQuiet'
$binary=Join-Path $directory 'GameQuiet.exe'
if (Get-Process | Where-Object { $_.Path -ieq $binary }) { throw 'Use Restore and quit in GameQuiet before uninstalling.' }
$state=Join-Path $env:LOCALAPPDATA 'GameQuiet\state.json'
if (Test-Path -LiteralPath $state) {
    $saved=Get-Content -LiteralPath $state -Raw | ConvertFrom-Json
    if ($saved.active -or $saved.recovery.Count) { throw 'Recovery is pending. Open GameQuiet and restore your workloads first.' }
}
foreach ($file in @($binary,(Join-Path ([Environment]::GetFolderPath('Programs')) 'GameQuiet.lnk'))) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
Write-Output 'GameQuiet removed. Settings, activity, and previous-version backups were preserved.'
