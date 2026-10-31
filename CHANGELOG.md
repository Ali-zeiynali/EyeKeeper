# Changelog

## [1.2.0](https://github.com/Ali-zeiynali/EyeKeeper/releases/tag/v1.2.0) â€” 2026-10-31

### Sound and countdown

- Locally synthesized, soft warning/start/completion cues and optional rest-second ticks.
- One opening chime for the warning capsule; its remaining seconds stay silent.
- Ring pulse and ripple synchronized with each rest countdown second.
- Independent sound/tick toggles and volume control, with shared playback across monitors.

### Interaction

- Temporary Escape hotkey dismisses an active warning or rest without requiring overlay focus.
- Normal background startup does not open the optional developer sound-lab window.
- Separate Close menu and confirmed full-Quit actions.

### Windows distribution

- Per-user Setup EXE with startup selection, Start menu shortcut, recovery task and uninstall support.
- Self-contained Windows x64 ZIP, installation guide and SHA-256 checksums.
- Existing preferences are retained on Setup updates except for the selected startup option; uninstall retains settings and logs.
- 32 application regression tests and an isolated installation/uninstallation smoke test passed for the release.

Also included: the procedural liquid renderer, 18 atmospheres, 8 motion materials, per-monitor center expansion, downward liquid exit and background guardian recovery.
