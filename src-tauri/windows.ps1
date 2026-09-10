#Requires -Version 7
[CmdletBinding()]
param([ValidateSet('scan','stop','restore')][string]$Operation)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$request = [Console]::In.ReadToEnd()
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$session = (Get-Process -Id $PID).SessionId
$ollamaDir = Join-Path $env:LOCALAPPDATA 'Programs\Ollama'

function Get-Blocked([Diagnostics.Process]$Process, [string]$Path) {
    if (-not $Path) { return 'Executable identity is unavailable.' }
    if ($Process.SessionId -ne $session) { return 'Another session or a Windows service.' }
    if ($Path.StartsWith(($env:WINDIR.TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { return 'Windows component.' }
    if ($Process.ProcessName -match '(?i)^(gamequiet|system|idle|registry|smss|csrss|wininit|winlogon|services|lsass|svchost|dwm|explorer|sihost|fontdrvhost|audiodg|conhost|openconsole|windowsterminal|powershell|pwsh|cmd|wsl|wslhost|vmmem.*|codex|claude|agent|opencode|node|msedgewebview2|rustc|cargo)$') { return 'System, terminal, or active development infrastructure.' }
    if ($Process.ProcessName -match '(?i)(wow|warcraft|steam|battle[.]?net|epicgames|riot|anticheat|easyanti|vgc|vgtray|faceit|defender|msmpeng|security|antivirus|nvidia|nvcontainer|radeon|amd|intel|realtek|icue|lghub|razer|discord|obs|vpn|tailscale|parsec|sunshine|moonlight|eir)') { return 'Game, security, communication, driver, or supporting infrastructure.' }
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($Process.Id)"
    if (-not $cim) { return 'Process exited.' }
    $owner = Invoke-CimMethod -InputObject $cim -MethodName GetOwnerSid
    if ($owner.ReturnValue -ne 0 -or $owner.Sid -ne $identity) { return 'Not owned by your Windows account.' }
    $line = ([string]$cim.CommandLine).Trim()
    if (-not $line) { return 'Launch arguments are unavailable; restoration cannot be established.' }
    if ($line -and $line -ine $Path -and $line -ine ('"' + $Path + '"')) { return 'Has launch arguments or open work; automatic restoration would be incomplete.' }
    return ''
}

function Get-Target([Diagnostics.Process]$Process) {
    return @{pid=$Process.Id; started=$Process.StartTime.ToUniversalTime().Ticks.ToString(); exe=$Process.Path; hash=(Get-FileHash -LiteralPath $Process.Path -Algorithm SHA256).Hash}
}

function Assert-Target($Target, [switch]$Ollama) {
    $process = Get-Process -Id $Target.pid -ErrorAction SilentlyContinue
    if (-not $process) { return $null }
    if ($process.StartTime.ToUniversalTime().Ticks.ToString() -ne $Target.started -or $process.Path -ine $Target.exe) { throw 'Process identity changed; rescan before applying Game Mode.' }
    if ((Get-FileHash -LiteralPath $Target.exe -Algorithm SHA256).Hash -ne $Target.hash) { throw 'The executable changed; rescan before applying Game Mode.' }
    if ($Ollama) {
        if (-not $process.Path.StartsWith(($ollamaDir + '\'), [StringComparison]::OrdinalIgnoreCase) -or $process.ProcessName -notin @('ollama','ollama app','llama-server')) { throw 'Ollama executable is outside its expected installation.' }
        $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
        $owner = Invoke-CimMethod -InputObject $cim -MethodName GetOwnerSid
        if ($process.SessionId -ne $session -or $owner.ReturnValue -ne 0 -or $owner.Sid -ne $identity) { throw 'Ollama is not owned by this user and session.' }
    } else {
        $blocked = Get-Blocked $process $process.Path
        if ($blocked) { throw $blocked }
    }
    return $process
}

function Get-Snapshot {
    $warnings = [Collections.Generic.List[string]]::new()
    $first = @{}
    foreach ($p in Get-Process) { try { $first[$p.Id] = $p.TotalProcessorTime.TotalMilliseconds } catch { continue } }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds 1100
    $processes = @(Get-Process | Where-Object { $_.SessionId -eq $session -and $_.WorkingSet64 -gt 5MB })
    $second = @{}
    foreach ($p in $processes) { try { $second[$p.Id] = $p.TotalProcessorTime.TotalMilliseconds } catch { continue } }
    $elapsed = $watch.Elapsed.TotalMilliseconds
    $cores = [Math]::Max(1, (Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors)
    $gpu = @{}
    try {
        foreach ($counter in Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine) {
            if ($counter.Name -match 'pid_(\d+)_') {
                $targetId = [int]$Matches[1]
                $gpu[$targetId] = [Math]::Max([double]$gpu[$targetId], [double]$counter.UtilizationPercentage)
            }
        }
    } catch { $warnings.Add('Per-process GPU counters are unavailable; CPU and memory readings remain available.') }
    $io = @{}
    try { foreach ($counter in Get-CimInstance Win32_PerfFormattedData_PerfProc_Process) { $io[[int]$counter.IDProcess] = [double]$counter.IODataBytesPersec / 1MB } }
    catch { $warnings.Add('Process I/O counters are unavailable.') }
    $rows = [Collections.Generic.List[object]]::new()
    $ollama = @($processes | Where-Object { $_.ProcessName -in @('ollama','ollama app','llama-server') -and $_.Path -and $_.Path.StartsWith(($ollamaDir + '\'), [StringComparison]::OrdinalIgnoreCase) })
    # GameQuiet itself is always listed (as protected) so its self-protection stays visible on busy PCs.
    foreach ($p in $processes | Sort-Object @{Expression={ $_.ProcessName -ieq 'gamequiet' }; Descending=$true}, @{Expression='WorkingSet64'; Descending=$true} | Select-Object -First 80) {
        try {
            if ($p.Id -eq $PID -or $p.Id -in $ollama.Id) { continue }
            $path = $p.Path
            $blocked = Get-Blocked $p $path
            if (-not $blocked -and $p.MainWindowHandle -eq 0) { $blocked = 'No normal close interface. Kept running; no force termination.' }
            $hash = if (-not $blocked) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '' }
            $version = if ($path) { $p.MainModule.FileVersionInfo } else { $null }
            $cpu = if ($first.ContainsKey($p.Id) -and $second.ContainsKey($p.Id)) { [Math]::Clamp(($second[$p.Id] - $first[$p.Id]) / $elapsed / $cores * 100, 0, 100) } else { 0 }
            $rows.Add(@{id="$($p.Id)-$($p.StartTime.ToUniversalTime().Ticks)"; pid=$p.Id; started=$p.StartTime.ToUniversalTime().Ticks.ToString(); name=$p.ProcessName; exe=[string]$path; hash=$hash; kind='close'; product=[string]$version.ProductName; publisher=[string]$version.CompanyName; cpu=[Math]::Round($cpu,1); memory_mb=[Math]::Round($p.WorkingSet64 / 1MB,1); io_mb=[Math]::Round([double]$io[$p.Id],2); gpu=[double]$gpu[$p.Id]; has_window=($p.MainWindowHandle -ne 0); blocked=$blocked; targets=@(); models=@()})
        } catch { $warnings.Add("Could not inspect process $($p.Id); it will be left running.") }
    }
    if ($ollama.Count) {
        $models = @()
        $blocked = ''
        try {
            $loaded = Invoke-RestMethod 'http://127.0.0.1:11434/api/ps' -TimeoutSec 5
            $models = @($loaded.models | ForEach-Object {
                $remaining=([DateTimeOffset]$_.expires_at - [DateTimeOffset]::UtcNow).TotalSeconds
                @{name=$_.name; context_length=[long]$_.context_length; keep_alive_seconds=$(if ($remaining -gt 31536000) {-1} else {[long][Math]::Max(1,$remaining)})}
            })
        } catch { $blocked = 'Cannot record loaded models because the Ollama API is unavailable.' }
        $launcher = $ollama | Where-Object ProcessName -eq 'ollama app' | Select-Object -First 1
        if (-not $launcher) { $launcher = $ollama | Where-Object ProcessName -eq 'ollama' | Select-Object -First 1 }
        if ($launcher) {
            $targets = @($ollama | ForEach-Object { Get-Target $_ })
            $target = Get-Target $launcher
            $sumCpu = 0; $sumGpu = 0; $sumMemory = 0
            foreach ($p in $ollama) {
                if ($first.ContainsKey($p.Id) -and $second.ContainsKey($p.Id)) { $sumCpu += [Math]::Max(0, ($second[$p.Id] - $first[$p.Id]) / $elapsed / $cores * 100) }
                $sumGpu += [double]$gpu[$p.Id]; $sumMemory += $p.WorkingSet64 / 1MB
            }
            $rows.Add(@{id='ollama'; pid=$target.pid; started=$target.started; name='Ollama'; exe=$target.exe; hash=$target.hash; kind='ollama'; product='Ollama local AI'; publisher='Ollama'; cpu=[Math]::Round([Math]::Min(100,$sumCpu),1); memory_mb=[Math]::Round($sumMemory,1); io_mb=0; gpu=[Math]::Min(100,$sumGpu); has_window=$false; blocked=$blocked; targets=$targets; models=$models})
        } else { $warnings.Add('An orphaned Ollama worker was detected; restart Ollama before using Game Mode.') }
    }
    $gpuSummary = 'GPU utilisation is the busiest engine for each process; shared engines are not additive.'
    return @{workloads=@($rows | Sort-Object -Property @{Expression='gpu';Descending=$true},@{Expression='cpu';Descending=$true}); warnings=@($warnings); gpu_summary=$gpuSummary}
}

try {
    if ($Operation -eq 'scan') { $result = Get-Snapshot }
    else {
        $w = $request | ConvertFrom-Json
        if ($w.kind -notin @('ollama','close') -or $w.blocked -or -not $w.hash) { throw 'This workload is not eligible for a reversible action.' }
        if ($Operation -eq 'stop') {
            if ($w.kind -eq 'ollama') {
                # Validate every process before stopping any. No name-based taskkill or elevation.
                $validated = @($w.targets | ForEach-Object { Assert-Target $_ -Ollama } | Where-Object { $null -ne $_ })
                foreach ($p in $validated | Sort-Object @{Expression={ if ($_.ProcessName -eq 'ollama app') {0} elseif ($_.ProcessName -eq 'ollama') {1} else {2} }}) {
                    if ($p -and -not $p.HasExited) { $p.Kill(); if (-not $p.WaitForExit(5000)) { throw 'Ollama did not stop within five seconds.' } }
                }
                $result = @{changed=($validated.Count -gt 0); message='Ollama stopped. Interrupted requests cannot be resumed; server and model availability will be restored.'}
            } else {
                $p = Assert-Target $w
                if ($p) {
                    if ($p.MainWindowHandle -eq 0 -or -not $p.CloseMainWindow()) { throw 'The app did not accept a normal close request.' }
                    if (-not $p.WaitForExit(8000)) { throw 'App remains open, possibly showing a save prompt. It was not forced closed.' }
                }
                $result = @{changed=($null -ne $p); message='Application closed normally.'}
            }
        } else {
            if (-not [IO.Path]::IsPathFullyQualified($w.exe) -or -not (Test-Path -LiteralPath $w.exe -PathType Leaf)) { throw 'The original executable is missing. Restore it manually.' }
            if ((Get-FileHash -LiteralPath $w.exe -Algorithm SHA256).Hash -ne $w.hash) { throw 'The app was updated or replaced. Launch it manually, then clear its recovery entry.' }
            $existing = @(Get-Process | Where-Object { $_.SessionId -eq $session -and $_.Path -ieq $w.exe })
            if (-not $existing.Count) {
                $arguments = @()
                if ($w.kind -eq 'ollama' -and [IO.Path]::GetFileName($w.exe) -ieq 'ollama.exe') { $arguments = @('serve') }
                $start = @{FilePath=$w.exe; WorkingDirectory=(Split-Path $w.exe); PassThru=$true; WindowStyle='Hidden'}
                if ($w.kind -eq 'close' -and $w.has_window) { $start.WindowStyle = 'Normal' }
                if ($arguments.Count) { $start.ArgumentList = $arguments }
                $launched = Start-Process @start
                Start-Sleep -Milliseconds 700
                if ($launched.HasExited -and $launched.ExitCode -ne 0) { throw 'The application exited with an error while restoring.' }
            }
            if ($w.kind -eq 'ollama') {
                $ready = $false
                for ($i=0; $i -lt 20; $i++) {
                    try { $null = Invoke-RestMethod 'http://127.0.0.1:11434/api/ps' -TimeoutSec 2; $ready=$true; break }
                    catch { Start-Sleep -Milliseconds 500 }
                }
                if (-not $ready) { throw 'Ollama restarted but its API is not ready. Retry Restore.' }
                foreach ($model in $w.models) {
                    if (-not $model.name -or $model.name.Length -gt 256 -or $model.context_length -gt 1048576) { throw 'Invalid saved model metadata.' }
                    if ($model.keep_alive_seconds -lt -1 -or $model.keep_alive_seconds -gt 31536000) { throw 'Invalid saved model lifetime.' }
                    $body = @{model=$model.name; keep_alive=$model.keep_alive_seconds; stream=$false}
                    if ($model.context_length -gt 0) { $body.options = @{num_ctx=$model.context_length} }
                    $null = Invoke-RestMethod 'http://127.0.0.1:11434/api/generate' -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json) -TimeoutSec 100
                }
            }
            $result = @{message='Restored application availability. Previous in-progress work is not replayed.'}
        }
    }
    $result | ConvertTo-Json -Depth 12 -Compress
} catch {
    # Only controlled error messages cross the boundary; exception details may contain local data.
    @{error=$_.Exception.Message} | ConvertTo-Json -Compress
}
