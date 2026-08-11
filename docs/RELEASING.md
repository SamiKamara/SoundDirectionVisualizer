# Releasing Sound Direction Visualizer

Sound Direction Visualizer releases are built by GitHub Actions from immutable semantic-version tags. The workflow publishes a ready-to-run Windows executable, so end users do not need the source tree or the .NET SDK.

## Published assets

Every release contains these manually uploaded assets:

- `SoundDirectionVisualizer-win-x64.exe` — self-contained, single-file Windows x64 application
- `LICENSE.txt` — the application's MIT license
- `THIRD-PARTY-NOTICES.txt` — notices for the bundled .NET runtime and NAudio libraries
- `SHA256SUMS.txt` — SHA-256 checksums for all three files above

GitHub also adds automatic source archives. Users who only want the application should download the named Windows executable from the Assets section.

The latest release page is:

```text
https://github.com/SamiKamara/SoundDirectionVisualizer/releases/latest
```

The stable direct-download URL is:

```text
https://github.com/SamiKamara/SoundDirectionVisualizer/releases/latest/download/SoundDirectionVisualizer-win-x64.exe
```

## One-command release process

1. Ensure `main` contains the code intended for release.
2. Update `<Version>` in `src/SoundDirectionVisualizer.App/SoundDirectionVisualizer.App.csproj`.
3. Add a dated matching section to `CHANGELOG.md`.
4. Commit and push the release preparation to `main`.
5. From a clean, synchronized `main`, run:

   ```powershell
   .\scripts\create-release.ps1 -Version 1.1.0 -Push
   ```

The script checks the project version, changelog, branch, remote commit, and existing tags. It then runs the complete local release build, synchronizes the final executable to verified local launch targets, restarts only instances that were running before the update, creates an annotated tag, and optionally pushes it.

Pushing the tag starts the `Build release` workflow. Monitor and verify it with:

```powershell
gh run list --workflow release.yml --limit 5
$runId = gh run list --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId'
gh run watch $runId --exit-status
gh release view v1.1.0
```

Do not move or recreate an existing release tag. Correct a broken release with a new patch version. Use the manual rerun below only when the tagged source is correct and the remote build or upload needs to be repeated.

## What the local tool and workflow verify

- The version is a three-part semantic version.
- The project, changelog, and tag versions agree.
- The local release is created only from clean `main` matching `origin/main`.
- Dependencies restore successfully.
- `dotnet format` reports no changes.
- The Release build and full automated tests pass.
- Publishing produces a self-contained Windows x64 executable without debug symbols.
- Version-specific third-party notices and SHA-256 checksums are generated and verified.
- The release uploads only the executable, licenses/notices, and checksum manifest, never `bin`, `obj`, runtime packs, test output, or the full `artifacts` tree.

The GitHub workflow uses the repository-scoped `GITHUB_TOKEN` with only `contents: write`. No personal access token or additional repository secret is required.

## Manual workflow rerun

If the tag exists but its release workflow needs to be rerun:

```powershell
gh workflow run release.yml --ref main -f tag=v1.1.0
```

The workflow checks out the existing tag. If the release already exists, its four manually uploaded assets are replaced with freshly built copies; otherwise the release is created.

## Verify a downloaded executable

Download the executable and `SHA256SUMS.txt` into the same folder and run:

```powershell
$assetName = 'SoundDirectionVisualizer-win-x64.exe'
$actual = (Get-FileHash ".\$assetName" -Algorithm SHA256).Hash.ToLowerInvariant()
$line = Get-Content .\SHA256SUMS.txt | Where-Object { $_ -match "  $([regex]::Escape($assetName))$" }
$expected = $line.Split(' ')[0].Trim()
if ($actual -ne $expected) { throw "Checksum mismatch." }
Write-Host "Checksum verified: $actual"
```

## Signing status

The executable is not currently Authenticode-signed. Windows SmartScreen may therefore show an unknown-publisher warning. Signing can be added later without changing the asset contract: sign the release executable after publishing and before calculating `SHA256SUMS.txt`.
