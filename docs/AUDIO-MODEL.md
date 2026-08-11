# Best-available audio model

## Scope

The baseline estimator analyzes exactly two interleaved channels. The best-available detected-game process path can additionally analyze recognized standard 5.1 and 7.1 layouts. Supported decoder encodings are 32-bit IEEE float and 16/24/32-bit PCM; current explicit multichannel process requests use 48 kHz 32-bit IEEE float. Every channel in a multichannel buffer is decoded, and a partial frame, unknown channel mask, mask/channel-count mismatch, or unsupported layout is rejected instead of silently reading only the first two channels.

This is an output-channel energy visualizer, not source separation, acoustic localization from microphones, or semantic sound recognition.

## Capture source selection

The default source is the selected Windows render endpoint. With `Default Windows output device` selected, this is the current Windows multimedia output. Endpoint loopback captures the final mix sent to that device, which is more reliable for games that divide menu and in-game audio between multiple processes or audio sessions.

Automatic best-available capture is enabled by default. When a Steam game and its strongest same-installation audio process are resolved, endpoint stereo remains the active baseline while a second, bounded process-loopback capture requests standard 7.1 float PCM. If activation fails it requests standard 5.1. The requested and observed `WAVEFORMATEXTENSIBLE` channel masks must match a recognized layout. During validation the probe does not publish direction frames, so a merely negotiated channel count cannot replace a working endpoint result.

An unavailable or uninformative automatic probe is retried during the same game session without restarting the endpoint recorder. The first retry is due after 30 seconds; subsequent rejected attempts back off to 1, 2, 4, and at most 5 minutes. A verified result or a new full audio-capture session clears the pending retry and restores the initial delay. A source transition that is still completing defers a due attempt briefly instead of running two capture transitions at once.

Side/rear energy must be at least 2% of the non-LFE directional energy, and energy not reconstructible as a linear combination of the front left, right, and center channels must be at least 1%. Three useful observations verify the layout immediately. A negative content decision requires at least 32 active buffers spanning eight seconds. Least-squares residual energy is calculated per buffer, so silent surround channels, exact copies, and ordinary stereo-derived linear upmix do not qualify. A verified process stream becomes active and endpoint capture stops. An unavailable, malformed, or uninformative probe stops while endpoint stereo continues. Silence does not count as an active observation, and a separate 12-second wall-clock cap prevents a silent or sparse probe from running indefinitely.

The setting can be disabled without disabling endpoint capture. It does not install a driver, change Windows speaker or spatial-sound settings, or require physical surround hardware. If Windows or the game does not expose discrete multichannel content, fallback is the expected result.

The disabled-by-default debug-force setting changes source selection, not the evidence threshold of the estimator. Once Windows successfully exposes a recognized 7.1/5.1 stream for the detected game process, that recorder becomes the active source immediately and every non-LFE channel contributes to its stereo fold-down while validation is pending. A verified result switches the estimator to layout-aware multichannel direction. An uninformative result keeps the forced process source but permanently retains the stereo fold-down for that attempt; explicit front/back ambiguity therefore remains. Failed format activation leaves endpoint stereo active and participates in the same bounded retry schedule. Turning the debug setting off starts a normal new capture session.

While debug force is enabled, the Status tab displays the smoothed RMS level of every channel in the currently publishing capture stream on a logarithmic −60…0 dBFS meter. Recognized 5.1/7.1 process streams expose all positions in channel-mask order, including LFE for diagnostics; endpoint fallback exposes its current left and right channels. This visibility does not alter validation or estimation, and LFE remains excluded from all directional calculations. Only immutable aggregate levels cross the capture/UI boundary, not sample buffers. A frame older than one second is shown as waiting rather than as a frozen live reading.

If the default endpoint remains below the effective silence gate for eight seconds, the application occasionally reads the lightweight peak meters of other active render endpoints. It can temporarily move the one active loopback capture to the strongest endpoint that reports stereo and meaningful activity. Empty scans back off from five seconds to a maximum 30-second interval, avoiding parallel capture and continuous idle polling. Explicitly selected non-default endpoints are never changed automatically.

Direct detected-game process capture remains an optional setting. It first uses the same 7.1/5.1 negotiation and validation. Until validation succeeds, all recognized channels are folded to stereo by summing channel energy by side: front/side/back left feed left, their right counterparts feed right, front/back center energy is split equally, and LFE is ignored. This retains explicit stereo front/back ambiguity without discarding the extra input channels. If multichannel activation fails, a native stereo process stream is requested; if all process activation fails, endpoint loopback is used. Process capture can miss audio routed through a separate sibling process even though child processes are included.

An independent automatic fallback is enabled by default. While a Steam game is detected and endpoint capture is active, at least 32 audible front/back-only frames must remain within an absolute raw-balance tolerance of `0.0025` across eight seconds before process capture is requested. A lateral audible frame resets the interval; a quiet gap longer than two seconds also resets it, so an isolated centered sound followed by silence cannot trigger a switch. The automatic choice lasts only for the current detected game session and does not write the manual process-capture setting. It uses no per-game compatibility list and can be disabled with its own Audio-tab checkbox. This heuristic can still activate during legitimately long centered content, because equal channel energy alone cannot distinguish dual mono from a real center sound.

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

For 5.1/7.1, the same RMS calculation and smoothing are applied independently to every interleaved channel. The stereo fallback aggregate is derived from squared RMS energy rather than raw sample addition, avoiding phase cancellation between unrelated speaker feeds. LFE is retained in the decoded level frame for diagnostics but excluded from the fallback direction, silence gate, validation energy denominator, and multichannel direction estimator.

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

## Verified multichannel azimuth

The richer estimator is used only after validation. It assigns nominal horizontal azimuths to standard speaker positions:

| Speaker position | Azimuth |
|---|---:|
| Front center | `0 degrees` |
| Front left / right | `330 / 30 degrees` |
| Side left / right | `270 / 90 degrees` |
| Back left / right | `210 / 150 degrees` |
| Back center | `180 degrees` |
| LFE | no azimuth; ignored |

For every non-LFE channel, squared RMS is treated as energy. The estimator adds energy-weighted unit vectors and reports the resultant azimuth when its concentration is at least `0.12` of total directional energy. This continuously interpolates between adjacent speakers; for example, equal front-right and side-right energy maps to `60 degrees`. The numeric angles are model positions, not measurements of the user's physical speaker placement.

If opposing or diffuse energy makes the resultant too weak, the estimator does not invent an unstable average. It returns the nominal azimuths of channels carrying at least half the strongest channel energy, so the existing multi-candidate overlay communicates the remaining uncertainty. Multiple simultaneous sources are still combined within each captured block and are not separated.

## Why front and back are both shown

A plain stereo amplitude pair contains no general, reliable front/back label. The same L/R balance can be produced by a source mirrored across the listener's left-right axis. Game-specific binaural processing may encode additional spectral and timing cues, but this first estimator intentionally does not claim to decode them.

The two rays are therefore a feature: they communicate the information that is present and the ambiguity that remains. They stay in use for endpoint stereo, native stereo process capture, and the multichannel process fold-down before or after failed validation. Only a verified layout with useful independent side/rear information removes this default mirror ambiguity.

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

- Multiple simultaneous sources are combined into one stereo balance or one multichannel energy vector; the estimator does not separate sources.
- Relative level classification cannot determine whether a loud frame is a gunshot, UI sound, nearby engine, music peak, or several simultaneous sources.
- Process capture includes the selected same-installation audio process tree. Audio routed through a process outside the verified Steam game directory may still require endpoint fallback.
- Direct game capture requires Windows build 19041 or newer and falls back automatically when unavailable.
- A 5.1/7.1 format is useful only if the game and Windows audio path actually deliver discrete side/rear content. Negotiated but silent, copied, or linearly stereo-derived channels stay on stereo fallback.
- Validation can reject an individual attempt whose first eight active seconds contain no useful side/rear event even if a later scene would have exposed one. Automatic mode keeps endpoint stereo active and retries with bounded backoff, so a later attempt can still verify the same game; changing the resolved game/audio process starts a fresh capture session and resets that retry schedule.
- Nominal speaker azimuths describe the standard channel model, not exact physical speaker angles or object-audio coordinates.
- Native multichannel render endpoints remain unsupported until the separate endpoint phase; they are never silently down-selected to stereo.
- The automatic centered-output fallback is a heuristic: eight seconds of genuinely centered content can look identical to a dual-mono endpoint and cause an unnecessary but session-local process-capture attempt.
- An endpoint-loopback fallback that exposes true dual mono contains no recoverable left/right direction information.
- Automatic calibration can amplify a narrow L/R energy difference, but cannot recover direction when a binaural mix encodes it only in timing or spectral cues and has equal channel energy.
- Music, UI sounds, dialogue, reverberation, and game ambience all contribute.
- Dynamic range compression and per-game mixing affect the estimate.
- Stereo alone does not provide reliable elevation.
- Binaural/headphone mixes may contain useful information this amplitude-only estimator ignores.
