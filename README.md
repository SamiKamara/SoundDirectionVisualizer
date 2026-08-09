# Sound Direction Visualizer

Sound Direction Visualizer is a Windows accessibility overlay that estimates a sound's left/right direction from the audio currently being played and draws the result over a game. It runs as a notification-area application and does not take mouse or keyboard focus from the game.

The first version deliberately supports **stereo output only**. Stereo provides left/right balance but normally cannot distinguish whether the sound is in front of or behind the listener, so the overlay shows both mathematically valid directions. This limitation is visible instead of being hidden behind a false sense of precision.

## Current features

- WASAPI loopback capture from the default or a selected Windows output device
- RMS-based stereo direction estimate with automatic output-level and stereo-width calibration
- Optional manual smoothing, silence-threshold, and hard-pan calibration controls
- Click-through, always-on-top compass overlay with current rays and a fading history trail
- Live-previewed color, percentage opacity, display-relative whole-overlay size, thickness, marker size, labels, position, and history duration
- Independent visibility toggles for the compass ring, cardinal ticks, current rays, current markers, listener dot, history trail, and F/B/L/R labels
- Automatic display targeting for a detected Steam game window
- Manual display selection and a display-cycling hotkey
- Global hotkeys for toggling the overlay and opening settings
- A dedicated three-color application, tray, and shortcut icon
- Persistent settings in `%AppData%\SoundDirectionVisualizer\settings.json`
- A UI-independent analysis library with automated tests

## Requirements

- Windows 10 or newer
- .NET 9 SDK when running from source
- A stereo Windows output endpoint for version 1

## Run from source

```powershell
dotnet run --project .\src\SoundDirectionVisualizer.App\SoundDirectionVisualizer.App.csproj
```

The settings window opens on the first launch. Closing the settings window leaves the application in the notification area. Double-click the tray icon to reopen settings.

## Default hotkeys

- `Alt+D`: toggle the visualizer
- `Ctrl+Alt+F10`: cycle displays and switch to manual targeting
- `Ctrl+Alt+D`: open settings

All bindings can be changed. A binding can be cleared with Delete, except the required overlay toggle, which falls back to its default if invalid.

Automatic audio calibration is enabled by default. It scales the silence gate down for quiet loopback devices and learns the stereo width actually produced by the selected device and game. Calibration restarts when capture starts or the output device changes. It can be disabled on the Audio tab to use the manual silence-threshold and hard-pan values.

The overlay is enabled by default with a white color, 40% opacity, a size of 110% of the target display height, 3 px line thickness, an 8 px direction marker, zero horizontal and vertical offsets, and a five-second trail. Only the current direction markers and fading direction trail are visible by default; every layer can be enabled independently on the Overlay tab.

## How to read the overlay

- `F`, `B`, `L`, and `R` mean front, back, left, and right relative to the player.
- Bright rays and dots are the current direction candidates.
- Fading dots are recent candidates.
- Two candidates are normal for stereo because the same left/right balance fits a front and a back direction.
- A single side candidate appears near a modelled hard-left or hard-right pan.
- During silence the current rays disappear, while existing history fades out.

The estimator and its assumptions are documented in [docs/AUDIO-MODEL.md](docs/AUDIO-MODEL.md).

## Target display selection

With automatic targeting enabled, the application:

1. checks the foreground window;
2. resolves its process and executable path;
3. verifies that the executable is under a discovered Steam library's `steamapps\common` directory;
4. targets the display containing that window;
5. retains a recently detected game while its window remains visible and not minimized;
6. periodically scans running processes as a fallback.

If no game is detected, the current/manual display remains selected. Turning automatic targeting off makes the chosen display explicit and persistent.

The overlay works best with borderless-windowed games. Exclusive fullscreen, protected presentation paths, and some anti-cheat environments may prevent third-party topmost windows from appearing.

## Build, test, and publish

```powershell
dotnet build .\SoundDirectionVisualizer.sln --configuration Release
dotnet test .\SoundDirectionVisualizer.sln --configuration Release
.\scripts\publish-win-x64.ps1
```

Publishing creates a self-contained single-file executable under `artifacts\publish\win-x64`.

## Architecture and project policy

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes components and runtime flow.
- [docs/AUDIO-MODEL.md](docs/AUDIO-MODEL.md) defines the stereo math and its limitations.
- [docs/ROADMAP.md](docs/ROADMAP.md) records planned multichannel and virtual-device research.
- [docs/TESTING.md](docs/TESTING.md) defines automated and manual verification.
- [CONTRIBUTING.md](CONTRIBUTING.md) explains the change discipline.

The analysis code must remain independent from WinForms and NAudio so that it can be tested with deterministic sample buffers. Behavioral changes require tests, and user-visible or architectural changes require documentation updates.

## Prior art used for the initial design

- [StereoDirectionVisualizer](https://github.com/SamiKamara/StereoDirectionVisualizer) provided the original L/R RMS, smoothing, stereo balance, front/back candidate, and direction-history concepts (reviewed at commit `a459bf3257a2a1bac72c66bb8535e4d7d5785093`).
- [Aimoro](https://github.com/SamiKamara/Aimoro) provided the proven WinForms overlay, tray-app, global-hotkey, Steam-window targeting, display-selection, and persisted-settings patterns (reviewed at commit `0f1164374f12f053cad6a02a9af8460d0efbe93d`).

This repository evolves those ideas into a separate sound-direction overlay with a testable core and an explicit path toward multichannel input.
