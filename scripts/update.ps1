$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseExe = Join-Path $projectRoot 'artifacts\app\EyeKeeper.exe'
$installedExe = Join-Path $env:LOCALAPPDATA 'Programs\EyeKeeper\EyeKeeper.exe'
if (-not (Test-Path -LiteralPath $installedExe)) { throw 'EyeKeeper is not installed. Use install.ps1.' }
$releaseHash = (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash
$settingsPath = Join-Path $env:LOCALAPPDATA 'EyeKeeper\settings.json'
$beforeSettings = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
$beforePreferences = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json } else { $null }
Start-Process -FilePath $installedExe -ArgumentList '--stop' -WindowStyle Hidden -Wait
Start-Sleep -Seconds 1
Get-Process -Name EyeKeeper -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe } | ForEach-Object {
    Stop-Process -Id $_.Id
    $_.WaitForExit(5000) | Out-Null
}
$stagedExe = $installedExe + '.new'
$rollbackExe = Join-Path (Split-Path -Parent $installedExe) ('EyeKeeper.previous-' + (Get-Date).ToString('yyyyMMddHHmmssfff') + '.exe')
Copy-Item -LiteralPath $releaseExe -Destination $stagedExe -Force
if ((Get-FileHash -LiteralPath $stagedExe -Algorithm SHA256).Hash -ne $releaseHash) { throw 'Staged executable hash mismatch' }
$replaced = $false
try {
    Move-Item -LiteralPath $installedExe -Destination $rollbackExe
    $replaced = $true
    Move-Item -LiteralPath $stagedExe -Destination $installedExe
    if ((Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash -ne $releaseHash) { throw 'Executable hash mismatch' }
} catch {
    if ($replaced -and -not (Test-Path -LiteralPath $installedExe)) { Move-Item -LiteralPath $rollbackExe -Destination $installedExe }
    if (Test-Path -LiteralPath $installedExe) { Start-Process -FilePath $installedExe -ArgumentList '--background' -WindowStyle Hidden }
    throw
}
Start-Process -FilePath $installedExe -ArgumentList '--background' -WindowStyle Hidden
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'install-recovery.ps1')) { & (Join-Path $PSScriptRoot 'install-recovery.ps1') }
$afterSettings = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
$existingPreferencesPreserved = $true
if ($beforePreferences) {
    $afterPreferences = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    foreach ($property in $beforePreferences.PSObject.Properties) {
        if (-not $afterPreferences.PSObject.Properties[$property.Name] -or $afterPreferences.($property.Name) -ne $property.Value) { $existingPreferencesPreserved = $false }
    }
}
[pscustomobject]@{
    Version = (Get-Item -LiteralPath $installedExe).VersionInfo.ProductVersion
    InstalledExe = $installedExe
    SHA256 = $releaseHash
    SettingsPreserved = $existingPreferencesPreserved
    SettingsBytesUnchanged = $beforeSettings -eq $afterSettings
    BackgroundLaunched = $true
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\update-receipt.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\update-receipt.json')
