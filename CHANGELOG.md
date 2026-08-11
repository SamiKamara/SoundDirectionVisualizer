# Changelog

All notable Sound Direction Visualizer changes are documented here. Versions follow semantic versioning.

## Unreleased

### Added

- Automatic best-available detected-game process capture that requests standard 7.1/5.1 layouts and promotes them only after useful independent side/rear content is verified
- Platform-independent all-channel analysis, multichannel direction estimation, stereo fold-down, validation, fallback status, and deterministic 5.1/7.1 tests
- Optional debug setting that forces an available multichannel game-process source while keeping validation-driven estimator honesty
- Live Status tab with capture policy, source, estimator, validation, retry timing, fallback reasons, and an in-memory session event log
- Debug-force Status visualization with live aggregate dBFS meters for every monitored channel, including diagnostic LFE

### Changed

- Endpoint stereo now remains active during bounded automatic multichannel probing, preserving explicit front/back ambiguity for unavailable, malformed, silent, copied, or stereo-derived surround content
- Capture diagnostics, the Status tab, and the tray audio row now report source policy, layout, estimator, validation, retry, and fallback state

## [1.0.2] - 2026-08-11

### Changed

- Reworked the application, tray, shortcut, and README icon around a compact cyan direction ring with prominent mirrored markers

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
