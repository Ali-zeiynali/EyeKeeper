# Troubleshooting and FAQ

[← Documentation](README.md) · [Controls](USER-GUIDE.md) · [Installation](WINDOWS-RELEASE.md)

## I launched it, but no window appeared

That is normal. EyeKeeper is a tray app. Look for the eye icon in the notification area, including the overflow arrow, and click or right-click it. A second normal launch exits without duplicating the existing worker.

The **Sound & pulse lab** window belongs to the explicit `--sound-lab` developer mode. Normal startup should not show it; check that a custom shortcut does not pass that argument.

## It disappeared after I clicked Close menu

**Close menu** hides only the preferences panel. Reminders continue. To stop the application, open **Application → Quit EyeKeeper completely…** and confirm. A confirmed Quit prevents recovery from restarting it. Launch EyeKeeper normally when you want protection to resume.

## Escape did not dismiss a reminder

Use the latest release. Escape is temporarily registered as a Windows hotkey while a warning or rest is active, so overlay focus should not be required. Another application holding the same hotkey can prevent registration; a registration failure is logged. The rest footer provides a clickable skip action as well.

The **S** shortcut requires rest-window focus. Use the clickable snooze action if another window has focus.

## The sound is missing, or I want fewer sounds

Open **Sound** in the tray panel. Check the master toggle, volume slider and **Rest countdown ticks**, then save. Also check the Windows volume mixer and active output device. Warning countdown seconds are intentionally silent; only the capsule's opening chime plays. Skipping a rest does not play the completion cue.

## Motion feels too busy or rendering is slow

Enable **Reduced motion**, or individually disable background flow, ring pulse and liquid transitions in **Animation details**. Save to apply. Rendering capabilities vary by GPU, driver and remote-desktop session; the app uses a lower-resolution CPU fallback when its GPU path is unavailable. There is no guaranteed frame rate across all hardware.

For a diagnostic file without opening a window:

```powershell
& "$env:LOCALAPPDATA\Programs\EyeKeeper\EyeKeeper.exe" --diagnostics "$env:TEMP\EyeKeeper-diagnostics.json"
```

## Why are there two EyeKeeper processes?

Normal installed operation uses a worker and a guardian. The guardian restarts unexpected worker exits and checks the UI heartbeat. Setup also creates **EyeKeeper Recovery**, a current-user scheduled task that can restore the guardian. It runs once a minute and at login without administrative privileges.

**Launch with Windows** and confirmed Quit control automatic recovery. The ZIP alone does not install the recovery task. After a confirmed Quit, start the app normally to resume; an intentional exit is not treated as a crash.

## Startup does not work

Check **Launch with Windows** in preferences and save. Keep a ZIP installation in a permanent location: its startup entry points to that executable. Use the Setup EXE for a Start menu shortcut and scheduled recovery integration. Windows or another tool may disable startup entries; check Windows startup-app settings as well.

## How do I reset preferences?

First quit the app completely and confirm. Back up `%LOCALAPPDATA%\EyeKeeper\settings.json`, then rename the original, for example to `settings.backup.json`. Start EyeKeeper again to load defaults. This resets app preferences; it does not uninstall the app or erase logs. Startup/recovery integration can be repaired by running Setup again.

## Logs and local files

All paths below are under `%LOCALAPPDATA%\EyeKeeper`:

| File | Purpose |
| :--- | :--- |
| `settings.json` | Saved preferences. |
| `error.log` | Exceptions and integration failures. |
| `lifecycle.log` | Starts, recovery events and intentional exits. |
| `worker.json` | Worker identity used to validate guardian reattachment. |
| `intentional-stop` | Marker that tells recovery to respect an intentional exit. |

Do not edit worker identity or stop markers while the app is running. A normal launch handles resuming after intentional Quit.

## Can I use it on Linux or macOS?

There is no supported build for either platform. WPF, Win32 overlay/hotkey handling and Windows recovery integration need a platform port. Wine has not been verified. See the [development guide](DEVELOPMENT.md#local-files-and-platform-scope).

## Report a reproducible problem

[Open an issue](https://github.com/Ali-zeiynali/EyeKeeper/issues/new) with your app version, Windows version, installation type, monitor count/scaling, reproduction steps and expected/actual behavior. For visual issues, a screenshot or short clip helps. Include relevant log excerpts after checking them for private information. See [contributing](../CONTRIBUTING.md) for more detail.
