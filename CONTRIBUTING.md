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

## Commit scope

Keep audio math, platform integration, UI, and documentation changes separable when practical. Generated `bin`, `obj`, `artifacts`, and `TestResults` output must not be committed.
