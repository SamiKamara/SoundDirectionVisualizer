# Changelog

All notable Sound Direction Visualizer changes are documented here. Versions follow semantic versioning.

## [1.0.1] - 2026-08-11

### Changed

- Replaced low-contrast system checkbox glyphs with dark checkmarks on the dark UI's cyan selection fill
- Vertically centered the text in global-hotkey fields while preserving keyboard capture and focus behavior
- Removed unnecessary references to the maintainer's other projects from the README

## [1.0.0] - 2026-08-11

### Added

- Selected/default stereo WASAPI loopback capture with active-endpoint discovery
- Optional detected-game process capture and automatic fallback for sustained centered Steam-game output
- Deterministic RMS direction analysis with adaptive silence-gate and stereo-width calibration
- Explicit front/back candidate pairs for stereo input
- Relative loud-sound emphasis with independently styled current and delayed markers
- Click-through, no-activation compass overlay with configurable layers and history
- Automatic Steam-game display targeting, manual display selection, and global hotkeys
- Responsive dark-mode settings window and notification-area menu
- Persistent local settings and aggregate-only live capture diagnostics
- Automated Release builds, tests, tagged GitHub Releases, and SHA-256 checksums
