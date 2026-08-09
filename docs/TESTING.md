# Testing

## Automated tests

Run the full suite from the repository root:

```powershell
dotnet test .\SoundDirectionVisualizer.sln --configuration Release
```

The core test suite currently covers:

- silence suppression;
- equal-channel front/back candidates;
- hard-left and hard-right collapse;
- intermediate mirrored candidates;
- automatic low-volume silence-gate scaling;
- narrow- and wide-stereo calibration plus minimum-width noise protection;
- immediate headroom and gradual release for wider transients after narrow ambience;
- preservation of an exact side only for a true single-channel hard pan;
- calibration reset between output devices;
- float32 and PCM16/24/32 RMS decoding;
- explicit rejection of non-stereo input in version 1;
- history expiry;
- display-relative whole-overlay size calculations;
- exact selected-color rendering without chroma-key contamination;
- application of opacity to the overlay window;
- default and legacy-settings migration for preferred detected-game audio capture;
- limited-information executable-path resolution and invalid-process handling;
- Steam game-install boundary resolution, including sibling-prefix rejection;
- same-game audio-process selection, strongest-session preference, and detected-process fallback;
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

1. Start the app with a Steam game running and confirm the tray audio status reads `Audio: Game: <process>`.
2. For a game with separate launcher/anti-cheat/game processes, confirm the status names the process owning the active game audio session rather than an idle launcher.
3. Stop the game and confirm capture falls back to the selected/default stereo endpoint without exiting; relaunch it and confirm direct game capture resumes.
4. Disable `Prefer audio captured directly from the detected Steam game` and confirm the tray status remains on the selected output device; enable it again.
5. On a headset whose endpoint loopback is dual mono, confirm known left/right game sounds move laterally under process capture instead of remaining at front/back.
6. Play silence and confirm no current direction rays appear.
7. With automatic calibration enabled, play a known center, left, right, and sweeping stereo test at both low and normal endpoint volumes.
8. After steady narrow ambience, produce a louder directional transient and confirm it remains a front/back candidate pair instead of clipping to exactly left or right; a true single-channel hard pan may still meet at the side.
9. Confirm quiet-device audio remains visible, center shows front/back, side pans spread away from that axis after several active frames, and history expires.
10. Confirm the game retains keyboard and mouse focus while the overlay is visible.
11. Confirm color, percentage opacity, display-height size, dimensions, offsets, labels, and trail settings preview immediately while editing.
12. Press Cancel and confirm the previously saved appearance is restored; reopen settings, change values, press Save, and confirm they persist.
13. Toggle the ring, cardinal ticks, current rays, current markers, listener dot, trail, and labels independently; confirm only the selected layers remain.
14. Confirm overlay toggle and settings hotkeys work globally.
15. Confirm manual display selection and display cycling on a multi-monitor system.
16. With Steam available, move a borderless game between displays and confirm auto targeting follows it.
17. Turn automatic display targeting off while preferred game audio remains on; confirm the overlay stays on the manual display while audio still follows the game process.
18. Disconnect/reconnect a display and confirm the app falls back without exiting.
19. Disable automatic calibration and confirm the manual silence-threshold and hard-pan controls become available and retain the legacy fixed behavior.
20. Select a non-stereo endpoint, disable direct game capture, and confirm a clear error appears without a crash.
21. Launch a second app instance and confirm it reopens the existing settings window.
22. Exit from the tray and confirm capture, overlay, hotkeys, and notification icon stop.

Record the Windows version, scaling, display layout, endpoint name, endpoint format, and game display mode for failures.

## Live capture diagnostics

With the target game running, use the production-service probe to verify source selection and measured balance:

```powershell
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- --resolve-game-audio
$game = Get-Process -Name DayZ_x64
dotnet run --project .\tools\SoundDirectionVisualizer.ProcessAudioProbe\SoundDirectionVisualizer.ProcessAudioProbe.csproj --configuration Release -- $game.Id 15
```

For DayZ, the resolver should report `DayZ_x64` as both the detected window and selected audio process even though managed full-module access is restricted. The capture source should then be `Game: DayZ_x64`, with a matching process ID. A directional sample should produce non-zero signed and absolute balance values. The exact hard-side counter should stay at zero for ordinary wider transients and increase only for a genuinely single-channel pan. The tool retains only aggregate levels and candidate counts in memory and does not write or forward captured audio.
