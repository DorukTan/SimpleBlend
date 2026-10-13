#requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$comparisonScript = Join-Path $PSScriptRoot 'Compare-RenderCaptures.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('SimpleBlendCaptureTests_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$checks = 0
function Assert-Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}
function Save-TestImage([string]$Name, [int]$Width, [int]$Height, [System.Drawing.Color]$Color) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear($Color)
        $bitmap.Save((Join-Path $testRoot $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Expect-Rejection([scriptblock]$Operation, [string]$Pattern) {
    $message = $null
    try { & $Operation | Out-Null } catch { $message = $_.Exception.Message }
    Assert-Check ($null -ne $message -and $message -like $Pattern) ('Expected rejection: ' + $Pattern)
}
try {
    Save-TestImage 'reference.png' 8 8 ([System.Drawing.Color]::FromArgb(255, 100, 100, 100))
    Save-TestImage 'near.png' 8 8 ([System.Drawing.Color]::FromArgb(255, 101, 101, 101))
    Save-TestImage 'different.png' 8 8 ([System.Drawing.Color]::FromArgb(255, 200, 200, 200))
    Save-TestImage 'wrong-size.png' 4 8 ([System.Drawing.Color]::Black)
    Save-TestImage 'transparent.png' 8 8 ([System.Drawing.Color]::FromArgb(0, 100, 100, 100))
    $reference = Join-Path $testRoot 'reference.png'
    $same = (& $comparisonScript -Reference $reference -Candidate $reference) | ConvertFrom-Json
    Assert-Check ($same.passed -and $same.metrics.rootMeanSquareRgb -eq 0) 'Identical captures must pass.'
    Assert-Check ($same.referenceSha256 -eq (Get-FileHash -LiteralPath $reference).Hash) 'Reference hash mismatch.'
    $near = (& $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'near.png')) | ConvertFrom-Json
    Assert-Check ($near.passed -and [Math]::Abs($near.metrics.meanAbsoluteRgb - 1.0 / 255) -lt 0.000001) 'One-byte error should pass with exact metrics.'
    $different = (& $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'different.png')) | ConvertFrom-Json
    Assert-Check (!$different.passed -and $different.metrics.fractionAbovePoint025 -eq 1) 'Large errors must fail.'
    Assert-Check ([Math]::Abs($different.metrics.percentile99MaxRgb - 100.0 / 255) -lt 0.000001) 'P99 histogram mismatch.'
    Save-TestImage 'sparse.png' 32 32 ([System.Drawing.Color]::FromArgb(255, 100, 100, 100))
    Save-TestImage 'large-reference.png' 32 32 ([System.Drawing.Color]::FromArgb(255, 100, 100, 100))
    $sparsePath = Join-Path $testRoot 'sparse.png'
    $sparseBitmap = [System.Drawing.Bitmap]::new($sparsePath)
    try {
        $sparseBitmap.SetPixel(0, 0, [System.Drawing.Color]::FromArgb(255, 150, 150, 150))
        $sparseBitmap.Save((Join-Path $testRoot 'sparse-changed.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $sparseBitmap.Dispose() }
    $sparse = (& $comparisonScript -Reference (Join-Path $testRoot 'large-reference.png') -Candidate (Join-Path $testRoot 'sparse-changed.png')) | ConvertFrom-Json
    Assert-Check ($sparse.passed -and $sparse.metrics.percentile99MaxRgb -eq 0 -and
        $sparse.metrics.fractionAbovePoint025 -eq 1.0 / 1024) 'Sparse-error metrics mismatch.'
    $cropped = (& $comparisonScript -Reference $reference -Candidate $reference -X 2 -Y 3 -Width 2 -Height 2) | ConvertFrom-Json
    Assert-Check ($cropped.passed -and $cropped.crop.x -eq 2 -and $cropped.crop.height -eq 2) 'Crop mismatch.'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'wrong-size.png') } '*dimensions differ*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate $reference -X 7 -Width 2 -Height 2 } '*positive crop*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate $reference -Width -1 -Height 2 } '*positive crop*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate $reference -Width 2 } '*positive crop*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate $reference -X -1 -Width 2 -Height 2 } '*positive crop*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'transparent.png') } '*must be opaque*'
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'missing.png') } '*does not exist*'
    $jpeg = [System.Drawing.Bitmap]::new(8, 8)
    try { $jpeg.Save((Join-Path $testRoot 'renamed.png'), [System.Drawing.Imaging.ImageFormat]::Jpeg) }
    finally { $jpeg.Dispose() }
    Expect-Rejection { & $comparisonScript -Reference $reference -Candidate (Join-Path $testRoot 'renamed.png') } '*lossless PNG*'
    [ordered]@{ checks = $checks; result = 'Passed' } | ConvertTo-Json
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $expected = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($expected, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^SimpleBlendCaptureTests_[0-9a-f]{32}$') {
        throw 'Refusing cleanup outside the owned temporary test folder.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
