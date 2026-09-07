#Requires -Version 7
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$binary=Join-Path $root 'target/release/gamequiet.exe'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build the verified local release first.' }
$destination=Join-Path $env:LOCALAPPDATA 'Programs\GameQuiet'
$installed=Join-Path $destination 'GameQuiet.exe'
if (Get-Process | Where-Object { $_.Path -ieq $installed }) { throw 'Use Restore and quit in GameQuiet before installing an update.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
if (Test-Path -LiteralPath $installed) { Copy-Item -LiteralPath $installed -Destination ($installed + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
Copy-Item -LiteralPath $binary -Destination $installed -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $destination 'uninstall.ps1') -Force
$menu=Join-Path ([Environment]::GetFolderPath('Programs')) 'GameQuiet.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($menu)
$shortcut.TargetPath=$installed
$shortcut.WorkingDirectory=$destination
$shortcut.Description='Make room for your game, then restore your apps.'
$shortcut.IconLocation="$installed,0"
$shortcut.Save()
if ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $binary).Hash) { throw 'Installed binary verification failed.' }
Write-Output "Installed GameQuiet 0.1.0: $installed"
Write-Output "Start menu: $menu"
