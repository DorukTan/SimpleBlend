#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Reference,
    [Parameter(Mandatory = $true)][string]$Candidate,
    [int]$X = 0,
    [int]$Y = 0,
    [int]$Width = 0,
    [int]$Height = 0
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This comparison script uses System.Drawing on Windows.'
}
Add-Type -AssemblyName System.Drawing
$referencePath = (Resolve-Path -LiteralPath $Reference).ProviderPath
$candidatePath = (Resolve-Path -LiteralPath $Candidate).ProviderPath
$first = $null
$second = $null
try {
    $first = [System.Drawing.Bitmap]::new($referencePath)
    $second = [System.Drawing.Bitmap]::new($candidatePath)
    if ($first.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid -or
        $second.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) {
        throw 'Both captures must be lossless PNG files.'
    }
    if ($first.Width -ne $second.Width -or $first.Height -ne $second.Height) {
        throw 'Capture dimensions differ. Do not resize or align images after capture.'
    }
    if ($Width -eq 0 -and $Height -eq 0 -and $X -eq 0 -and $Y -eq 0) {
        $Width = $first.Width
        $Height = $first.Height
    }
    if ($X -lt 0 -or $Y -lt 0 -or $Width -le 0 -or $Height -le 0 -or
        ([long]$X + $Width) -gt $first.Width -or ([long]$Y + $Height) -gt $first.Height) {
        throw 'Specify a positive crop fully inside the unchanged capture dimensions.'
    }
    $histogram = [long[]]::new(256)
    [double]$absolute = 0
    [double]$squared = 0
    [long]$changed = 0
    [long]$pixels = [long]$Width * $Height
    for ($row = $Y; $row -lt $Y + $Height; $row++) {
        for ($column = $X; $column -lt $X + $Width; $column++) {
            $a = $first.GetPixel($column, $row)
            $b = $second.GetPixel($column, $row)
            if ($a.A -ne 255 -or $b.A -ne 255) {
                throw 'The comparison region must be opaque. Capture against a fixed solid background.'
            }
            $red = [Math]::Abs([int]$a.R - [int]$b.R)
            $green = [Math]::Abs([int]$a.G - [int]$b.G)
            $blue = [Math]::Abs([int]$a.B - [int]$b.B)
            $absolute += $red + $green + $blue
            $squared += $red * $red + $green * $green + $blue * $blue
            $maximum = [Math]::Max($red, [Math]::Max($green, $blue))
            $histogram[$maximum]++
            if ($maximum / 255.0 -gt 0.025) { $changed++ }
        }
    }
    $rank = [long][Math]::Ceiling($pixels * 0.99)
    [long]$seen = 0
    $p99 = 0.0
    for ($index = 0; $index -lt 256; $index++) {
        $seen += $histogram[$index]
        if ($seen -ge $rank) { $p99 = $index / 255.0; break }
    }
    $mean = $absolute / ($pixels * 3.0 * 255.0)
    $rmse = [Math]::Sqrt($squared / ($pixels * 3.0 * 255.0 * 255.0))
    $fraction = $changed / [double]$pixels
    [ordered]@{
        protocol = 'simpleblend-visual-v1'
        reference = $referencePath
        candidate = $candidatePath
        referenceSha256 = (Get-FileHash -LiteralPath $referencePath -Algorithm SHA256).Hash
        candidateSha256 = (Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash
        imageWidth = $first.Width
        imageHeight = $first.Height
        crop = [ordered]@{ x = $X; y = $Y; width = $Width; height = $Height }
        domain = 'stored PNG RGB bytes normalized to 0..1; no gamma conversion'
        metrics = [ordered]@{
            meanAbsoluteRgb = $mean
            rootMeanSquareRgb = $rmse
            percentile99MaxRgb = $p99
            fractionAbovePoint025 = $fraction
        }
        limits = [ordered]@{
            meanAbsoluteRgb = 0.005
            rootMeanSquareRgb = 0.01
            percentile99MaxRgb = 0.025
            fractionAbovePoint025 = 0.01
        }
        passed = ($mean -le 0.005 -and $rmse -le 0.01 -and $p99 -le 0.025 -and $fraction -le 0.01)
        ownerReviewRequired = $true
    } | ConvertTo-Json -Depth 5
}
finally {
    if ($null -ne $first) { $first.Dispose() }
    if ($null -ne $second) { $second.Dispose() }
}
