# Sound Direction Visualizer

Sound Direction Visualizer is a Windows accessibility overlay that estimates a sound's left/right direction from the audio currently being played and draws the result over a game. It runs as a notification-area application and does not take mouse or keyboard focus from the game.

The first version deliberately supports **stereo output only**. Stereo provides left/right balance but normally cannot distinguish whether the sound is in front of or behind the listener, so the overlay shows both mathematically valid directions. This limitation is visible instead of being hidden behind a false sense of precision.

## Current features

- Selected/default-output WASAPI loopback capture, optional detected-game process capture, low-frequency active-endpoint fallback when the Windows default stays silent, and automatic process fallback for sustained centered Steam-game audio
- RMS-based stereo direction estimate with automatic output-level and stereo-width calibration
- Adaptive loud-sound classification relative to recent ambience, with separately styled current and delayed markers
- Optional manual smoothing, silence-threshold, and hard-pan calibration controls
- Click-through, always-on-top compass overlay with current rays and a fading history trail
- Responsive dark-mode settings window and matching notification-area menu, styled around the application's cyan compass identity
- Live-previewed overlay appearance plus independent ambient/loud marker size, fill color, percentage opacity, labels, position, and history duration
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

Optional direct game-process capture requires Windows 10 version 2004 (build 19041) or newer. On older Windows versions, or if direct activation fails, the application automatically uses the selected stereo output endpoint instead.

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

Selected-output capture and automatic audio calibration are enabled by default. With the `Default Windows output device` selection, the app follows the current Windows multimedia output. If that source remains silent for eight seconds, the app checks endpoint peak meters in the background and can temporarily follow the strongest other active stereo output; unsuccessful idle checks back off to at most once every 30 seconds. Only one source is captured at a time.

Direct detected-game process capture remains available as a manual Audio-tab setting for devices whose physical output loopback is dual mono after spatial-audio processing. A separate automatic fallback is enabled by default: while any Steam game is detected, eight seconds of audible front/back-only output with no lateral frame makes the app try that game's process audio for the rest of the game session. Before the fallback triggers, a lateral frame or a quiet gap longer than two seconds restarts observation; changing or exiting the game resets the session. The heuristic is identical for every detected Steam game and does not use a compatibility list. Disable `Automatically try game-process audio when a running game's output stays centered` to keep endpoint capture even when the output appears dual mono.

Calibration scales the silence gate down for quiet sources and learns the active endpoint's usual stereo width. It normalizes that width toward a fixed lateral reference angle and immediately gives a wider transient such as a gunshot enough room not to clip to an exact side because of stale ambience calibration. This reduces direction changes between wide speaker output and headset output narrowed by crossfeed or spatial processing, but stereo amplitude alone cannot guarantee identical physical angles through every device pipeline. Calibration restarts whenever the capture source changes.

The overlay is enabled by default with a white color, 40% opacity, a size of 110% of the target display height, 3 px line thickness, an 8 px base direction-marker scale, zero horizontal and vertical offsets, and a five-second trail. Ambient current and delayed markers default to 60% of the base marker size and 40% relative marker opacity. Loud-sound emphasis is enabled with a `2.5 ×` recent-ambience threshold; loud current and delayed markers default to 160% size, 100% relative marker opacity, and a 0.8 px black outline. Both marker types have independent size, fill-color, and percentage-opacity controls; existing settings initially inherit the overlay color for both fills. Loud markers are always rendered in a top marker layer so ambient current or trail points cannot obscure them. The loud outline can be enabled separately and its color and thickness are adjustable in 0.1 px steps. Only the current direction markers and fading direction trail are visible by default; every layer can be enabled independently on the Overlay tab.

## How to read the overlay

- `F`, `B`, `L`, and `R` mean front, back, left, and right relative to the player.
- Bright rays and dots are the current direction candidates.
- Fading dots are recent candidates.
- Larger outlined dots identify frames classified as loud relative to the recent ambience; this is a level distinction, not a sound-type recognizer.
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

Game detection is also used when manual process capture or automatic centered-output fallback is enabled. Games may split launcher, anti-cheat, rendering, and audio work across several processes, so those modes check active Windows audio sessions and prefer the audio-producing process from the same Steam game installation. Process audio can continue to follow a detected Steam game while display targeting is manual. The tray menu's disabled `Audio:` row distinguishes a normal endpoint, `Game: <process>`, `Auto game fallback: Game: <process>`, and `Auto endpoint fallback: <device>`.

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
