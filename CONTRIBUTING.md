# Contributing

## Change discipline

- Keep reusable analysis in `SoundDirectionVisualizer.Core`; do not add WinForms, NAudio, registry, process, or screen dependencies there.
- Treat `DirectionEstimate.CandidateAzimuths` as an uncertainty-aware result. Do not reduce it to one direction unless the input model supports that conclusion.
- Add or update automated tests with every audio-model, decoder, history, or normalization change.
- Update README or `docs/` with every user-visible setting, limitation, architecture decision, or roadmap change.
- Preserve click-through/no-activation behavior when changing the overlay window.
- Fail clearly on unsupported channel layouts. Silent partial analysis is not acceptable.
- Avoid expensive work on the NAudio callback thread; publish immutable frames and return quickly.

## Before submitting a change

```powershell
dotnet build .\SoundDirectionVisualizer.sln --configuration Release
dotnet test .\SoundDirectionVisualizer.sln --configuration Release --no-build
```

For overlay, targeting, hotkey, settings, or audio-device changes, also complete the relevant items in [docs/TESTING.md](docs/TESTING.md).

## Local desktop build synchronization

When a task produces a new runnable build on a development machine, the final validated build must replace every local copy used to launch the application, not only `bin\Release`:

1. Record the exact executable paths of any running Sound Direction Visualizer instances.
2. Complete the Release build and tests, then create the standard self-contained build with `scripts\publish-win-x64.ps1`.
3. Resolve every desktop shortcut that targets `SoundDirectionVisualizer.exe` and deploy the same final build to each verified target path. This includes shortcut targets outside the repository.
4. Stop only verified instances when their executable must be replaced. Preserve `%AppData%\SoundDirectionVisualizer\settings.json`.
5. Relaunch only the executable locations that were running before the update, using their matching shortcuts when available.
6. Verify target timestamps or hashes and the executable paths of the restarted processes before reporting completion. Retry transient file locks instead of treating a failed publish as successful.

Generated publish output remains local and must not be committed.

## Releases

Release tags, assets, checksums, and recovery procedures are documented in [docs/RELEASING.md](docs/RELEASING.md). Use `scripts\create-release.ps1` rather than assembling or uploading release assets by hand.

## Commit scope

Keep audio math, platform integration, UI, and documentation changes separable when practical. Generated `bin`, `obj`, `artifacts`, and `TestResults` output must not be committed.
