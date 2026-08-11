[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $repositoryRoot 'src\SoundDirectionVisualizer.App\Assets'
$pngPath = Join-Path $assetDirectory 'SoundDirectionVisualizerIcon.png'
$icoPath = Join-Path $assetDirectory 'SoundDirectionVisualizerIcon.ico'
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)

function New-SoundDirectionBitmap {
    param(
        [Parameter(Mandatory)]
        [int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        $background = [System.Drawing.Color]::FromArgb(13, 17, 29)
        $accent = [System.Drawing.Color]::FromArgb(62, 213, 240)
        $marker = [System.Drawing.Color]::FromArgb(240, 244, 250)
        $graphics.Clear($background)

        $center = $Size / 2.0
        $radius = $Size * 0.285
        $ringThickness = [Math]::Max(1.25, $Size * 0.048)
        $markerRadius = [Math]::Max(1.20, $Size * 0.078)
        $markerHaloRadius = $markerRadius + [Math]::Max(0.55, $ringThickness * 0.38)

        $ringPen = [System.Drawing.Pen]::new($accent, [single] $ringThickness)
        $backgroundBrush = [System.Drawing.SolidBrush]::new($background)
        $markerBrush = [System.Drawing.SolidBrush]::new($marker)

        try {
            $ringBounds = [System.Drawing.RectangleF]::new(
                [single] ($center - $radius),
                [single] ($center - $radius),
                [single] ($radius * 2),
                [single] ($radius * 2))
            $graphics.DrawEllipse($ringPen, $ringBounds)

            foreach ($azimuth in @(52.0, 128.0)) {
                $radians = $azimuth * [Math]::PI / 180.0
                $markerX = $center + $radius * [Math]::Sin($radians)
                $markerY = $center - $radius * [Math]::Cos($radians)

                $graphics.FillEllipse(
                    $backgroundBrush,
                    [single] ($markerX - $markerHaloRadius),
                    [single] ($markerY - $markerHaloRadius),
                    [single] ($markerHaloRadius * 2),
                    [single] ($markerHaloRadius * 2))
                $graphics.FillEllipse(
                    $markerBrush,
                    [single] ($markerX - $markerRadius),
                    [single] ($markerY - $markerRadius),
                    [single] ($markerRadius * 2),
                    [single] ($markerRadius * 2))
            }
        }
        finally {
            $markerBrush.Dispose()
            $backgroundBrush.Dispose()
            $ringPen.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Convert-BitmapToPngBytes {
    param(
        [Parameter(Mandatory)]
        [System.Drawing.Bitmap] $Bitmap
    )

    $stream = [System.IO.MemoryStream]::new()
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null

$readmeBitmap = New-SoundDirectionBitmap -Size 1024
try {
    $readmeBitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $readmeBitmap.Dispose()
}

$frames = @(foreach ($size in $iconSizes) {
    $bitmap = New-SoundDirectionBitmap -Size $size
    try {
        [pscustomobject]@{
            Size = $size
            Data = [byte[]] (Convert-BitmapToPngBytes -Bitmap $bitmap)
        }
    }
    finally {
        $bitmap.Dispose()
    }
})

$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16] 0)
    $writer.Write([uint16] 1)
    $writer.Write([uint16] $frames.Count)

    $imageOffset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $encodedSize = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte] $encodedSize)
        $writer.Write([byte] $encodedSize)
        $writer.Write([byte] 0)
        $writer.Write([byte] 0)
        $writer.Write([uint16] 1)
        $writer.Write([uint16] 32)
        $writer.Write([uint32] $frame.Data.Length)
        $writer.Write([uint32] $imageOffset)
        $imageOffset += $frame.Data.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]] $frame.Data)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($icoPath, $iconStream.ToArray())
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

$png = [System.Drawing.Image]::FromFile($pngPath)
try {
    if ($png.Width -ne 1024 -or $png.Height -ne 1024) {
        throw "Unexpected README icon dimensions: $($png.Width)x$($png.Height)."
    }
}
finally {
    $png.Dispose()
}

$iconBytes = [System.IO.File]::ReadAllBytes($icoPath)
$frameCount = [System.BitConverter]::ToUInt16($iconBytes, 4)
if ($frameCount -ne $iconSizes.Count) {
    throw "Unexpected ICO frame count: $frameCount."
}

$expectedOffset = 6 + 16 * $frameCount
for ($index = 0; $index -lt $frameCount; $index++) {
    $entryOffset = 6 + 16 * $index
    $frameLength = [System.BitConverter]::ToUInt32($iconBytes, $entryOffset + 8)
    $frameOffset = [System.BitConverter]::ToUInt32($iconBytes, $entryOffset + 12)
    if ($frameOffset -ne $expectedOffset -or $frameOffset + $frameLength -gt $iconBytes.Length) {
        throw "Invalid ICO frame offset or length at index $index."
    }

    $pngSignature = @(137, 80, 78, 71, 13, 10, 26, 10)
    for ($signatureIndex = 0; $signatureIndex -lt $pngSignature.Count; $signatureIndex++) {
        if ($iconBytes[$frameOffset + $signatureIndex] -ne $pngSignature[$signatureIndex]) {
            throw "ICO frame $index is not PNG encoded."
        }
    }

    $expectedOffset += $frameLength
}

if ($expectedOffset -ne $iconBytes.Length) {
    throw "Unexpected trailing or missing ICO data."
}

Write-Output "Generated $pngPath"
Write-Output "Generated $icoPath with sizes $($iconSizes -join ', ')"
