$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    # The eight optimized shader binaries are checked in. Recompile after HLSL edits
    # with Windows SDK fxc /T ps_3_0 /E main /O3 /D STYLE=0..7.
    dotnet build src/EyeKeeper/EyeKeeper.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet src/EyeKeeper/bin/Release/net10.0-windows/EyeKeeper.dll --render-previews artifacts/previews
    if ($LASTEXITCODE -ne 0) { throw 'Offscreen rendering failed' }
    New-Item -ItemType Directory -Force src/EyeKeeper/Assets | Out-Null
    Copy-Item -LiteralPath artifacts/previews/eyekeeper.ico -Destination src/EyeKeeper/Assets/eyekeeper.ico
    dotnet run --project tests/EyeKeeper.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    if (Get-Command python -ErrorAction SilentlyContinue) {
        python scripts/make-preview-animation.py
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Optional animation packaging needs Python with Pillow.' }
    }
    dotnet publish src/EyeKeeper/EyeKeeper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/app
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Write-Host 'Built artifacts/app/EyeKeeper.exe. The background app has NOT been launched.'
} finally { Pop-Location }
