# Sound Direction Visualizer

<p align="center">
  <img src="src/SoundDirectionVisualizer.App/Assets/SoundDirectionVisualizerIcon.png" alt="Sound Direction Visualizer icon" width="96" height="96">
</p>

Sound Direction Visualizer is a Windows accessibility overlay that estimates a sound's left/right direction from the audio currently being played and draws the result over a game. It runs as a notification-area application and does not take mouse or keyboard focus from the game.

The application automatically uses verified 7.1 or 5.1 game-process audio when Windows and the game expose useful independent side or rear channels. It otherwise keeps the stereo path, where left/right balance normally cannot distinguish whether a sound is in front of or behind the listener, and shows both mathematically valid directions instead of inventing precision.

## Download

Download the ready-to-run application from the [latest GitHub Release](https://github.com/SamiKamara/SoundDirectionVisualizer/releases/latest), or use the [direct Windows x64 download](https://github.com/SamiKamara/SoundDirectionVisualizer/releases/latest/download/SoundDirectionVisualizer-win-x64.exe).

The release is a self-contained, single-file executable. It does not require a separate .NET installation. Download `SHA256SUMS.txt` from the same release to verify the executable before running it; the release also includes the application license and bundled third-party notices. The executable is not currently code-signed, so Windows SmartScreen may show an unknown-publisher warning.

## Current features

- Automatic best-available WASAPI capture: verified 7.1/5.1 detected-game process audio with selected/default-output stereo fallback, low-frequency active-endpoint fallback, and automatic process fallback for sustained centered Steam-game audio
- Layout-aware multichannel and RMS-based stereo direction estimates with automatic output-level and stereo-width calibration
- Adaptive loud-sound classification relative to recent ambience, with separately styled current and delayed markers
- Optional manual smoothing, silence-threshold, and hard-pan calibration controls
- Click-through, always-on-top compass overlay with current rays and a fading history trail
- Responsive dark-mode settings window and matching notification-area menu, styled around the application's cyan direction-ring identity
- Live-previewed overlay appearance plus independent ambient/loud marker size, fill color, percentage opacity, labels, position, and history duration
- Independent visibility toggles for the compass ring, cardinal ticks, current rays, current markers, listener dot, history trail, and F/B/L/R labels
- Automatic display targeting for a detected Steam game window
- Manual display selection and a display-cycling hotkey
- Global hotkeys for toggling the overlay and opening settings
- A dedicated three-color application, tray, shortcut, and README icon based on the overlay's paired direction markers
- Persistent settings in `%AppData%\SoundDirectionVisualizer\settings.json`
- A UI-independent analysis library with deterministic stereo, 5.1, 7.1, validation, and fallback tests

## Requirements

- Windows 10 or newer
- 64-bit Windows for the published executable
- .NET 9 SDK only when running from source
- A stereo Windows output endpoint for the unconditional fallback path

Automatic and manual direct game-process capture require Windows 10 version 2004 (build 19041) or newer. On older Windows versions, or if direct activation fails, the application automatically uses the selected stereo output endpoint instead. Verified multichannel process capture does not require surround speakers, a virtual device, or a driver installed by this application.

## Best-available audio path

When a Steam game is detected and `Automatically use verified multichannel game audio when available` is enabled, the application keeps normal endpoint stereo active while it asks Windows process loopback for 48 kHz float 7.1, then 5.1 if 7.1 activation fails. It accepts only recognized `WAVEFORMATEXTENSIBLE` channel masks and analyzes every channel; it never treats the first two channels of a multichannel stream as the whole result. If activation succeeds but the validation window contains no useful independent side/rear data, or activation is temporarily unavailable, the working stereo capture remains uninterrupted and the process probe is retried after 30 seconds with a 1, 2, 4, and at-most-5-minute backoff. Verification or a new audio session resets that schedule.

Successful format negotiation is only a probe result. Promotion requires material side/rear energy in at least three frames whose content cannot be reconstructed as a linear mixture of the front channels. Rejection requires at least 32 active frames spanning eight seconds, while a 12-second wall-clock cap also bounds a silent or too-sparse probe. Silence, copied channels, and stereo-derived upmix therefore do not enable the richer estimator. A verified probe becomes the active source and the endpoint capture stops; an unavailable or uninformative probe stops and leaves endpoint stereo unchanged. Manual process capture uses an energy-preserving stereo fold-down with explicit front/back ambiguity until the same validation succeeds.

The Audio tab also has a disabled-by-default `Debug: force multichannel source when available` option. It makes an available recognized 7.1/5.1 game-process stream the active source immediately instead of waiting for content verification. Validation still controls the direction estimator: verified independent side/rear content enables multichannel direction, while copied, silent, or otherwise uninformative extra channels keep the forced source but use the all-channel stereo fold-down with explicit front/back ambiguity. If no supported multichannel stream is available, endpoint stereo remains active and the normal retry schedule continues. This debug option never treats channel count alone as proof of directional precision.

The tray audio row, the settings window's `Status` tab, and the diagnostic probe expose the active source, capture method, requested and observed layout, estimator mode, validation state, fallback reason, and next automatic retry. While debug force is enabled, Status additionally shows a live −60…0 dBFS meter for every channel currently monitored: all recognized 5.1/7.1 channels for an active process stream, or left/right when endpoint fallback is active. LFE remains visible in this diagnostic meter even though it is excluded from direction estimation. The meter transfers only aggregate RMS levels to the UI, becomes a waiting state after one second without fresh data, and is hidden outside debug-force mode. The Status tab also keeps the newest 100 capture decisions and errors for the current application session, with a plain-language reason for each decision. This event log is held only in memory and never contains audio. The multichannel estimator maps standard horizontal speaker positions to nominal azimuths (`front left/right = +/-30 degrees`, `side = +/-90 degrees`, `back left/right = +/-150 degrees`), combines simultaneous energy as a vector, preserves multiple candidates when opposing energy cannot support one direction, and never treats LFE as a directional speaker. Native multichannel endpoints and an optional Windows spatial-sound recommendation remain later work; see [docs/ROADMAP.md](docs/ROADMAP.md).

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

Selected-output capture and automatic audio calibration are enabled by default. With the `Default Windows output device` selection, the app follows the current Windows multimedia output. If that source remains silent for eight seconds, the app checks endpoint peak meters in the background and can temporarily follow the strongest other active stereo output; unsuccessful idle checks back off to at most once every 30 seconds. One source is normally captured at a time; the bounded best-available validation temporarily runs a process probe beside endpoint stereo so an unverified format cannot interrupt the baseline result.

Best-available multichannel probing is enabled by default and is independent of manual process capture. Debug force is disabled by default. Direct detected-game process capture remains available as a manual Audio-tab setting for devices whose physical output loopback is dual mono after spatial-audio processing. A separate automatic stereo-process fallback is also enabled by default: while any Steam game is detected, eight seconds of audible front/back-only endpoint output with no lateral frame makes the app try that game's process audio for the rest of the game session. Before that fallback triggers, a lateral frame or a quiet gap longer than two seconds restarts observation; changing or exiting the game resets the session. The heuristic is identical for every detected Steam game and does not use a compatibility list.

Calibration scales the silence gate down for quiet sources and learns the active endpoint's usual stereo width. It normalizes that width toward a fixed lateral reference angle and immediately gives a wider transient such as a gunshot enough room not to clip to an exact side because of stale ambience calibration. This reduces direction changes between wide speaker output and headset output narrowed by crossfeed or spatial processing, but stereo amplitude alone cannot guarantee identical physical angles through every device pipeline. Calibration restarts whenever the capture source changes.

The overlay is enabled by default with a white color, 40% opacity, a size of 110% of the target display height, 3 px line thickness, an 8 px base direction-marker scale, zero horizontal and vertical offsets, and a five-second trail. Ambient current and delayed markers default to 60% of the base marker size and 40% relative marker opacity. Loud-sound emphasis is enabled with a `2.5 ×` recent-ambience threshold; loud current and delayed markers default to 160% size, 100% relative marker opacity, and a 0.8 px black outline. Both marker types have independent size, fill-color, and percentage-opacity controls; existing settings initially inherit the overlay color for both fills. Loud markers are always rendered in a top marker layer so ambient current or trail points cannot obscure them. The loud outline can be enabled separately and its color and thickness are adjustable in 0.1 px steps. Only the current direction markers and fading direction trail are visible by default; every layer can be enabled independently on the Overlay tab.

## How to read the overlay

- `F`, `B`, `L`, and `R` mean front, back, left, and right relative to the player.
- Bright rays and dots are the current direction candidates.
- Fading dots are recent candidates.
- Larger outlined dots identify frames classified as loud relative to the recent ambience; this is a level distinction, not a sound-type recognizer.
- Two candidates are normal for stereo because the same left/right balance fits a front and a back direction. A verified multichannel frame normally produces one candidate; opposing simultaneous channel energy can still produce several honest candidates.
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

Game detection is also used for automatic best-available probing, manual process capture, and automatic centered-output fallback. Games may split launcher, anti-cheat, rendering, and audio work across several processes, so those modes check active Windows audio sessions and prefer the audio-producing process from the same Steam game installation. Process audio can continue to follow a detected Steam game while display targeting is manual. The tray menu's disabled `Audio:` row identifies the source and adds plain-language stereo/multichannel validation details.

The overlay works best with borderless-windowed games. Exclusive fullscreen, protected presentation paths, and some anti-cheat environments may prevent third-party topmost windows from appearing.

## Build, test, and publish

```powershell
dotnet restore .\SoundDirectionVisualizer.sln
dotnet format .\SoundDirectionVisualizer.sln --verify-no-changes --no-restore
dotnet build .\SoundDirectionVisualizer.sln --configuration Release --no-restore
dotnet test .\SoundDirectionVisualizer.sln --configuration Release --no-build
.\scripts\publish-win-x64.ps1
```

Publishing creates a self-contained single-file executable under `artifacts\publish\win-x64`.

The tracked PNG and multi-resolution Windows icon share one deterministic source. Regenerate both after changing the icon geometry or palette:

```powershell
.\scripts\generate-icon-assets.ps1
```

To build the same named executable and checksum used by GitHub Releases:

```powershell
.\scripts\build-release.ps1 -Version 1.0.2
```

Maintainers should follow [docs/RELEASING.md](docs/RELEASING.md). The tagged release workflow validates the semantic version, formatting, build, tests, executable, and SHA-256 checksum before creating or updating a GitHub Release.

For a live verification of the same production capture service used by the overlay:

```powershell
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- --resolve-game-audio
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- <game-process-id> 15
```

The first command reports the detected Steam window process and the active audio process selected from the same game installation. The second captures that process and reports requested/observed layouts, estimator and validation modes, fallback reason, active source, observed L/R aggregate balance, and exact hard-side frame counts without writing captured audio to disk.

## Privacy and security

Sound Direction Visualizer analyzes loopback audio locally in memory. It does not record audio to disk, transmit audio, include telemetry, or require an account. Settings remain in `%AppData%\SoundDirectionVisualizer\settings.json`. The optional diagnostic probe reports aggregate levels and candidate counts only.

Report security vulnerabilities privately according to [SECURITY.md](SECURITY.md). Do not attach captured audio, personal filesystem paths, or sensitive game logs to public issues.

## Architecture and project policy

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes components and runtime flow.
- [docs/AUDIO-MODEL.md](docs/AUDIO-MODEL.md) defines the stereo and verified-multichannel math and their limitations.
- [docs/ROADMAP.md](docs/ROADMAP.md) records completed best-available process capture, broader endpoint support, and optional virtual-device research.
- [docs/TESTING.md](docs/TESTING.md) defines automated and manual verification.
- [docs/RELEASING.md](docs/RELEASING.md) defines the tagged GitHub Release process.
- [CONTRIBUTING.md](CONTRIBUTING.md) explains the change discipline.

The analysis code must remain independent from WinForms and NAudio so that it can be tested with deterministic sample buffers. Behavioral changes require tests, and user-visible or architectural changes require documentation updates.

## License

Sound Direction Visualizer is available under the [MIT License](LICENSE).
