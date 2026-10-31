# Build and develop EyeKeeper

[← Documentation](README.md) · [Contributing](../CONTRIBUTING.md)

## Toolchain

- Windows 10/11 x64 with the .NET 10 SDK.
- Inno Setup 6.7.4 or compatible to create the Setup EXE.
- Windows SDK `fxc.exe` only when changing HLSL. Compiled shaders are checked in.
- Optional: Python + Pillow to package rendered frames into animated previews. They are not application dependencies.

## Build without launching

```powershell
dotnet build src/EyeKeeper/EyeKeeper.csproj -c Release
dotnet run --project tests/EyeKeeper.Tests -c Release
dotnet publish src/EyeKeeper/EyeKeeper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/app
```

The production output is `artifacts/app/EyeKeeper.exe`. Normal builds leave `SOUND_LAB_BUILD` undefined. Do not publish a developer lab build as a production release.

For the complete local pipeline, including offscreen renders and optional animation packaging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

Neither path launches the tray app. Explicitly running `EyeKeeper.exe --background` starts it. A second ordinary launch exits without duplicating the running instance.

## Source map

| File | Responsibility |
| :--- | :--- |
| [`Engine.cs`](../src/EyeKeeper/Engine.cs) | Monotonic deadlines, phase transitions, preferences and palettes. |
| [`Program.cs`](../src/EyeKeeper/Program.cs) | Mode dispatch, tray host, settings application and session lifecycle. |
| [`Supervisor.cs`](../src/EyeKeeper/Supervisor.cs) | Worker/guardian singleton roles, UI heartbeat, reattachment and bounded recovery delays. |
| [`Overlay.cs`](../src/EyeKeeper/Overlay.cs) | Per-display overlay windows, render timing and shared audio coordination. |
| [`Visuals.cs`](../src/EyeKeeper/Visuals.cs) | Capsule, countdown ring, center expansion and downward liquid exit. |
| [`FluidEffect.cs`](../src/EyeKeeper/FluidEffect.cs) | GPU material and shader capability selection. |
| [`FluidTexture.cs`](../src/EyeKeeper/FluidTexture.cs) | CPU reference rasterizer and fallback material. |
| [`FluidStyles.cs`](../src/EyeKeeper/FluidStyles.cs) | Material selection and randomization. |
| [`Sounds.cs`](../src/EyeKeeper/Sounds.cs) | Cue clock, pulse envelope, PCM synthesis and playback cache. |
| [`Native.cs`](../src/EyeKeeper/Native.cs) | Monitor placement, window styles and temporary Escape registration. |
| [`Panel.cs`](../src/EyeKeeper/Panel.cs) | English tray preferences and separate menu-close/application-quit actions. |
| [`SafeWindow.cs`](../src/EyeKeeper/SafeWindow.cs) | Guards against repeated or reentrant window closure. |
| [`Storage.cs`](../src/EyeKeeper/Storage.cs) | Local preferences, startup entry and bounded lifecycle logging. |
| [`Preview.cs`](../src/EyeKeeper/Preview.cs) / [`SoundLab.cs`](../src/EyeKeeper/SoundLab.cs) | Explicit offscreen visual and interactive audio development tools. |

The scheduling engine uses monotonic time, so wall-clock edits do not trigger or prolong a break. Each monitor gets an overlay with its own expansion origin; audio is coordinated once for the overlay group.

## Rendering and shaders

The primary material renderer uses WPF rendering tier 2 and Pixel Shader 3 when available. Eight separately compiled shader variants remove unused style branches. The CPU reference is used for previews and for a lower-resolution background-worker fallback on unsupported hardware or remote sessions. Continuous render handlers are detached after rest and exit; the normal scheduler ticks four times a second.

After editing `src/EyeKeeper/Assets/Fluid.hlsl`, rebuild all eight variants using the Windows SDK compiler:

```powershell
0..7 | ForEach-Object {
    & fxc.exe /T ps_3_0 /E main /O3 /D "STYLE=$_" /Fo "src/EyeKeeper/Assets/Fluid$_.ps" src/EyeKeeper/Assets/Fluid.hlsl
    if ($LASTEXITCODE -ne 0) { throw "Shader $_ failed" }
}
```

Keep the CPU reference and GPU formulas consistent. Compare multiple time samples, palettes and styles; one attractive still does not establish animated parity.

References: [WPF hardware rendering](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware), [ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect), [per-monitor DPI examples](https://github.com/microsoft/WPF-Samples/tree/main/PerMonitorDPI).

## Preview and diagnostic modes

```powershell
# Render files without showing the application or modifying user settings/startup.
dotnet src/EyeKeeper/bin/Release/net10.0-windows/EyeKeeper.dll --render-previews artifacts/previews

# Record rendering capability without opening a window.
artifacts/app/EyeKeeper.exe --diagnostics artifacts/diagnostics.json
```

`--demo` is an explicit on-screen demonstration: quit a running instance first. It shows the warning and rest, then remains in the tray on the ordinary schedule.

`--sound-lab <output-folder>` opens a separate developer preview with temporary preferences and exported WAV cues. It does not modify installed settings, startup or guardian integration. Normal startup never opens the lab window.

## Validation

The dependency-free test executable covers scheduling, settings, random selection, procedural material variation and animation, safe window closure, independent Close menu/Quit behavior, cue deduplication, silent warning seconds, focus-independent Escape registration/release, pulse timing and bounded/tapered PCM synthesis. Window regressions run offscreen.

```powershell
dotnet run --project tests/EyeKeeper.Tests -c Release
```

The Windows GitHub Actions workflow runs this suite and publishes a self-contained build artifact. Release 1.2.0 passed 32 regression tests and an isolated installer/uninstaller smoke test. These are not a physical multi-monitor or universal performance certification.

`scripts/test-recovery.ps1` is a separate installed-app integration test. It deliberately terminates the worker and guardian, verifies restart/reattachment and intentional shutdown, then resumes protection. Run it only when those interruptions are appropriate.

## Package a release

Start from a clean, committed source tree and run the tests. Then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1 -Compiler 'C:\Path\To\ISCC.exe'
```

The script reads the project version, publishes the self-contained x64 app, creates the ZIP and Setup EXE, copies the Windows guide and generates `SHA256SUMS.txt` under `artifacts/release`. It does not sign, tag, push or publish automatically.

The installer definition is `scripts/windows-installer.iss`; `scripts/release-integration.ps1` handles scoped shutdown, startup and recovery-task installation/removal. Smoke-test installation and uninstall with a separate app identity/directory before publication. Verify asset hashes after upload and keep release tags attached to the source used for the binary.

The source-oriented `install.ps1` and `update.ps1` scripts are local development utilities. End users should follow the [release installation guide](WINDOWS-RELEASE.md).

## Local files and platform scope

Generated builds, previews, diagnostics, receipts and logs under `artifacts/` are ignored by Git. Do not add settings, credentials, machine-specific paths or runtime logs to source commits.

The app targets `net10.0-windows` and depends on WPF, Win32 windowing, tray integration and Windows recovery APIs. Linux requires replacing these platform layers and validating the renderer, hotkeys and multi-monitor behavior; changing the runtime identifier alone is insufficient.
