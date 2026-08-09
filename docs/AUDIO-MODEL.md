# Stereo audio model

## Scope

Version 1 analyzes exactly two interleaved output channels captured through WASAPI loopback. Supported sample encodings are 32-bit IEEE float and 16/24/32-bit PCM. The application rejects a non-stereo endpoint with a clear error instead of silently reading the first two channels and presenting an incomplete result.

This is an amplitude-balance visualizer, not source separation, acoustic localization from microphones, or semantic sound recognition.

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

No direction candidate is generated when `L_rms + R_rms` is below the configured silence threshold. The default threshold is `0.00125`.

## Balance and azimuth

The normalized stereo balance is:

```text
balance = (R_rms - L_rms) / (R_rms + L_rms)
```

The original prototype assumed a model in which an apparent hard side used a 20/80 energy split, producing a balance magnitude of `0.60`. The adjustable `modelMaximumBalance` now defaults to `0.50`, making the visualization reach the modelled side position at a slightly less extreme L/R difference.

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

## Calibration guidance

- Raise the silence threshold if noise or quiet ambience keeps creating markers.
- Lower it if relevant quiet sounds disappear.
- Raise smoothing for a faster but more nervous display.
- Lower smoothing for a steadier but slower display.
- Change hard-pan model balance only when known side sounds consistently stop too near the front/back or collapse too early at the side.

Settings should be tested with a repeatable stereo pan sample before being tuned inside a game mix.

## Known limitations

- Multiple simultaneous sources are combined into one L/R energy balance.
- Music, UI sounds, dialogue, reverberation, and game ambience all contribute.
- Dynamic range compression and per-game mixing affect the estimate.
- Stereo alone does not provide reliable elevation.
- Binaural/headphone mixes may contain useful information this amplitude-only estimator ignores.
- A surround endpoint must not be down-selected to two channels without an explicit, documented strategy.
