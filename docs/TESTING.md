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
- explicit rejection of non-stereo input in version 1;
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
- independent rendering and complete hiding of every overlay element layer.

Add tests in the same change whenever direction math, sample decoding, thresholds, candidate behavior, or history behavior changes. Every newly supported sample format or channel layout needs a deterministic byte-level fixture.

## Build verification

```powershell
dotnet restore .\SoundDirectionVisualizer.sln
dotnet build .\SoundDirectionVisualizer.sln --configuration Release --no-restore
dotnet test .\SoundDirectionVisualizer.sln --configuration Release --no-build
```

GitHub Actions runs these commands on `windows-latest` for pushes and pull requests.

## Manual Windows smoke test

Before a user-facing release:

1. Start the app with a Steam game running and the optional process-capture setting disabled; confirm the tray audio status names the selected/default Windows output device and both menu and in-game sounds create frames.
2. Leave the Windows default output silent and play audio through another active stereo output. After the silence grace period, confirm the tray status changes to `Audio: Auto endpoint fallback: <device>` without running parallel captures.
3. With all outputs silent, confirm endpoint probes back off to at most once every 30 seconds and the app remains idle. Confirm surround-only candidates are not silently treated as stereo.
4. Leave manual process capture disabled and `Automatically try game-process audio when a running game's output stays centered` enabled. With a Steam game running, feed at least eight seconds of continuous audible dual-mono output and confirm the tray changes to `Audio: Auto game fallback: Game: <process>`. Confirm one lateral frame before eight seconds cancels the attempt, and a quiet gap longer than two seconds restarts the interval.
5. Disable the automatic centered-output fallback and repeat the dual-mono case; confirm endpoint capture remains selected. Re-enable it, trigger automatic process capture, then exit or change games and confirm the next game starts again from endpoint capture.
6. Enable `Capture only the detected Steam game's process audio`. For a game with separate launcher/anti-cheat/game processes, confirm the status reads `Audio: Game: <process>` and names the active audio process rather than an idle launcher; disable the option and confirm endpoint capture resumes.
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
25. Select a non-stereo endpoint with game-process capture disabled and confirm a clear error appears without a crash; confirm automatic endpoint discovery never down-selects a multichannel endpoint.
26. Launch a second app instance and confirm it reopens the existing settings window.
27. Exit from the tray and confirm capture, overlay, hotkeys, and notification icon stop.

Record the Windows version, scaling, display layout, endpoint name, endpoint format, and game display mode for failures.

## Live capture diagnostics

With the target game running, use the production-service probe to verify source selection and measured balance:

```powershell
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- --resolve-game-audio
$game = Get-Process -Name DayZ_x64
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- $game.Id 15
```

For DayZ, the resolver should report `DayZ_x64` as both the detected window and selected audio process even though managed full-module access is restricted. The capture source should then be `Game: DayZ_x64`, with a matching process ID. A directional sample should produce non-zero signed and absolute balance values. The exact hard-side counter should stay at zero for ordinary wider transients and increase only for a genuinely single-channel pan. Combined-level percentiles and the classified-loud frame count help tune the relative threshold. The tool retains only aggregate levels, classifications, and candidate counts in memory and does not write or forward captured audio.
