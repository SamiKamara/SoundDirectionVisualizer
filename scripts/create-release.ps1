[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$Push,

    [switch]$SkipLocalSync
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\SoundDirectionVisualizer.App\SoundDirectionVisualizer.App.csproj"
$changelogPath = Join-Path $repositoryRoot "CHANGELOG.md"
$tag = "v$Version"

Push-Location $repositoryRoot
try {
    $status = @(git status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read git status."
    }

    if ($status.Count -ne 0) {
        throw "Release creation requires a clean worktree. Commit or discard changes first."
    }

    $branch = (git branch --show-current).Trim()
    if ($branch -ne "main") {
        throw "Releases must be created from 'main'; current branch is '$branch'."
    }

    [xml]$project = Get-Content -LiteralPath $projectPath
    $projectVersion = $project.SelectSingleNode('//Version').InnerText
    if ($projectVersion -ne $Version) {
        throw "Version '$Version' does not match project version '$projectVersion'."
    }

    $changelog = [System.IO.File]::ReadAllText($changelogPath, [System.Text.Encoding]::UTF8)
    if ($changelog -notmatch "(?m)^## \[$([regex]::Escape($Version))\] - \d{4}-\d{2}-\d{2}$") {
        throw "CHANGELOG.md does not contain a dated [$Version] release heading."
    }

    git fetch origin main --tags
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch origin/main and tags."
    }

    $headCommit = (git rev-parse HEAD).Trim()
    $remoteMainCommit = (git rev-parse origin/main).Trim()
    if ($headCommit -ne $remoteMainCommit) {
        throw "Local main must exactly match origin/main before release tagging."
    }

    git rev-parse --verify --quiet "refs/tags/$tag" *> $null
    $localTagExitCode = $LASTEXITCODE
    if ($localTagExitCode -eq 0) {
        throw "Local tag '$tag' already exists."
    }
    if ($localTagExitCode -ne 1) {
        throw "Unable to determine whether local tag '$tag' exists."
    }

    git ls-remote --exit-code --tags origin "refs/tags/$tag" *> $null
    $remoteTagExitCode = $LASTEXITCODE
    if ($remoteTagExitCode -eq 0) {
        throw "Remote tag '$tag' already exists."
    }
    if ($remoteTagExitCode -ne 2) {
        throw "Unable to determine whether remote tag '$tag' exists."
    }

    & (Join-Path $PSScriptRoot "build-release.ps1") -Version $Version

    if (-not $SkipLocalSync) {
        $releaseExecutable = Join-Path $repositoryRoot "artifacts\release\$tag\SoundDirectionVisualizer-win-x64.exe"
        & (Join-Path $PSScriptRoot "sync-local-build.ps1") -SourceExecutable $releaseExecutable
    }

    git tag -a $tag -m "Sound Direction Visualizer $tag"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create tag '$tag'."
    }

    if ($Push) {
        git push origin $tag
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push tag '$tag'. The local tag remains available for retry."
        }

        Write-Host "Pushed $tag. GitHub Actions will build and publish the release."
    }
    else {
        Write-Host "Created local tag $tag. Push it with: git push origin $tag"
    }
}
finally {
    Pop-Location
}
