# Make EyeKeeper yours

[← Documentation](README.md) · [Install](WINDOWS-RELEASE.md) · [Troubleshooting](TROUBLESHOOTING.md)

## The everyday flow

EyeKeeper lives in the Windows notification area. Click or right-click the eye icon to open its small preferences panel. If you cannot see it, open the tray overflow arrow.

The work timer runs quietly. In the final 30 seconds, a capsule appears with one opening chime; its countdown stays silent. At the deadline, each monitor unfolds from its own center into a full-screen rest. The rest countdown has optional soft ticks and a pulse at each second. At completion, the surface drains downward and the next work interval begins.

The default is 20 minutes of work followed by 20 seconds of rest. The warning is part of the work interval, not another 30 seconds added to it. During the rest, look at something far away.

![Warning capsule](previews/08-warning-capsule.png)

## Rhythm and controls

| Setting or action | Behavior |
| :--- | :--- |
| Work interval | 1–180 minutes. |
| Rest length | 5–300 seconds. |
| Save preferences | Applies your choices and starts a fresh work interval. |
| Take a break | Starts a rest immediately. |
| Pause 30 min | Holds the schedule and resumes automatically after 30 minutes. |
| Resume | Resumes a paused schedule early. |
| Snooze | Starts a five-minute work countdown before the next rest. |
| Close menu | Hides the panel; it does not stop reminders. |

**Esc** dismisses an active warning or rest without requiring overlay focus. This is a temporary Windows hotkey, registered only while the reminder is visible and released when dismissed. **S** snoozes when the rest window has focus. The footer also has clickable skip and snooze actions.

After waking from sleep or unlocking Windows, an active work schedule starts fresh to avoid an overdue rest covering the desktop unexpectedly. A manual pause is retained.

## Color and movement

Choose a fixed atmosphere or use **Random**, **Random light**, **Random dark** or **Random vivid**. Choose a fixed motion style or randomize that independently. Each random picker avoids immediately repeating its own previous selection.

All 18 atmospheres work with all 8 liquid materials. Visit the [visual gallery](GALLERY.md) to compare them.

Under **Animation details**, control:

- **Background flow** — continuous movement of the procedural material.
- **Ring pulse** — the pulse and ripple synchronized with each countdown number.
- **Liquid transitions** — the entrance and downward exit movement.

**Reduced motion** turns movement off as a group. You can keep the visual reminder and countdown without the animated effects.

## Sound

The **Sound** section has a master sound toggle, a **Rest countdown ticks** toggle and a volume slider. The volume setting changes EyeKeeper's generated audio, not Windows' master volume.

| Moment | Sound |
| :--- | :--- |
| Warning appears | One short opening chime. |
| Warning counts down | Silent. |
| Rest begins | A distinct start cue. |
| Rest counts down | Soft second ticks, if enabled. |
| Rest completes naturally | A completion cue. |
| Rest is skipped or paused | No completion cue. |

Sounds are synthesized locally and cached. Multiple monitors share one sound controller, so each display does not play a duplicate tick.

## Startup, recovery and full exit

**Launch with Windows** controls the current-user login entry and whether the recovery task is allowed to start the guardian automatically. Setup creates the **EyeKeeper Recovery** task; the ZIP by itself does not install that task.

EyeKeeper normally has two processes: the user-visible worker and a separate guardian. The guardian can restart an unexpectedly exited worker. It can also detect a UI heartbeat stalled for 60 seconds, excluding sleep/resume gaps. The recovery task checks once a minute and at login to restore a missing guardian.

To fully exit, use **Application → Quit EyeKeeper completely…** and confirm. Intentional Quit tells recovery to remain stopped. Launching EyeKeeper normally resumes protection. Recovery is not a guarantee against every OS or application failure.

## Where your settings live

Preferences are stored at `%LOCALAPPDATA%\EyeKeeper\settings.json`. Uninstall retains preferences and logs. For log locations and reset steps, see [troubleshooting](TROUBLESHOOTING.md#logs-and-local-files).
