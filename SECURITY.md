# Security policy

## Supported versions

Security fixes are provided for the latest `1.x` release. Users should update to the newest published patch version before reporting a problem that may already be fixed.

## Reporting a vulnerability

Please report vulnerabilities through [GitHub private vulnerability reporting](https://github.com/SamiKamara/SoundDirectionVisualizer/security/advisories/new). Do not open a public issue for a suspected vulnerability.

Include the affected version, Windows version, impact, and minimal reproduction steps. Remove personal filesystem paths, account identifiers, private game logs, and captured audio. Sound Direction Visualizer does not need recorded audio to investigate source-selection or direction-analysis failures.

The maintainer will acknowledge a valid report, investigate it privately, and coordinate disclosure with the reporter when practical. Please do not publish exploit details before a fix or mitigation is available.

## Scope notes

- The application analyzes audio locally and does not intentionally transmit or save captured audio.
- Settings are stored in `%AppData%\SoundDirectionVisualizer\settings.json`.
- Exclusive fullscreen, protected presentation paths, anti-cheat restrictions, and unsupported channel layouts are compatibility limitations rather than security bypass targets.
- Reports requesting bypasses of anti-cheat, protected audio, or protected overlays are out of scope.
