# EyeKeeper 1.2.0 for Windows

Download the files from https://github.com/Ali-zeiynali/EyeKeeper/releases/latest.

## Requirements

Windows 10 version 1809 or newer, or Windows 11, x64. The application includes its .NET 10 runtime; no SDK or separate runtime installation is required. This release is not code-signed, so Windows may display an unknown-publisher or SmartScreen prompt.

## Recommended installation

Run `EyeKeeper-1.2.0-windows-x64-setup.exe`. It installs for your account without administrator permissions, adds a Start menu shortcut and an uninstall entry in Windows Settings. The startup checkbox controls launch at login. The guardian and current-user recovery task restore unexpected exits while respecting intentional Quit and your startup preference.

Setup preserves existing preferences except for the startup checkbox selection. Updates stop the running installed copy before replacing its executable. Select **Start EyeKeeper** on the final page to launch; silent installation does not auto-launch. The tray icon may be under the overflow arrow.

To uninstall, use Windows Settings → Apps → EyeKeeper → Uninstall. The uninstaller stops this installation and removes its startup entry, recovery task, shortcut and installed files. Preferences and logs in `%LOCALAPPDATA%\EyeKeeper` are retained.

## ZIP package

Extract `EyeKeeper-1.2.0-windows-x64.zip` to a permanent folder and run `EyeKeeper.exe`. The ZIP does not register an uninstaller, Start menu shortcut or scheduled recovery task. Preferences still live in `%LOCALAPPDATA%\EyeKeeper`; this is not an isolated portable profile. The app's startup preference can register this executable at login. Turn off **Launch with Windows** and quit before moving or deleting the extracted files. Use Setup for complete integration.

## Controls

- Click or right-click the tray icon for settings. The default rhythm is 20 minutes of work and 20 seconds of rest, preceded by a silent 30-second countdown with one opening chime.
- Choose among 18 color atmospheres and 8 animated liquid materials. Animation, ring pulse and sound settings are independent.
- The full-screen rest uses soft second ticks and a pulsing ring; each monitor expands from its own center and drains downward when finished.
- **Esc** dismisses a warning or rest even without focus. **S** snoozes while the rest window has focus; the on-screen action is also available.
- **Close menu** leaves reminders running. **Application → Quit EyeKeeper completely…** asks for confirmation and tells recovery to stay stopped. Relaunching normally resumes protection.

## Verification and troubleshooting

Compare `Get-FileHash .\EyeKeeper-1.2.0-windows-x64-setup.exe -Algorithm SHA256` with the corresponding entry in `SHA256SUMS.txt`. Hashes detect changed files; they are not a code-signing signature.

Errors and lifecycle events are recorded in `%LOCALAPPDATA%\EyeKeeper\error.log` and `lifecycle.log`. Include relevant error messages and your Windows version in GitHub issues; inspect logs for private information before sharing. The app performs no screen capture, input tracking or network requests.

## Linux status

There is no Linux build in this release. The current WPF/Win32/DirectX renderer, tray, monitor overlays, hotkey and recovery integrations require Windows. A supported Linux release needs a UI/platform port, not just a different .NET runtime target. Wine has not been tested and no compatibility package is advertised as working. See https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview.

## Build the same packages

On Windows, install the .NET 10 SDK and Inno Setup 6.7.4 or compatible. From the repository root:

```powershell
dotnet run --project tests/EyeKeeper.Tests -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1 -Compiler 'C:\Path\To\ISCC.exe'
```

Outputs are in `artifacts/release`. Source archives are provided separately by GitHub alongside the release tag.
