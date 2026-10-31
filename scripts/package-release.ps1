param([string]$Compiler = 'ISCC.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src/EyeKeeper/EyeKeeper.csproj') -Raw)).Project.PropertyGroup.Version
$releaseDirectory = Join-Path $projectRoot 'artifacts/release'
$packageDirectory = Join-Path $projectRoot 'artifacts/portable'
New-Item -ItemType Directory -Force $releaseDirectory, $packageDirectory | Out-Null
& dotnet publish (Join-Path $projectRoot 'src/EyeKeeper/EyeKeeper.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $projectRoot 'artifacts/app')
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/app/EyeKeeper.exe') -Destination (Join-Path $packageDirectory 'EyeKeeper.exe') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/WINDOWS-RELEASE.md') -Destination (Join-Path $packageDirectory 'README.md') -Force
$archive = Join-Path $releaseDirectory "EyeKeeper-$version-windows-x64.zip"
Compress-Archive -LiteralPath (Join-Path $packageDirectory 'EyeKeeper.exe'), (Join-Path $packageDirectory 'README.md') -DestinationPath $archive -Force
& $Compiler "/DAppVersion=$version" (Join-Path $PSScriptRoot 'windows-installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/WINDOWS-RELEASE.md') -Destination (Join-Path $releaseDirectory 'WINDOWS-README.md') -Force
$names = @("EyeKeeper-$version-windows-x64-setup.exe", "EyeKeeper-$version-windows-x64.zip", 'WINDOWS-README.md')
$checksums = foreach ($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
$checksums | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding ascii
Get-ChildItem -LiteralPath $releaseDirectory | Select-Object Name, Length
