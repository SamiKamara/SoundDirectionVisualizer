# Sound Direction Visualizer

Sound Direction Visualizer is a Windows accessibility overlay that estimates a sound's left/right direction from the audio currently being played and draws the result over a game. It runs as a notification-area application and does not take mouse or keyboard focus from the game.

The first version deliberately supports **stereo output only**. Stereo provides left/right balance but normally cannot distinguish whether the sound is in front of or behind the listener, so the overlay shows both mathematically valid directions. This limitation is visible instead of being hidden behind a false sense of precision.

## Current features

- Automatic process-loopback capture from the detected Steam game, with selected-output WASAPI loopback as a fallback
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

Direct game-process capture requires Windows 10 version 2004 (build 19041) or newer. On older Windows versions, or if direct activation fails, the application automatically uses the selected stereo output endpoint instead.

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

Direct detected-game audio capture and automatic audio calibration are enabled by default. When a Steam game is detected, its process audio is analyzed before the physical headset endpoint; this preserves L/R information on devices whose output loopback is dual mono after spatial-audio processing. If no game is detected or direct capture is unavailable, the selected Windows output is used automatically. Calibration scales the silence gate down for quiet sources, learns the usual stereo width, and immediately adds headroom when a wider transient such as a gunshot arrives so it is not clipped to an exact side by stale ambience calibration. Calibration restarts when the capture source changes. Both behaviors can be changed on the Audio tab.

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
2. resolves its process and executable path with a limited-information Windows query that remains available for many anti-cheat-protected game processes;
3. verifies that the executable is under a discovered Steam library's `steamapps\common` directory;
4. targets the display containing that window;
5. retains a recently detected game while its window remains visible and not minimized;
6. periodically scans running processes as a fallback.

If no game is detected, the current/manual display remains selected. Turning automatic targeting off makes the chosen display explicit and persistent.

Game detection is also used by the default audio-capture mode. Games may split launcher, anti-cheat, rendering, and audio work across several processes, so the application checks active Windows audio sessions and prefers the audio-producing process from the same Steam game installation. Audio can continue to follow a detected Steam game while display targeting is manual. The tray menu's disabled `Audio:` row shows whether the active source is `Game: <process>` or a Windows output device.

The overlay works best with borderless-windowed games. Exclusive fullscreen, protected presentation paths, and some anti-cheat environments may prevent third-party topmost windows from appearing.

## Build, test, and publish

```powershell
dotnet build .\SoundDirectionVisualizer.sln --configuration Release
dotnet test .\SoundDirectionVisualizer.sln --configuration Release
.\scripts\publish-win-x64.ps1
```

Publishing creates a self-contained single-file executable under `artifacts\publish\win-x64`.

For a live verification of the same production capture service used by the overlay:

```powershell
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- --resolve-game-audio
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- <game-process-id> 15
```

The first command reports the detected Steam window process and the active audio process selected from the same game installation. The second captures that process and reports the active source, observed L/R balance, and exact hard-side frame counts for all active audio and its loudest decile without writing captured audio to disk.

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
