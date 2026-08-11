[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "SoundDirectionVisualizer.sln"
$projectPath = Join-Path $repositoryRoot "src\SoundDirectionVisualizer.App\SoundDirectionVisualizer.App.csproj"
$releaseDirectory = Join-Path $repositoryRoot "artifacts\release\v$Version"
$publishDirectory = Join-Path $releaseDirectory "publish"
$releaseExecutable = Join-Path $releaseDirectory "SoundDirectionVisualizer-win-x64.exe"
$releaseLicense = Join-Path $releaseDirectory "LICENSE.txt"
$thirdPartyNotices = Join-Path $releaseDirectory "THIRD-PARTY-NOTICES.txt"
$checksumFile = Join-Path $releaseDirectory "SHA256SUMS.txt"

[xml]$project = Get-Content -LiteralPath $projectPath
$projectVersion = $project.SelectSingleNode('//Version').InnerText
if ($projectVersion -ne $Version) {
    throw "Requested version '$Version' does not match project version '$projectVersion'."
}

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet format $solutionPath --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet format verification failed with exit code $LASTEXITCODE."
}

dotnet build $solutionPath --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE."
}

dotnet test $solutionPath --configuration Release --no-build
if ($LASTEXITCODE -ne 0) {
    throw "Release tests failed with exit code $LASTEXITCODE."
}

& (Join-Path $PSScriptRoot "publish-win-x64.ps1") -OutputPath $publishDirectory

$publishedExecutable = Join-Path $publishDirectory "SoundDirectionVisualizer.exe"
Copy-Item -LiteralPath $publishedExecutable -Destination $releaseExecutable -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination $releaseLicense -Force

$projectAssetsPath = Join-Path $repositoryRoot "src\SoundDirectionVisualizer.App\obj\project.assets.json"
$projectAssets = Get-Content -LiteralPath $projectAssetsPath -Raw | ConvertFrom-Json
$targetFramework = $projectAssets.project.frameworks.PSObject.Properties |
    Select-Object -First 1 -ExpandProperty Value
$runtimeDependency = @($targetFramework.downloadDependencies) |
    Where-Object { $_.name -eq "Microsoft.NETCore.App.Runtime.win-x64" } |
    Select-Object -First 1
if (-not $runtimeDependency) {
    throw "Unable to resolve the bundled .NET runtime version from project.assets.json."
}

$runtimeVersion = $runtimeDependency.version.Trim('[', ']') -split ',' | Select-Object -First 1
$runtimeVersion = $runtimeVersion.Trim()
$runtimeNoticesPath = Join-Path $env:USERPROFILE `
    ".nuget\packages\microsoft.netcore.app.runtime.win-x64\$runtimeVersion\THIRD-PARTY-NOTICES.TXT"
$runtimeLicensePath = Join-Path $env:USERPROFILE `
    ".nuget\packages\microsoft.netcore.app.runtime.win-x64\$runtimeVersion\LICENSE.TXT"
if (-not (Test-Path -LiteralPath $runtimeNoticesPath -PathType Leaf)) {
    throw "Bundled .NET runtime notices were not found at '$runtimeNoticesPath'."
}
if (-not (Test-Path -LiteralPath $runtimeLicensePath -PathType Leaf)) {
    throw "Bundled .NET runtime license was not found at '$runtimeLicensePath'."
}

$runtimeNotices = [System.IO.File]::ReadAllText($runtimeNoticesPath)
$runtimeLicense = [System.IO.File]::ReadAllText($runtimeLicensePath)
$naudioNotice = @'


================================================================================
NAudio.Core and NAudio.Wasapi
================================================================================

Copyright (c) 2026 Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'@
$noticePreamble = @"
Third-party notices for Sound Direction Visualizer $Version

The following .NET runtime notices come from Microsoft.NETCore.App.Runtime.win-x64 $runtimeVersion.

"@
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    $thirdPartyNotices,
    $noticePreamble + $runtimeLicense.TrimEnd() + [Environment]::NewLine +
        [Environment]::NewLine + $runtimeNotices.TrimEnd() + $naudioNotice,
    $utf8WithoutBom)

$hashedAssets = @($releaseExecutable, $releaseLicense, $thirdPartyNotices)
$checksumLines = foreach ($asset in $hashedAssets) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($asset))"
}
$checksumLines | Set-Content `
    -LiteralPath $checksumFile `
    -Encoding Ascii

foreach ($asset in $hashedAssets) {
    $assetName = [System.IO.Path]::GetFileName($asset)
    $checksumLine = Get-Content -LiteralPath $checksumFile |
        Where-Object { $_ -match "  $([regex]::Escape($assetName))$" } |
        Select-Object -First 1
    if (-not $checksumLine) {
        throw "Generated checksum file does not contain '$assetName'."
    }

    $expectedHash = $checksumLine.Split(' ')[0].Trim()
    $actualHash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Generated release checksum verification failed for '$assetName'."
    }
}

Write-Host "Release assets verified in $releaseDirectory"
Get-Item -LiteralPath $releaseExecutable, $releaseLicense, $thirdPartyNotices, $checksumFile
