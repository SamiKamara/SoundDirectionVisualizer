# Testing

## Automated tests

Run the full suite from the repository root:

```powershell
dotnet test .\SoundDirectionVisualizer.sln --configuration Release
```

The automated test suites currently cover:

- silence suppression;
- equal-channel front/back candidates;
- hard-left and hard-right collapse;
- intermediate mirrored candidates;
- automatic low-volume silence-gate scaling;
- narrow- and wide-stereo calibration plus minimum-width noise protection;
- angular-reference stereo-width normalization across compressed and wide endpoints, plus immediate transient headroom and gradual release after narrow ambience;
- preservation of an exact side only for a true single-channel hard pan;
- adaptive loudness warm-up, recent-ambience thresholding, sustained-level adaptation, and reset;
- calibration reset between output devices;
- float32 and PCM16/24/32 RMS decoding;
- explicit stereo-analyzer rejection of non-stereo input;
- every-channel 5.1 float and 7.1 PCM decoding plus energy-preserving stereo fold-down;
- recognized 5.1/7.1 `WaveFormatExtensible` masks, 7.1-before-5.1 request order, unknown masks, missing masks, and malformed partial frames;
- nominal front/side/back multichannel impulses, adjacent-speaker mixtures, opposing-direction uncertainty, and LFE exclusion;
- least-squares rejection of copied front-left/right/center channels and stereo-derived surround upmix, plus independent side/rear detection;
- multichannel validation pending/verified/uninformative/reset transitions, silence handling, minimum material surround energy, and plain-language capture status;
- immutable stereo and 7.1 live-channel meter frames, complete mask-order channel labels including LFE, logarithmic dBFS scaling, debug-only visibility, accessibility text, and rendered level bars;
- history expiry;
- display-relative whole-overlay size calculations;
- exact selected-color rendering without chroma-key contamination;
- application of opacity to the overlay window;
- endpoint-capture default and migration away from the former process-capture default;
- default-enabled automatic game-process fallback setting and sustained-centered-output detection, including lateral, quiet-gap, sparse-frame, latch, and reset behavior;
- silent-endpoint probe grace period, bounded idle backoff, stereo/activity filtering, current-endpoint exclusion, and strongest-endpoint selection;
- limited-information executable-path resolution and invalid-process handling;
- Steam game-install boundary resolution, including sibling-prefix rejection;
- same-game audio-process selection, strongest-session preference, and detected-process fallback;
- independent ambient/loud size, fill-color, relative-opacity, delayed-state, and loud-outline rendering;
- loud-marker top-layer ordering over overlapping ambient current and trail markers;
- marker appearance normalization, legacy overlay-color inheritance, and tenth-pixel loud-outline precision;
- separate Ambient markers and Loud markers settings groups with percentage-opacity sliders;
- dark settings-window palette, custom keyboard-accessible sliders, dark checkbox glyphs, vertically centered hotkey fields, and dark tab navigation;
- independent rendering and complete hiding of every overlay element layer.

Add tests in the same change whenever direction math, sample decoding, thresholds, candidate behavior, or history behavior changes. Every newly supported sample format or channel layout needs a deterministic byte-level fixture.

## Build verification

```powershell
dotnet restore .\SoundDirectionVisualizer.sln
dotnet build .\SoundDirectionVisualizer.sln --configuration Release --no-restore
dotnet test .\SoundDirectionVisualizer.sln --configuration Release --no-build
```

GitHub Actions runs these commands on `windows-latest` for pushes and pull requests.

## Release-package verification

Build the exact local release assets with:

```powershell
.\scripts\build-release.ps1 -Version 1.0.2
```

The script verifies formatting, performs the Release build and full tests, publishes the self-contained Windows x64 executable without debug symbols, packages the application license and version-specific third-party notices, creates `SHA256SUMS.txt`, and verifies every checksum. Generated assets remain under `artifacts\release\v1.0.2` and must not be committed.

The tagged GitHub Actions workflow runs the same script before uploading only the named executable, license, third-party notices, and checksum manifest to a release.

## Manual Windows smoke test

Before a user-facing release:

1. Start the app with a Steam game running, automatic best-available audio enabled, and manual process capture disabled. Confirm endpoint stereo remains visible while the tray reports a 7.1 (or 5.1 after activation fallback) game-process check. With discrete side/rear content, confirm status becomes verified multichannel and known rear sounds no longer create the mirrored stereo candidate. With copied surround channels or stereo-derived upmix, confirm the probe becomes uninformative after at least 32 active frames across eight seconds; with silence or sparse audio, confirm the 12-second wall-clock cap stops it. Endpoint stereo must continue without interruption in every rejection case. Leave the game running and confirm the Status tab shows a retry due after 30 seconds, then 1, 2, 4, and no more than 5 minutes after repeated rejection; provide discrete content during a later validation and confirm it promotes to multichannel and clears the retry. Then enable `Debug: force multichannel source when available`: confirm a recognized process stream becomes the named active source immediately, Status shows the debug-force policy separately from the estimator, and the live channel card shows every current 5.1/7.1 channel—including LFE—or L/R during endpoint fallback. Stop audio and confirm the meter changes to waiting instead of retaining stale levels. Confirm uninformative content keeps the forced source while retaining the stereo estimator and mirrored candidates. Disable debug force and confirm the channel card is hidden and a normal capture session resumes.
2. Leave the Windows default output silent and play audio through another active stereo output. After the silence grace period, confirm the tray status changes to `Audio: Auto endpoint fallback: <device>` without running parallel captures.
3. With all outputs silent, confirm endpoint probes back off to at most once every 30 seconds and the app remains idle. Confirm a native multichannel endpoint is rejected rather than silently treated as stereo, while an automatic process probe validates every channel by its explicit mask.
4. Leave manual process capture disabled and `Automatically try game-process audio when a running game's output stays centered` enabled. With a Steam game running, feed at least eight seconds of continuous audible dual-mono output and confirm the tray changes to `Audio: Auto game fallback: Game: <process>`. Confirm one lateral frame before eight seconds cancels the attempt, and a quiet gap longer than two seconds restarts the interval.
5. Disable the automatic centered-output fallback and repeat the dual-mono case; confirm endpoint capture remains selected. Re-enable it, trigger automatic process capture, then exit or change games and confirm the next game starts again from endpoint capture.
6. Enable `Capture only the detected Steam game's process audio`. For a game with separate launcher/anti-cheat/game processes, confirm the status names the active audio process rather than an idle launcher. During pending/uninformative multichannel validation, confirm the overlay uses a mirrored stereo fold-down from all non-LFE channels; after validation, confirm status and direction switch to verified 5.1/7.1. Disable the option and confirm automatic endpoint-plus-probe behavior resumes.
7. On a headset whose endpoint loopback is dual mono, confirm known left/right game sounds remain ambiguous under endpoint capture but can move laterally when process capture is selected manually or by the automatic fallback and the game exposes usable stereo there.
8. Play silence and confirm no current direction rays appear.
9. With automatic calibration enabled, play a known center, left, right, and sweeping stereo test at both low and normal endpoint volumes.
10. After steady narrow ambience, produce a louder directional transient and confirm it remains a front/back candidate pair instead of clipping to exactly left or right; a true single-channel hard pan may still meet at the side.
11. Confirm quiet-device audio remains visible, center shows front/back, side pans spread away from that axis after several active frames, and history expires.
12. Confirm the game retains keyboard and mouse focus while the overlay is visible.
13. After at least a second of steady ambience, produce a distinctly louder sound and confirm its current and delayed markers use the configured larger size, stronger visual opacity, and outline.
14. Confirm a normal current marker is the same size as a fresh normal trail marker, and that the loud marker returns to normal after the sound level stays steady long enough to become the new ambience.
15. In the separate Ambient markers and Loud markers groups, change both size percentages, percentage-opacity sliders, and fill colors; then change loud outline visibility, color, and thickness in 0.1 px steps. Confirm current and trail markers preview the type-specific styling live, loud markers remain above overlapping ambient markers, and the master emphasis toggle renders loud frames with the ambient style when disabled.
16. Confirm color, percentage opacity, display-height size, dimensions, offsets, labels, and trail settings preview immediately while editing.
17. Press Cancel and confirm the previously saved appearance is restored; reopen settings, change values, press Save, and confirm they persist.
18. Toggle the ring, cardinal ticks, current rays, current markers, listener dot, trail, and labels independently; confirm only the selected layers remain.
19. Confirm overlay toggle and settings hotkeys work globally.
20. Confirm manual display selection and display cycling on a multi-monitor system.
21. With Steam available, move a borderless game between displays and confirm auto targeting follows it.
22. Turn automatic display targeting off while optional game-process capture remains on; confirm the overlay stays on the manual display while audio still follows the game process.
23. Disconnect/reconnect a display and confirm the app falls back without exiting.
24. Disable automatic calibration and confirm the manual silence-threshold and hard-pan controls become available and retain the legacy fixed behavior.
25. Select a non-stereo endpoint with manual game-process capture disabled and confirm a clear error appears without a crash; confirm automatic endpoint discovery never down-selects a multichannel endpoint. This endpoint limitation is independent of process-loopback 5.1/7.1 support.
26. Launch a second app instance and confirm it reopens the existing settings window.
27. Exit from the tray and confirm capture, overlay, hotkeys, and notification icon stop.
28. Confirm the settings window, title bar, controls, tabs, and notification-area menu use the dark visual system at 100%, 125%, and 150% display scaling; verify the non-interactive `LIVE OVERLAY PREVIEW` header badge is absent, every selected checkbox has a clearly visible dark checkmark, each hotkey field keeps its text vertically centered, and sliders remain operable by mouse, wheel, and keyboard. Open the Status tab and confirm source, capture method, estimator, format, layouts, validation, fallback reason, next retry, and the newest-first reasoned session event log update while the dialog remains open.
29. Confirm the compact cyan direction ring and both light direction markers remain recognizable in the settings title bar, notification area, desktop shortcut, executable, and README image.

Record the Windows version, scaling, display layout, endpoint name, endpoint format, and game display mode for failures.

## Live capture diagnostics

With the target game running, use the production-service probe to verify source selection and measured balance:

```powershell
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- --resolve-game-audio
$game = Get-Process -Name DayZ_x64
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- $game.Id 15
```

For DayZ, the resolver should report `DayZ_x64` as both the detected window and selected audio process even though managed full-module access is restricted. The capture source should then be `Game: DayZ_x64`, with a matching process ID. The initial and final diagnostic blocks report requested/observed layout, estimator mode, validation state, and fallback reason. A directional sample should produce non-zero signed and absolute aggregate balance values. With verified multichannel content, rear/side samples should produce the corresponding single nominal candidate; stereo fallback retains mirrored candidates. The exact hard-side counter should stay at zero for ordinary wider transients and increase only for a genuinely single-channel pan. The tool retains only aggregate levels, classifications, status, and candidate counts in memory and does not write or forward captured audio.
