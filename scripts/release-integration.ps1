param(
    [ValidateSet('Install', 'Remove', 'Stop')][string]$Mode,
    [Parameter(Mandatory)][string]$Executable,
    [ValidateSet(0, 1)][int]$Startup = 1
)
$ErrorActionPreference = 'Stop'
$Executable = [IO.Path]::GetFullPath($Executable)
$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'EyeKeeper'

if ($Mode -in @('Stop', 'Remove')) {
    # Do not stop a different installation when testing an isolated installer.
    $running = @(Get-Process -Name EyeKeeper -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Executable })
    if ($running.Count) {
        Start-Process -FilePath $Executable -ArgumentList '--stop' -WindowStyle Hidden -Wait
        foreach ($process in $running) {
            if (-not $process.WaitForExit(10000)) { throw 'EyeKeeper is still running. Quit it before continuing.' }
        }
    }
    if ($Mode -eq 'Stop') { exit 0 }
    $entry = (Get-ItemProperty -LiteralPath $startupKey -Name EyeKeeper -ErrorAction SilentlyContinue).EyeKeeper
    if ($entry -eq ('"' + $Executable + '" --background')) {
        Remove-ItemProperty -LiteralPath $startupKey -Name EyeKeeper
    }
}

$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$folder = $scheduler.GetFolder('\')
if ($Mode -eq 'Remove') {
    try { $existing = $folder.GetTask('EyeKeeper Recovery') } catch { $existing = $null }
    if ($existing -and $existing.Definition.Actions.Item(1).Path -eq $Executable) {
        $folder.DeleteTask('EyeKeeper Recovery', 0)
    }
    # Preferences and logs deliberately survive uninstall.
    exit 0
}

New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
$settingsPath = Join-Path $settingsDirectory 'settings.json'
$settings = if (Test-Path -LiteralPath $settingsPath) {
    Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
} else { [pscustomobject]@{} }
$settings | Add-Member -NotePropertyName StartWithWindows -NotePropertyValue ([bool]$Startup) -Force
$settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
if ($Startup) {
    if (-not (Test-Path -LiteralPath $startupKey)) { New-Item -Path $startupKey | Out-Null }
    New-ItemProperty -LiteralPath $startupKey -Name EyeKeeper -Value ('"' + $Executable + '" --background') -PropertyType String -Force | Out-Null
} else {
    Remove-ItemProperty -LiteralPath $startupKey -Name EyeKeeper -ErrorAction SilentlyContinue
}

$task = $scheduler.NewTask(0)
$task.RegistrationInfo.Description = 'EyeKeeper guardian recovery; respects intentional Quit and the startup preference.'
$account = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$task.Principal.UserId = $account
$task.Principal.LogonType = 3
$task.Principal.RunLevel = 0
$task.Settings.Enabled = $true
$task.Settings.MultipleInstances = 2
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.StartWhenAvailable = $true
$task.Settings.ExecutionTimeLimit = 'PT0S'
$task.Settings.RestartCount = 3
$task.Settings.RestartInterval = 'PT1M'
$login = $task.Triggers.Create(9)
$login.UserId = $account
$login.Delay = 'PT10S'
$timer = $task.Triggers.Create(1)
$timer.StartBoundary = (Get-Date).AddMinutes(1).ToString('yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
$timer.Repetition.Interval = 'PT1M'
$action = $task.Actions.Create(0)
$action.Path = $Executable
$action.Arguments = '--watchdog-autostart'
$action.WorkingDirectory = Split-Path -Parent $Executable
$folder.RegisterTaskDefinition('EyeKeeper Recovery', $task, 6, $account, $null, 3) | Out-Null
