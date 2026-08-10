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

Near-term hardening:

- test on a wider set of endpoint formats and Windows scaling combinations;
- add structured diagnostics/export for device format and capture failures;
- add an in-app capture-health indicator with visibility into automatic fallback observations and source changes;
- add an optional calibration view and synthetic pan test;
- profile overlay painting during sustained high-frequency capture;
- investigate non-Steam target rules without weakening predictable screen selection;
- add packaged releases and signed binaries when distribution begins;
- move from the pinned NAudio 3 preview to a stable release after process-loopback behavior is verified unchanged.

## Phase 2: native multichannel output

The next estimator should support hardware/endpoints exposing more than stereo, initially standard Windows layouts such as 5.1 and 7.1.

Planned technical work:

1. Read `WaveFormatExtensible` channel masks instead of relying only on channel count.
2. Decode all channels into a layout-aware frame.
3. Map standard speaker positions to azimuth vectors.
4. Estimate horizontal direction from per-channel energy and preserve uncertainty when layouts or content are incomplete.
5. Decide how LFE and center channels contribute; never treat LFE as a normal directional speaker by default.
6. Expose the detected channel layout and estimator mode in settings/status.
7. Add deterministic fixtures for 5.1 and 7.1 channel impulses, mixtures, silence, and malformed layouts.
8. Keep the current overlay contract so rendering remains independent of channel count.

Elevation should only be added for layouts that actually provide height channels and after the result can be communicated without misleading precision.

## Phase 3 research: virtual multichannel audio endpoint

A later option is to present games with a virtual surround output device even when the physical hardware is stereo. The virtual endpoint could advertise a layout such as 5.1 or 7.1, retain the game's discrete directional channels for visualization, then downmix/render them to the user's real device.

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

The virtual-device idea should only proceed after a prototype demonstrates materially better direction estimates and acceptable latency. It must remain optional; the overlay should continue to work with ordinary hardware endpoints.

## Non-goals unless separately designed

- inferring exact game-world coordinates from mixed output;
- identifying sound categories with cloud processing;
- bypassing anti-cheat, protected overlays, or protected audio paths;
- claiming accurate front/back or elevation from amplitude-only stereo.
