# M8 visual acceptance protocol

Status: **prepared, not observed**. Issue #4 remains open. This protocol does not turn pass-compilation or isolated GPU probes into appearance acceptance.

## Prerequisites

Use a disposable Unity project or dedicated acceptance checkout. Preserve the development project's scenes and render settings.

The graph references are in PR #14; terrain binding/shading are in PRs #15/#16. This documentation branch builds on #16 and does not contain #14. A combined run needs a reviewed integration of those implementations. Do not merge or overwrite a working checkout merely to obtain screenshots. Record the exact tested commit(s), including any local integration resolution.

Verified implementation baseline is Unity 6000.4.8f1, URP 17.4.0, Shader Graph 17.4.0, Windows/D3D11. That is a starting point, not evidence that this visual protocol has passed. Unity 6.3 LTS and 6.6 need separate runs.

Copy `docs/acceptance/m8-report-template.md` into an evidence report before taking captures. Record all settings and source hashes first. Freeze this protocol version and crop rectangles before comparing images. The thresholds below are initial acceptance criteria, not measured results.

## Fixed capture settings

| Setting | Required value |
|---|---|
| Resolution | 1920 × 1080, no resizing or supersampling |
| Format | Opaque 8-bit lossless PNG; native render output, not a compressed chat image |
| Color space | Linear project; comparison uses stored PNG RGB bytes, not linear-light reconstruction |
| Camera | Perspective, vertical FOV 60°, clipping 0.1–100 m |
| Camera background | Solid color (0.1, 0.1, 0.1, 1), no skybox |
| Render scale | 1.0; dynamic resolution off |
| Numeric comparison | HDR, post processing, anti-aliasing, fog, SSAO and decals off |
| Lighting | One white directional light, intensity 1, rotation (50°, -30°, 0°) |
| Shadows | Off for numeric A/B comparisons; separate shadows-on visual review |
| Environment | Ambient/reflection inputs identical within a pair; record exact values/assets |
| Rendering | Forward and Deferred, each recorded separately; do not mix paths in an A/B pair |
| Player | Same commit, assets, graphics API, quality level and resolution as Editor |

Record the actual URP asset and renderer data GUIDs and hashes, effective rendering path, quality level, graphics API, GPU/driver, light settings, and any pipeline setting not covered here. Select the matching quality-level asset in the player; the default Graphics asset alone is not sufficient.

Capture the Game camera output after at least ten stationary frames. Use Unity's [ScreenCapture.CaptureScreenshot](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html) or an equivalent lossless native-output method. Record that method; do not include editor chrome or Scene view gizmos. Capture after the frame is rendered, not during scene setup. Use unique filenames so reruns do not overwrite evidence.

## Graph/HLSL reference

Materials under `Packages/com.simpletools.simpleblend/ShaderGraph/Examples/Materials`:

- `Hlsl3.mat` versus `Graph3.mat`.
- `Hlsl4.mat` versus `Graph4.mat`.

Use one owned, painted mesh with valid normals, tangents and UVs. A Unity Plane is a starting fixture; record the mesh GUID/hash, transform and paint data. Include unpainted, extreme Red/Green (and Blue for the four-layer case), and mixed-height regions in the painted surface. Record the actual paint once; do not repaint between captures.

Use the same renderer and change only its assigned material for A/B. Do not compare side-by-side meshes at different positions: light, probe and view differences would confound the result. Duplicate package materials into the acceptance folder if adjustments are required; keep each pair's textures, tints, tiling, normal scales, metallic, AO, smoothness and transition values identical.

Center the ground-facing fixture at world origin. Aim at the visible painted surface center. The terrain camera placement rule below also applies, with this mesh center as target. Take all three distances in each rendering path, in both Editor and player. Inspect layer boundaries and highlights, not just flat albedo. Render each selected material against itself twice first; unstable control captures invalidate subsequent comparisons.

## Terrain reference

`SimpleBlend.Editor.Tests.TerrainBlendReferenceFixture.Create(assetRoot)` creates a reproducible owned terrain/seam scene in an existing empty Assets folder. Save or close untitled scenes before invoking it; it rejects that setup without changing a scene. The dedicated player-build fixture is not a persistent scene authoring command: it cleans up its generated assets.

Use the generated `TerrainBlendReference.unity` scene. It has a translated 16 × 16 m terrain at (14, 2, -9), four diffuse layers, and an RGB strip spanning the terrain edge. It uses linear terrain-layer blending; terrain height/density blending, holes and custom source shaders are unsupported.

For the seam cameras use target **(26, 2.02, -1)** and:
`position = target + (0, distance / sqrt(2), -distance / sqrt(2))`.
Aim directly at the target, with zero roll.

| Capture | Distance from target | Approximate position |
|---|---|---|
| Near | 4 m | (26, 4.848427, -3.828427) |
| Medium | 12 m | (26, 10.505281, -9.485281) |
| Far | 32 m | (26, 24.647417, -23.627417) |

Override the generated camera and numeric comparison light settings in the owned scene only. Keep the strip's intentional 0.02 m elevation; record it. Review the unpainted seam within the source terrain footprint separately from painted overlays outside it, where control weights clamp.

Take two captures from each fixed camera:

1. Source terrain with the strip renderer disabled.
2. Identical scene/camera with the strip renderer enabled.

For numeric comparisons, use the centered 32 × 32 pixel crop at **X=944, Y=524** only after confirming it lies entirely inside the unpainted strip and valid terrain footprint. Do not compare painted regions or silhouettes to bare terrain. If the crop is invalid, stop and define a revised protocol before comparing; do not move it to avoid an observed defect.

Full-frame terrain images are for owner review, not a global A/B error metric: overlay and silhouette changes are intentional. Inspect strip borders at all distances for texture alignment, abrupt lighting/color changes, flickering and z-fighting. Repeat with shadows on as a separate review, without treating those captures as the shadows-off numeric pair.

The supplied scene covers diffuse-only terrain layers. A complete gate also needs an owned duplicate fixture with normal and mask maps, non-default remaps and both supported smoothness sources. Record those assets/settings, run Bind / Refresh, and repeat. Source geometry and normals must agree; world-XZ projection does not conceal geometric mismatches.

## Comparison tolerance

Run `tools/Compare-RenderCaptures.ps1` on Windows. It reads both images and outputs JSON; it does not modify captures or settings. Every limit must pass:

| Metric, normalized stored RGB bytes | Limit |
|---|---|
| Mean absolute RGB component error | ≤ 0.005 |
| Root-mean-square RGB component error | ≤ 0.01 |
| 99th percentile of per-pixel maximum RGB component error | ≤ 0.025 |
| Fraction of pixels with max RGB error > 0.025 | ≤ 0.01 |

These are display-referred image differences, not physical surface-value tolerances. A metric pass is necessary but not sufficient: visible repeatable seams or layer-boundary defects fail owner review. Do not relax limits after a failure without a documented reason, a protocol revision and a complete rerun. Do not average failing distances/render paths into a pass.

Graph pairs: compare full frames and a predeclared material-interior crop excluding the background. Terrain pairs: compare only the defined unpainted interior crop. Editor/player: compare the same material and fixture state, with the same corresponding crop rules.

```powershell
./tools/Compare-RenderCaptures.ps1 -Reference ./captures/hlsl3-near-forward-editor.png -Candidate ./captures/graph3-near-forward-editor.png
./tools/Compare-RenderCaptures.ps1 -Reference ./captures/terrain-near-forward-editor-source.png -Candidate ./captures/terrain-near-forward-editor-strip.png -X 944 -Y 524 -Width 32 -Height 32
```

Successful execution means the JSON was computed; inspect `passed` before accepting a comparison. The script intentionally reports measured failures as `passed: false` rather than treating them as input errors.

## Evidence and source safety

Use filenames `case-distance-path-environment-state.png`, with cases `graph3`, `graph4`, `terrain-diffuse`, and `terrain-masked`; environment is `editor` or `player`. Retain both raw captures, JSON comparisons, SHA-256 hashes and owner notes. Do not present illustrative or generated images as render evidence.

Hash original mesh, TerrainData, TerrainLayer, texture and serialized paint source files before and after. Record the exact file list; generated fixture assets are distinct from original project sources. Stop on any unexplained source-file change. Automated preservation tests from earlier issues do not prove preservation during this visual session.

No live Frame Debugger, baked-lightmap, steep-surface or performance result is implied by this gate. Record those as separate observations or leave them open.

## Completion

All graph pairs, both terrain cases, every distance, each rendering path and Editor/player comparisons must have attributable owner observations, valid settings, stable controls, unchanged source hashes and passing required metrics. Blank, partial, failed and unsupported rows are not passes. Issue #4 stays open until that record is reviewed. Preparing this kit alone does not complete it.
