# SimpleBlend

SimpleBlend is an open-source Unity editor toolkit for painting vertex colors on meshes in the Scene view and blending URP material layers from those colors.

## Current status

Version `0.3.0-alpha.2` adds independent R/G/B/A/RGB previews, a transform-aware vertex-density heatmap, and a deterministic 40k-vertex performance gate to the complete brush and storage foundation. The managed drag path passes its timing and zero-allocation target, so SimpleBlend has no mandatory Burst dependency. Blend shaders remain a later milestone.

## Compatibility

- Tested development baseline: Unity `6000.4.8f1` with Universal Render Pipeline `17.4.0`.
- Compatibility targets awaiting validation: Unity `6000.3` LTS and Unity `6000.5`.
- Render pipeline: URP only.

No compatibility claim is made for an untested Unity or URP version.

## Installation

During development, the package is embedded at `Packages/com.simpletools.simpleblend/` in this Unity project. Git URL and registry installation instructions will be added only after a canonical public repository exists.

## First stroke

Select a GameObject with a `MeshFilter`/`MeshRenderer` or a `SkinnedMeshRenderer`, then choose **GameObject > SimpleBlend > Enable Vertex Painting** (also available from the Renderer component context menu). The command adds and initializes `VertexPaintData` as one Undo operation and activates the paint tool. Choose a channel, mode, and preview in the **SimpleBlend Brush** Scene view overlay, then drag or click the mesh. A collider is not required.

See `Documentation~/first-stroke.md` for setup, `Documentation~/brush-controls.md` for exact brush semantics, and `Documentation~/data-safety.md` for prefab and storage behavior.

## Development verification

Run the package EditMode tests from PowerShell with the installed development baseline:

```powershell
New-Item -ItemType Directory -Force `
  -Path 'D:\Projects\SimpleBlend\TestResults', 'D:\Projects\SimpleBlend\Logs' | Out-Null

& 'C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'D:\Projects\SimpleBlend' `
  -runTests -testPlatform EditMode `
  -testFilter 'SimpleBlend.Editor.Tests' `
  -testResults 'D:\Projects\SimpleBlend\TestResults\SimpleBlend-EditMode.xml' `
  -logFile 'D:\Projects\SimpleBlend\Logs\SimpleBlend-EditMode.log'
```

The Unity Test Framework exits the process when the run completes. Do not add `-quit`; on the tested editor it can stop the initial import before test dispatch.

## License

SimpleBlend is available under the MIT License. See `LICENSE`.
