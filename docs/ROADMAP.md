# Roadmap

This document records direction, not a release promise. Behavioral work should arrive with tests and an update to the relevant design documentation.

## Phase 1: stereo overlay foundation

Implemented in the initial project:

- selected/default-endpoint loopback capture with silent-default endpoint discovery, optional detected-game process capture, and content-based automatic process fallback for sustained centered Steam-game output
- deterministic sample decoding and RMS analysis
- adjustable smoothing, silence threshold, and balance model
- explicit front/back ambiguity
- click-through in-game compass with history
- Steam game display detection plus manual display selection
- persistent settings, tray controls, global hotkeys, tests, and Windows CI
- dark-mode settings and tray UI
- reproducible tagged GitHub Releases with a self-contained executable and checksum

Near-term hardening:

- test on a wider set of endpoint formats and Windows scaling combinations;
- add structured diagnostics/export for device format and capture failures;
- add an in-app capture-health indicator with visibility into automatic fallback observations and source changes;
- add an optional calibration view and synthetic pan test;
- profile overlay painting during sustained high-frequency capture;
- investigate non-Steam target rules without weakening predictable screen selection;
- add Authenticode signing when a suitable certificate and protected signing workflow are available;
- move from the pinned NAudio 3 preview to a stable release after process-loopback behavior is verified unchanged.

## Phase 2: automatic best-available process capture

Before requiring surround hardware, a virtual device, or manual routing, the application should opportunistically request standard multichannel formats from Windows process loopback for the detected game's audio process. The goal is the best trustworthy direction estimate that the existing system can provide with no required user setup. Physical stereo headphones or speakers must remain a supported normal configuration.

Planned technical work:

1. Attempt explicit standard process-loopback capture formats, initially 7.1 and 5.1 float PCM, when a game audio process can be resolved.
2. Read and validate `WaveFormatExtensible` channel masks instead of relying only on channel count. Never silently analyze only the first two channels of a multichannel stream.
3. Decode all channels into a platform-independent, layout-aware level frame and map standard horizontal speaker positions to azimuth vectors.
4. Add a multichannel estimator that uses per-channel energy, preserves uncertainty for incomplete layouts or mixed content, and returns the existing direction-result contract.
5. Define explicit center- and LFE-channel behavior; LFE must not be treated as a normal directional speaker by default.
6. Separate successful format negotiation from demonstrated directional value. A stream reporting eight or six channels is not sufficient by itself: status and selection logic must account for whether useful independent side or rear information is actually observed instead of duplicated, upmixed, silent, or stereo-derived content.
7. Keep the current stereo process and endpoint paths as the unconditional automatic fallback when process activation fails, a layout is unknown or malformed, useful multichannel content is not demonstrated, or the game only renders stereo. Fallback must keep the overlay operating and retain explicit stereo front/back ambiguity.
8. Expose the active capture source, requested and observed layout, estimator mode, validation state, and fallback reason without requiring users to understand audio-device terminology.
9. Allow an optional, dismissible recommendation to enable Windows spatial sound on stereo hardware when multichannel process capture is unavailable or uninformative. The application must not require or silently change that Windows setting.
10. Add deterministic fixtures for 5.1 and 7.1 channel impulses, mixtures, silence, duplicated/upmixed stereo, unknown masks, malformed buffers, and every fallback transition.
11. Keep capture and analysis local and aggregate-only under the existing privacy model; capability detection must not write captured audio to disk.

The automatic mode should prefer a verified richer estimate, but it must never reduce baseline compatibility in pursuit of one. A user who installs and launches the application with default Windows audio settings should always receive at least the current stereo behavior.

## Phase 3: broader native multichannel endpoint support

After the process-loopback path establishes the layout-aware analysis model, extend the same decoder and estimator to physical or software output endpoints that already expose more than stereo. This phase should not create a second multichannel model or require users with ordinary stereo hardware to configure an endpoint.

Planned follow-up work:

1. Discover and accept supported standard endpoint layouts without silently down-selecting them to stereo.
2. Reuse the Phase 2 channel-mask parsing, level frame, estimator, uncertainty representation, status, and tests.
3. Cover endpoint format changes, device failover, sample-rate variation, and layouts that differ in side/back channel conventions.
4. Compare process-loopback and endpoint-loopback results when both are available, choosing the path with demonstrated directional value rather than assuming that a larger channel count is better.
5. Preserve the same automatic stereo fallback and avoid requiring manual routing for baseline operation.

Elevation should only be added for layouts that actually provide height channels and after the result can be communicated without misleading precision.

## Phase 4 research: optional virtual multichannel audio endpoint

A later opt-in option is to present games with a virtual surround output device even when the physical hardware is stereo. The virtual endpoint could advertise a layout such as 5.1 or 7.1, retain the game's discrete directional channels for visualization, then downmix/render them to the user's real device. It should be pursued only for material compatibility gaps that remain after automatic process capture and native multichannel endpoints have been evaluated.

Potential benefit:

- the visualizer receives discrete directional information before it is collapsed into stereo;
- games that choose their mix from the Windows speaker layout may produce richer direction data;
- the user could still listen through stereo headphones or speakers.

This is a separate driver/audio-routing project, not a small extension to the overlay. Research must address:

- Windows virtual audio driver architecture, signing, installation, update, and removal;
- low-latency, glitch-free forwarding to the physical endpoint;
- correct downmixing and volume/mute/session behavior;
- sample-rate and format negotiation;
- device failover and recovery;
- compatibility with games, DRM/protected audio, voice chat, and anti-cheat systems;
- privacy and a clear guarantee that audio remains local unless the user explicitly chooses otherwise;
- automated latency, channel-routing, and long-duration stability tests.

The virtual-device idea should only proceed after the zero-setup process-capture work shows which games cannot otherwise expose useful multichannel data and a prototype demonstrates materially better direction estimates with acceptable latency. It must remain optional and must not become a prerequisite for the overlay; declining driver installation or routing changes must leave the automatic stereo path fully functional.

## Non-goals unless separately designed

- inferring exact game-world coordinates from mixed output;
- identifying sound categories with cloud processing;
- bypassing anti-cheat, protected overlays, or protected audio paths;
- claiming accurate front/back or elevation from amplitude-only stereo.
