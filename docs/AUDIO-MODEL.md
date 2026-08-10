# Stereo audio model

## Scope

Version 1 analyzes exactly two interleaved channels. Supported sample encodings are 32-bit IEEE float and 16/24/32-bit PCM. The application rejects a non-stereo capture source with a clear error instead of silently reading the first two channels and presenting an incomplete result.

This is an amplitude-balance visualizer, not source separation, acoustic localization from microphones, or semantic sound recognition.

## Capture source selection

The default source is the selected Windows render endpoint. With `Default Windows output device` selected, this is the current Windows multimedia output. Endpoint loopback captures the final mix sent to that device, which is more reliable for games that divide menu and in-game audio between multiple processes or audio sessions.

If the default endpoint remains below the effective silence gate for eight seconds, the application occasionally reads the lightweight peak meters of other active render endpoints. It can temporarily move the one active loopback capture to the strongest endpoint that reports stereo and meaningful activity. Empty scans back off from five seconds to a maximum 30-second interval, avoiding parallel capture and continuous idle polling. Explicitly selected non-default endpoints are never changed automatically.

Direct detected-game process capture is an optional setting. If a game uses separate launcher, anti-cheat, and audio processes, that mode prefers an active audio-session process from the same verified Steam game installation. Windows process-loopback capture is independent of the game's current physical output endpoint and is available on Windows 10 version 2004 (build 19041) and newer. It can preserve stereo information when a physical spatial-audio or Bluetooth endpoint exposes only dual mono, but it can also miss audio routed through a different game process or session. If direct activation fails, endpoint loopback is used automatically. The tray audio status identifies the source actually in use.

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

With automatic calibration enabled, the estimator starts with an effective maximum balance of `0.08` and keeps the latest 256 active absolute balance values. Every eight active frames it estimates the capture source's usual stereo width from the 90th percentile and gradually moves the effective maximum toward a model that places that learned width at a `75°` lateral reference. The equivalent scale margin is `1 / sin(75°)`, about 3.53%. A wider new observation raises the effective maximum immediately, before that same frame is estimated, while the percentile model releases it gradually after the transient. The automatic range is limited to the theoretical balance interval `0.03..1.00`, independently of the disabled manual hard-pan value.

Using an angular reference makes the normalization less sensitive to endpoint processing than a large fixed percentage margin. For example, a compressed endpoint whose relative side balance is `0.80` maps to `75°`, while a wide endpoint at `0.98` maps to about `78.5°` because its model reaches the theoretical ceiling. The same pair differed by about 12 degrees with the previous 8.9% margin. This is still content-relative calibration rather than measured acoustic localization: unrelated unusually wide sounds can affect the learned scale, and stereo amplitude alone cannot guarantee identical angles across speaker crossfeed, headset virtualization, or game-specific mixes. Only a true balance magnitude of `1.00`—energy in one channel and none in the other—must represent an exact hard side. Calibration state is reset whenever capture starts or its source changes, so each active endpoint learns independently.

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
