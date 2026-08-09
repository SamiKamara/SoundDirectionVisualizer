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
- calibration reset between output devices;
- float32 and PCM16/24/32 RMS decoding;
- explicit rejection of non-stereo input in version 1;
- history expiry;
- display-relative whole-overlay size calculations;
- exact selected-color rendering without chroma-key contamination;
- application of opacity to the overlay window;
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

1. Start the app with the default stereo endpoint.
2. Play silence and confirm no current direction rays appear.
3. With automatic calibration enabled, play a known center, left, right, and sweeping stereo test at both low and normal endpoint volumes.
4. Confirm quiet-device audio remains visible, center shows front/back, side pans spread away from that axis after several active frames, and history expires.
5. Confirm the game retains keyboard and mouse focus while the overlay is visible.
6. Confirm color, percentage opacity, display-height size, dimensions, offsets, labels, and trail settings preview immediately while editing.
7. Press Cancel and confirm the previously saved appearance is restored; reopen settings, change values, press Save, and confirm they persist.
8. Toggle the ring, cardinal ticks, current rays, current markers, listener dot, trail, and labels independently; confirm only the selected layers remain.
9. Confirm overlay toggle and settings hotkeys work globally.
10. Confirm manual display selection and display cycling on a multi-monitor system.
11. With Steam available, move a borderless game between displays and confirm auto targeting follows it.
12. Disconnect/reconnect a display and confirm the app falls back without exiting.
13. Disable automatic calibration and confirm the manual silence-threshold and hard-pan controls become available and retain the legacy fixed behavior.
14. Select a non-stereo endpoint and confirm a clear error appears without a crash.
15. Launch a second app instance and confirm it reopens the existing settings window.
16. Exit from the tray and confirm capture, overlay, hotkeys, and notification icon stop.

Record the Windows version, scaling, display layout, endpoint name, endpoint format, and game display mode for failures.
