param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceExe = Join-Path $projectRoot 'artifacts\app\EyeKeeper.exe'
if (-not (Test-Path -LiteralPath $sourceExe)) { throw 'Build the release first with scripts/build.ps1.' }
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\EyeKeeper'
$installedExe = Join-Path $installDirectory 'EyeKeeper.exe'
$allowedPaths = @([IO.Path]::GetFullPath($sourceExe), [IO.Path]::GetFullPath($installedExe))
if (Test-Path -LiteralPath $installedExe) { Start-Process -FilePath $installedExe -ArgumentList '--stop' -WindowStyle Hidden -Wait; Start-Sleep -Seconds 1 }
Get-Process -Name EyeKeeper -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Path -and $allowedPaths -contains [IO.Path]::GetFullPath($_.Path)) { Stop-Process -Id $_.Id; $_.WaitForExit(5000) | Out-Null }
}
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
if (Test-Path -LiteralPath $installedExe) {
    $previousExe = Join-Path $installDirectory ('EyeKeeper.previous-' + [Guid]::NewGuid().ToString('N') + '.exe')
    Move-Item -LiteralPath $installedExe -Destination $previousExe
}
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\EyeKeeper\Assets\eyekeeper.ico') -Destination (Join-Path $installDirectory 'eyekeeper.ico') -Force
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'EyeKeeper'
New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
$settingsPath = Join-Path $settingsDirectory 'settings.json'
$settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{ WorkMinutes = 20; RestSeconds = 20; Palette = 'Random'; MotionStyle = 'Random' } }
foreach ($name in @('StartWithWindows', 'FlowingBackground', 'BreathingRing', 'LiquidTransitions')) { $settings | Add-Member -NotePropertyName $name -NotePropertyValue $true -Force }
$settings | Add-Member -NotePropertyName ReducedMotion -NotePropertyValue $false -Force
$settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (-not (Test-Path -LiteralPath $startupKey)) { New-Item -Path $startupKey | Out-Null }
New-ItemProperty -LiteralPath $startupKey -Name EyeKeeper -Value ('"' + $installedExe + '" --background') -PropertyType String -Force | Out-Null
$programsDirectory = [Environment]::GetFolderPath('Programs')
$shortcutFile = Join-Path $programsDirectory 'EyeKeeper.lnk'
$shellLink = New-Object -ComObject WScript.Shell
$shortcut = $shellLink.CreateShortcut($shortcutFile)
$shortcut.TargetPath = $installedExe
$shortcut.Arguments = '--background'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.IconLocation = (Join-Path $installDirectory 'eyekeeper.ico')
$shortcut.Description = 'EyeKeeper - a quiet moment for your eyes.'
$shortcut.Save()
if (-not $NoLaunch) { Start-Process -FilePath $installedExe -ArgumentList '--background' -WindowStyle Hidden }
else { Set-Content -LiteralPath (Join-Path $settingsDirectory 'intentional-stop') -Value 'Installed without launching' -Encoding utf8 }
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'install-recovery.ps1')) { & (Join-Path $PSScriptRoot 'install-recovery.ps1') }
$receipt = [pscustomobject]@{
    InstalledExe = $installedExe
    StartMenuShortcut = $shortcutFile
    StartupEnabled = $true
    BackgroundLaunched = -not $NoLaunch
    AllAnimationsEnabled = $true
    SHA256 = (Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\installation.json') -Encoding utf8
$receipt
