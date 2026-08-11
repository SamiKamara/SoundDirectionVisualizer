[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SourceExecutable
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = (Resolve-Path -LiteralPath $SourceExecutable).Path
$executableNames = @(
    "SoundDirectionVisualizer.exe",
    "SoundDirectionVisualizer-win-x64.exe")
if ([System.IO.Path]::GetFileName($sourcePath) -notin $executableNames) {
    throw "Unexpected source executable '$sourcePath'."
}

$targets = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$shortcutsByTarget = @{}
$runningPaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

$standardTarget = Join-Path $repositoryRoot "artifacts\publish\win-x64\SoundDirectionVisualizer.exe"
[void]$targets.Add([System.IO.Path]::GetFullPath($standardTarget))

$shortcutShell = New-Object -ComObject WScript.Shell
$desktopDirectories = @(
    [Environment]::GetFolderPath('DesktopDirectory'),
    [Environment]::GetFolderPath('CommonDesktopDirectory')) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } |
    Select-Object -Unique

foreach ($desktopDirectory in $desktopDirectories) {
    foreach ($shortcutFile in Get-ChildItem -LiteralPath $desktopDirectory -Filter '*.lnk' -File) {
        $shortcut = $shortcutShell.CreateShortcut($shortcutFile.FullName)
        if ([string]::IsNullOrWhiteSpace($shortcut.TargetPath)) {
            continue
        }

        $targetPath = [System.IO.Path]::GetFullPath($shortcut.TargetPath)
        if ([System.IO.Path]::GetFileName($targetPath) -notin $executableNames) {
            continue
        }

        [void]$targets.Add($targetPath)
        $shortcutsByTarget[$targetPath] = $shortcutFile.FullName
    }
}

$runningProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            [System.IO.Path]::GetFileName($_.Path) -in $executableNames
        }
        catch {
            $false
        }
    })
foreach ($runningProcess in $runningProcesses) {
    try {
        $runningPath = [System.IO.Path]::GetFullPath($runningProcess.Path)
    }
    catch {
        continue
    }

    if ([System.IO.Path]::GetFileName($runningPath) -notin $executableNames) {
        continue
    }

    [void]$targets.Add($runningPath)
    [void]$runningPaths.Add($runningPath)
}

foreach ($runningProcess in $runningProcesses) {
    try {
        $runningPath = [System.IO.Path]::GetFullPath($runningProcess.Path)
    }
    catch {
        continue
    }

    if (-not $runningPaths.Contains($runningPath)) {
        continue
    }

    Stop-Process -Id $runningProcess.Id
    $runningProcess.WaitForExit()
}

$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
$results = @()
foreach ($targetPath in $targets) {
    $targetDirectory = Split-Path -Parent $targetPath
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

    if ($targetPath -ne $sourcePath) {
        $copied = $false
        for ($attempt = 1; $attempt -le 10; $attempt++) {
            try {
                Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
                $copied = $true
                break
            }
            catch [System.IO.IOException] {
                if ($attempt -eq 10) {
                    throw
                }

                Start-Sleep -Milliseconds 300
            }
        }

        if (-not $copied) {
            throw "Failed to update '$targetPath'."
        }
    }

    $targetHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
    if ($targetHash -ne $sourceHash) {
        throw "Hash mismatch after updating '$targetPath'."
    }

    $results += [pscustomobject]@{
        Target = $targetPath
        SHA256 = $targetHash
        WasRunning = $runningPaths.Contains($targetPath)
    }
}

foreach ($runningPath in $runningPaths) {
    if ($shortcutsByTarget.ContainsKey($runningPath)) {
        Start-Process -FilePath $shortcutsByTarget[$runningPath]
    }
    else {
        Start-Process -FilePath $runningPath
    }

    $restarted = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        Start-Sleep -Milliseconds 200
        $matchingProcess = Get-Process -ErrorAction SilentlyContinue |
            Where-Object {
                try {
                    [System.IO.Path]::GetFileName($_.Path) -in $executableNames -and
                        [System.IO.Path]::GetFullPath($_.Path) -eq $runningPath
                }
                catch {
                    $false
                }
            } |
            Select-Object -First 1
        if ($matchingProcess) {
            $restarted = $true
            break
        }
    }

    if (-not $restarted) {
        throw "Updated application did not restart from '$runningPath'."
    }
}

$results | Sort-Object Target
