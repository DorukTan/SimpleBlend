# Shader Graph implementation checkpoint

Issue: [#1](https://github.com/DorukTan/SimpleBlend/issues/1)

Verified baseline: Unity `6000.4.8f1`, URP `17.4.0`, Shader Graph `17.4.0`, Windows x64, D3D11. Package version remains `0.4.0-beta.1` with these changes recorded under Unreleased.

## Delivered behavior

Three reusable subgraphs share the height and surface functions with the hand-written shaders. Complete three- and four-layer URP Lit reference graphs sample matching texture sets and route the same final weights into albedo, tangent-space normals, metallic, ambient occlusion, and smoothness. The three-layer graph explicitly adds a zero fourth weight; vertex alpha remains free.

Four paired materials use ten original procedural texture assets, identical surface controls, and identical per-layer tiling/offset. These fixtures can be reproduced through the explicit editor authoring method and are distributed under the package MIT license.

Fresh import found malformed Heights property-node slot references in both existing height subgraphs. Those references now point to material slots. Import tests force reimport, check public importer error logs, and validate the saved slot types.

The old three-layer CPU reference included the unused fourth height when finding the maximum. The reference now excludes it; GPU probes test both precision entry points against that corrected calculation.

## Observed verification

| Check | Result |
| --- | --- |
| Focused graph and hand-written shader checks | 23 passed, 0 failed, 0 skipped |
| Complete package EditMode suite | 96 passed, 0 failed, 0 skipped; 4.521 seconds |
| Windows x64 player build | Succeeded; both graph reference shaders and both hand-written shaders retained |
| Native graph connection validation | All references and edge slots resolved |

The graphics-device tests ran in hidden batch mode with D3D11, without `-nographics`. They exercise zero weights, base/extreme weights, ties, negative inputs, clamped heights, transition limits, aligned surface outputs, and normal normalization. Compilation tests temporarily select URP's tagged subshader before a batch render loop exists, then restore the previous setting.

Result files remain local and outside the package:

- `TestResults/ShaderGraph-focused-final.xml`
- `TestResults/SimpleBlend-issue1.xml`
- `Logs/ShaderGraph-focused-final.log`
- `Logs/SimpleBlend-issue1.log`
- `Logs/ShaderGraph-player-build.log`

Full-suite XML SHA-256: `836497180605BEE231216AA37AB9ADEECE84C502B2C61450CEE18DA2F79B2588`.

## Reproduce the checks

From the development project, run Unity batch mode with `-runTests -testPlatform EditMode -testFilter SimpleBlend.Editor.Tests -force-d3d11`, supplying project, result, and log paths. Omit `-quit` for the test invocation so Test Framework can complete its run.

For retained player compilation, run batch mode with `-quit -force-d3d11 -executeMethod SimpleBlend.Editor.Tests.ShaderGraphPlayerBuildFixture.Build`. The fixture creates temporary Resources materials in its own folder and removes that folder after the build.

The intentional reference asset authoring method is `SimpleBlend.Editor.Tests.ShaderGraphReferenceFixture.CreateAssets`. It is not called by tests.

## Remaining acceptance

Live lighting, graph/HLSL reference-image parity, Frame Debugger batching, and baked output remain unobserved visual checks. This checkpoint does not close issue #4 or acceptance test 7. Terrain binding and its shader remain issues #2 and #3.

Unity 6.3 LTS and 6.6 target-version validation remains separate work. No compatibility claim for those versions follows from this baseline run. Source Scene and Project Settings changes from the original checkout are not included in the implementation PR.
