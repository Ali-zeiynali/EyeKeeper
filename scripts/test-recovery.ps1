$ErrorActionPreference = 'Stop'
$installedExe = Join-Path $env:LOCALAPPDATA 'Programs\EyeKeeper\EyeKeeper.exe'
function Get-EyeProcesses {
    @(Get-CimInstance Win32_Process -Filter "Name='EyeKeeper.exe'" | Where-Object { $_.ExecutablePath -eq $installedExe })
}
function Get-Worker { Get-EyeProcesses | Where-Object CommandLine -Match '--worker(?:-demo)?(?:\s|$)' | Select-Object -First 1 }
function Get-Guardian { Get-EyeProcesses | Where-Object CommandLine -Match '--watchdog(?:-autostart|-demo)?(?:\s|$)' | Select-Object -First 1 }
function Test-Heartbeat([int]$workerId) {
    $memory = $null; $view = $null
    try {
        $memory = [IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting("Local\EyeKeeper.Health.$workerId")
        $view = $memory.CreateViewAccessor(0, 8, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
        return $view.ReadInt64(0) -ge 3
    } catch [IO.FileNotFoundException] { return $false }
    finally { if ($view) { $view.Dispose() }; if ($memory) { $memory.Dispose() } }
}
function Wait-Worker([int]$oldId = 0) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $current = Get-Worker
        if ($current -and $current.ProcessId -ne $oldId -and (Test-Heartbeat $current.ProcessId)) { return $current }
        Start-Sleep -Milliseconds 500
    } while ($timer.Elapsed.TotalSeconds -lt 25)
    throw 'The guardian did not restore the worker within 25 seconds.'
}
$results = [ordered]@{ Version = (Get-Item -LiteralPath $installedExe).VersionInfo.ProductVersion; StartedAt = [DateTimeOffset]::Now.ToString('O'); WorkerRestarts = @() }
$worker = Wait-Worker
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $oldId = [int]$worker.ProcessId
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Stop-Process -Id $oldId -Force
    $worker = Wait-Worker $oldId
    $results.WorkerRestarts += [ordered]@{ Attempt = $attempt; OldPid = $oldId; NewPid = $worker.ProcessId; RecoverySeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 2) }
    Write-Output "PASS forced worker termination $attempt recovered"
}
$guardian = Get-Guardian
if (-not $guardian) { throw 'Guardian missing before recovery-task test.' }
$workerBefore = [int]$worker.ProcessId
$oldGuardian = [int]$guardian.ProcessId
$timer = [Diagnostics.Stopwatch]::StartNew()
Stop-Process -Id $oldGuardian -Force
do {
    Start-Sleep -Seconds 2
    $guardian = Get-Guardian
} while ((-not $guardian -or $guardian.ProcessId -eq $oldGuardian) -and $timer.Elapsed.TotalSeconds -lt 85)
if (-not $guardian -or $guardian.ProcessId -eq $oldGuardian) { throw 'The Windows scheduled task did not restore the guardian.' }
$worker = Get-Worker
if (-not $worker -or $worker.ProcessId -ne $workerBefore) { throw 'Existing worker was not preserved during guardian recovery.' }
$results.GuardianRecovery = [ordered]@{ OldPid = $oldGuardian; NewPid = $guardian.ProcessId; Seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 2); ExistingWorkerPreserved = $true }
Write-Output 'PASS Windows task automatically restored guardian and reattached existing worker'
Start-Process -FilePath $installedExe -ArgumentList '--stop' -WindowStyle Hidden -Wait
$timer.Restart()
while ((Get-EyeProcesses).Count -gt 0 -and $timer.Elapsed.TotalSeconds -lt 12) { Start-Sleep -Milliseconds 500 }
if ((Get-EyeProcesses).Count -gt 0) { throw 'Intentional shutdown did not stop both processes.' }
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$null = $scheduler.GetFolder('\').GetTask('EyeKeeper Recovery').Run($null)
Start-Sleep -Seconds 3
if ((Get-EyeProcesses).Count -gt 0) { throw 'Windows task ignored intentional shutdown.' }
$results.IntentionalQuitRespected = $true
Write-Output 'PASS intentional shutdown remains stopped even when recovery task runs'
Start-Process -FilePath $installedExe -ArgumentList '--background' -WindowStyle Hidden
$worker = Wait-Worker
$results.RunningWorker = $worker.ProcessId
$results.RunningGuardian = (Get-Guardian).ProcessId
$results.CompletedAt = [DateTimeOffset]::Now.ToString('O')
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\recovery-tests.json') -Encoding utf8
Write-Output 'PASS normal launch resumes background protection'
Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\recovery-tests.json')
