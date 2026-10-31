# Contributing to EyeKeeper

Thanks for helping make screen breaks a little calmer. Focused bug reports, visual feedback and small, well-tested pull requests are welcome.

## Report a bug

Check the [troubleshooting guide](docs/TROUBLESHOOTING.md) and existing issues first. When opening an issue, include:

- EyeKeeper version, Windows version and Setup/ZIP/source installation.
- Steps to reproduce, what you expected, and what actually happened.
- Monitor count, resolution and scaling for overlay or animation issues.
- Relevant log excerpts; inspect them for private information before sharing.
- A screenshot or short recording when it explains the problem.

Please avoid posting credentials, full settings folders or unrelated personal information.

## Suggest a feature

Describe the workflow or problem first, then the proposed behavior. Include how it should work with multiple monitors, reduced motion, sound preferences and intentional Quit when those are relevant. Discuss larger platform ports or architecture changes in an issue before investing in a broad rewrite.

## Work on the source

1. Fork and clone the repository; create a focused branch.
2. Follow the [development guide](docs/DEVELOPMENT.md) to build on Windows with .NET 10.
3. Keep user-facing interface text and documentation in English.
4. Match the existing C# style and avoid unrelated formatting or generated-file changes.
5. Run the regression suite for behavior changes. Add targeted coverage when it protects a meaningful behavior.
6. Update the relevant guide for changes to controls, installation or command-line modes.

For visual changes, include before/after offscreen previews and explain motion behavior. Check both light and dark atmospheres and reduced motion. For shader changes, rebuild all affected compiled variants and keep the CPU reference consistent. For audio changes, preserve the silent warning countdown and avoid duplicate cues across monitors.

Do not commit `artifacts/`, runtime settings, logs, credentials or machine-specific output. Recovery integration tests intentionally terminate app processes; run them only when those interruptions are appropriate.

## Send a pull request

Explain the problem, the resulting behavior and the validation performed. Keep the scope easy to review. Mention any hardware-dependent behavior you could not verify. Do not change release tags or publish binaries as part of an ordinary source contribution.

For documentation-only changes, verify relative links, image paths, section anchors and GitHub rendering. Application regression tests are not required when no executable behavior changes.
