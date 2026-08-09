# Stereo audio model

## Scope

Version 1 analyzes exactly two interleaved channels. Supported sample encodings are 32-bit IEEE float and 16/24/32-bit PCM. The application rejects a non-stereo capture source with a clear error instead of silently reading the first two channels and presenting an incomplete result.

This is an amplitude-balance visualizer, not source separation, acoustic localization from microphones, or semantic sound recognition.

## Capture source selection

The default source is the audio rendered by the detected Steam game process and its child processes. If a game uses separate launcher, anti-cheat, and audio processes, the application prefers an active audio-session process from the same verified Steam game installation. Windows process-loopback capture is independent of the game's current physical output endpoint and is available on Windows 10 version 2004 (build 19041) and newer. It also excludes unrelated audio such as voice chat, music, and browser playback from the estimate.

If no game is detected, direct activation fails, the Windows version is too old, or the user disables the preference, the selected/default render endpoint is captured through ordinary WASAPI loopback. The fallback preserves the original StereoDirectionVisualizer behavior.

This ordering matters for spatial-audio and Bluetooth endpoints. A physical endpoint's loopback can contain two identical channels even though a later driver, spatial-object renderer, or hardware stage produces directional sound for the listener. No sample-level algorithm can recover direction from two identical channels. Capturing the game process earlier in the Windows render path preserves the stereo information when the game exposes it there. The tray audio status identifies the source actually in use.

## Level calculation

For each captured block, the root mean square level is calculated independently for left and right:

```text
L_rms = sqrt(sum(L_sample²) / frame_count)
R_rms = sqrt(sum(R_sample²) / frame_count)
```

An exponential moving average reduces flicker:

```text
smoothed = previous + smoothing_factor × (new - previous)
```

The default smoothing factor is `0.20`. Larger values respond faster; smaller values move more slowly.

No direction candidate is generated when `L_rms + R_rms` is below the effective silence threshold. Automatic calibration is enabled by default because loopback amplitude can vary substantially with the endpoint and its volume path. It tracks a slowly releasing recent peak and sets the effective gate to 0.5% of that peak, with a floor of `0.00001` and the configured manual threshold as a ceiling. Manual mode uses the configured threshold directly; its default is `0.00125`.

## Balance and azimuth

The normalized stereo balance is:

```text
balance = (R_rms - L_rms) / (R_rms + L_rms)
```

The original prototype assumed a model in which an apparent hard side used a 20/80 energy split, producing a balance magnitude of `0.60`. The manual `modelMaximumBalance` defaults to `0.50` and is used only when automatic calibration is disabled.

With automatic calibration enabled, the estimator starts with an effective maximum balance of `0.08` and keeps the latest 256 active absolute balance values. Every eight active frames it estimates the capture source's usual stereo width from the 90th percentile, adds 25% headroom, and gradually moves the effective maximum toward that value. A wider new observation raises the effective maximum immediately, before that same frame is estimated, while the percentile model releases it gradually after the transient. The automatic range is limited to the theoretical balance interval `0.03..1.00`, independently of the disabled manual hard-pan value. This gives narrow ambience useful lateral movement without allowing its learned width to clip a wider gunshot directly to ±90 degrees. Only a true balance magnitude of `1.00`—energy in one channel and none in the other—must still represent an exact hard side. Calibration state is reset whenever capture starts or its source changes.

```text
s = clamp(balance / modelMaximumBalance, -1, +1)
base_angle = asin(abs(s))
```

Azimuth uses `0° = front`, `90° = right`, `180° = back`, and `270° = left`.

| Condition | Candidate azimuths |
|---|---|
| Equal L/R | `0°` and `180°` |
| Right-biased | `base_angle` and `180° - base_angle` |
| Left-biased | `360° - base_angle` and `180° + base_angle` |
| Modelled hard side | candidates meet at `90°` or `270°` |

## Why front and back are both shown

A plain stereo amplitude pair contains no general, reliable front/back label. The same L/R balance can be produced by a source mirrored across the listener's left-right axis. Game-specific binaural processing may encode additional spectral and timing cues, but this first estimator intentionally does not claim to decode them.

The two rays are therefore a feature: they communicate the information that is present and the ambiguity that remains.

## Loud-sound classification

Loud emphasis is a relative level classifier, not gunshot detection or semantic audio recognition. It uses the smoothed combined RMS level already available to the direction pipeline:

```text
combined_level = L_rms + R_rms
loud_threshold = median(recent_active_levels) × configured_multiplier
```

The classifier keeps the latest 256 active levels and uses their median as the recent ambience baseline. It waits for 32 active samples before classifying anything as loud, so application startup does not guess without context. The current level is classified before it is inserted into the history; a transient therefore cannot raise its own threshold. The default multiplier is `2.5`. A larger multiplier produces fewer emphasized markers, while a smaller value is more sensitive.

A sustained new level eventually occupies most of the rolling window and becomes the new ambience instead of remaining emphasized forever. Silence does not train the baseline. Classifier state resets whenever capture starts or its source changes. Loud classification affects only marker presentation and does not change direction estimation, stereo ambiguity, or trail duration.

## Calibration guidance

- Keep automatic calibration enabled for normal use and after changing devices or game audio modes.
- Allow a few directional sounds for stereo-width calibration to settle.
- Disable automatic calibration before adjusting the two manual calibration values.
- Raise the silence threshold if noise or quiet ambience keeps creating markers.
- Lower it if relevant quiet sounds disappear.
- Raise smoothing for a faster but more nervous display.
- Lower smoothing for a steadier but slower display.
- In manual mode, change hard-pan model balance only when known side sounds consistently stop too near the front/back or collapse too early at the side.

Settings should be tested with a repeatable stereo pan sample before being tuned inside a game mix.

## Known limitations

- Multiple simultaneous sources are combined into one L/R energy balance.
- Relative level classification cannot determine whether a loud frame is a gunshot, UI sound, nearby engine, music peak, or several simultaneous sources.
- Process capture includes the selected same-installation audio process tree. Audio routed through a process outside the verified Steam game directory may still require endpoint fallback.
- Direct game capture requires Windows build 19041 or newer and falls back automatically when unavailable.
- An endpoint-loopback fallback that exposes true dual mono contains no recoverable left/right direction information.
- Automatic calibration can amplify a narrow L/R energy difference, but cannot recover direction when a binaural mix encodes it only in timing or spectral cues and has equal channel energy.
- Music, UI sounds, dialogue, reverberation, and game ambience all contribute.
- Dynamic range compression and per-game mixing affect the estimate.
- Stereo alone does not provide reliable elevation.
- Binaural/headphone mixes may contain useful information this amplitude-only estimator ignores.
- A surround endpoint must not be down-selected to two channels without an explicit, documented strategy.
