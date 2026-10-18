#requires -Version 5.1
[CmdletBinding()]
param([string]$PackageRoot = (Join-Path $PSScriptRoot '../Packages/com.simpletools.simpleblend'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackageRoot).ProviderPath
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.md')
$checked = 0
foreach ($file in $files) {
    $content = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, '(?<!!)\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(https?://|mailto:|#)') { continue }
        $target = $target.Split('#')[0]
        $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        if (!$resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw ('Link escapes the package: ' + $file.Name + ' -> ' + $target)
        }
        if (!(Test-Path -LiteralPath $resolved)) { throw ('Broken local link: ' + $file.Name + ' -> ' + $target) }
        $checked++
    }
}
$setup = [IO.File]::ReadAllText((Join-Path $root 'Editor/Data/VertexPaintSetupUtility.cs'))
$inspector = [IO.File]::ReadAllText((Join-Path $root 'Editor/Data/VertexPaintDataInspector.cs'))
$shaderGui = [IO.File]::ReadAllText((Join-Path $root 'Editor/Shaders/SimpleBlendLayerBlendShaderGUI.cs'))
foreach ($label in @('GameObject/SimpleBlend/Enable Vertex Painting', 'CONTEXT/Renderer/Enable SimpleBlend Vertex Painting')) {
    if (!$setup.Contains($label)) { throw ('Documented setup command changed: ' + $label) }
}
foreach ($label in @('Initialize Vertex Painting','Activate SimpleBlend Paint Tool','Reapply Paint Data','Clear Paint Data','Update Storage Policy')) {
    if (!$inspector.Contains($label)) { throw ('Documented Inspector control changed: ' + $label) }
}
if (!$shaderGui.Contains('"Height Transition"')) { throw 'Documented shader control changed.' }
$index = [IO.File]::ReadAllText((Join-Path $root 'Documentation~/index.md'))
foreach ($page in @('installation','compatibility','first-stroke','workspace','surface-palettes','brush-controls','data-safety','layer-blend-shaders','shader-graph','terrain-binding','teaching-samples','troubleshooting','removal')) {
    if (!$index.Contains($page + '.md')) { throw ('Documentation page is not indexed: ' + $page) }
}
$workspace = [IO.File]::ReadAllText((Join-Path $root 'Editor/Workspace/SimpleBlendWorkspace.cs'))
foreach ($label in @('Window/SimpleBlend/Workspace','Set up painting','Start painting','Stop painting','Make a more detailed paint copy')) {
    if (!$workspace.Contains($label)) { throw ('Documented workspace control changed: ' + $label) }
}
$creator = [IO.File]::ReadAllText((Join-Path $root 'Editor/Workspace/SurfacePaletteCreator.cs'))
foreach ($label in @('Window/SimpleBlend/Create Surface Palette','Create palette assets')) {
    if (!$creator.Contains($label)) { throw ('Documented palette control changed: ' + $label) }
}
if (!$workspace.Contains('Assign palette material to this slot')) { throw 'Documented palette assignment changed.' }
[ordered]@{ files = $files.Count; localLinks = $checked; sourceControlChecks = 16; indexedPages = 13; result = 'Passed' } | ConvertTo-Json
