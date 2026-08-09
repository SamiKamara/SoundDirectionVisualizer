# Repository instructions

- Read `README.md` and the relevant files under `docs/` before changing behavior.
- Keep `SoundDirectionVisualizer.Core` platform-independent and deterministic.
- Every audio-model, decoder, history, or settings-normalization behavior change requires automated tests.
- Every user-visible feature or limitation change requires matching documentation.
- Preserve explicit stereo front/back ambiguity; do not invent precision not supported by the input.
- Do not silently process only the first two channels of a multichannel endpoint.
- Preserve overlay click-through and no-activation window styles.
- Run Release build and tests before handing off changes.
- Do not commit generated output under `bin`, `obj`, `artifacts`, or `TestResults`.
