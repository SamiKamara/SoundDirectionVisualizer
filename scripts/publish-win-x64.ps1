[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\SoundDirectionVisualizer.App\SoundDirectionVisualizer.App.csproj"
$outputPath = Join-Path $repositoryRoot "artifacts\publish\win-x64"

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $outputPath

Write-Host "Published SoundDirectionVisualizer.exe to $outputPath"
