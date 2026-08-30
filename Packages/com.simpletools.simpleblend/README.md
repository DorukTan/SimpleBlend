# SimpleBlend

SimpleBlend is an open-source Unity editor toolkit for painting vertex colors on meshes in the Scene view and blending URP material layers from those colors.

## Current status

Version `0.1.0-dev.2` is the durable-data checkpoint. It contains versioned per-renderer color data, deterministic topology validation, transient Additional Vertex Streams, explicit cloned-mesh fallback assets, and EditMode safety tests. Scene view painting and blend shaders are not implemented yet.

## Compatibility

- Tested development baseline: Unity `6000.4.8f1` with Universal Render Pipeline `17.4.0`.
- Compatibility targets awaiting validation: Unity `6000.3` LTS and Unity `6000.5`.
- Render pipeline: URP only.

No compatibility claim is made for an untested Unity or URP version.

## Installation

During development, the package is embedded at `Packages/com.simpletools.simpleblend/` in this Unity project. Git URL and registry installation instructions will be added only after a canonical public repository exists.

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
