$ErrorActionPreference = 'Stop'
$installedExe = Join-Path $env:LOCALAPPDATA 'Programs\EyeKeeper\EyeKeeper.exe'
if (-not (Test-Path -LiteralPath $installedExe)) { throw 'Install EyeKeeper first.' }
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$task = $scheduler.NewTask(0)
$task.RegistrationInfo.Description = 'Restarts the EyeKeeper guardian if it stops unexpectedly. Intentional Quit and the Launch with Windows preference are respected.'
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
$loginTrigger = $task.Triggers.Create(9)
$loginTrigger.UserId = $account
$loginTrigger.Delay = 'PT10S'
$timerTrigger = $task.Triggers.Create(1)
$timerTrigger.StartBoundary = (Get-Date).AddMinutes(1).ToString('yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
$timerTrigger.Repetition.Interval = 'PT1M'
$action = $task.Actions.Create(0)
$action.Path = $installedExe
$action.Arguments = '--watchdog-autostart'
$action.WorkingDirectory = Split-Path -Parent $installedExe
$registered = $scheduler.GetFolder('\').RegisterTaskDefinition('EyeKeeper Recovery', $task, 6, $account, $null, 3)
[pscustomobject]@{ Name = $registered.Name; Enabled = $registered.Enabled; NextRunTime = $registered.NextRunTime; RunLevel = 'Current user, no elevation'; Interval = '1 minute'; IntentionalQuitRespected = $true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\recovery-task.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\recovery-task.json')
