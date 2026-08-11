# Repository instructions

- Read `README.md` and the relevant files under `docs/` before changing behavior.
- Keep `SoundDirectionVisualizer.Core` platform-independent and deterministic.
- Every audio-model, decoder, history, or settings-normalization behavior change requires automated tests.
- Every user-visible feature or limitation change requires matching documentation.
- Preserve explicit stereo front/back ambiguity; do not invent precision not supported by the input.
- Do not silently process only the first two channels of a multichannel endpoint.
- Preserve overlay click-through and no-activation window styles.
- Run Release build and tests before handing off changes.
- Any task that produces a new runnable local build must finish by synchronizing all local launch locations after the final Release build and tests:
  - update the repository's normal Release output and run `scripts\publish-win-x64.ps1` for the standard self-contained publish output;
  - enumerate desktop `.lnk` files that resolve to `SoundDirectionVisualizer.exe`, verify every absolute target path, and update every such target with the same final published build, including targets outside the repository;
  - before stopping anything, record the exact executable path of every running Sound Direction Visualizer instance;
  - stop only the verified instances whose files must be replaced, preserve settings, and relaunch after the update only the executable locations that were running before it; prefer the matching desktop shortcut when one exists;
  - verify the final target timestamps or hashes and the relaunched executable paths before reporting success. If a target remains locked, wait and retry rather than claiming that it was updated.
- Intermediate compiler checks may leave the running app alone, but the final validated build must complete the synchronization and restart procedure above.
- Do not commit generated output under `bin`, `obj`, `artifacts`, or `TestResults`.
