# Integrated development checkout and mesh preparation

## Scope

Issue #21 combines the terrain/sample/guide branch (`c1e93fa`) and Shader Graph branch (`1985f23`) with the public-repository setup. Remote pull requests remain separate review items; local integration does not establish release acceptance.

The paint Inspector and brush overlay explain sparse meshes. A mesh with at most 64 vertices receives a heuristic warning; larger meshes can still contain coarse regions. Density preview remains the spacing inspection tool.

## Subdivided copy contract

- Explicit Inspector action after paint initialization; no automatic source replacement.
- Readable static triangle meshes only; no skinning, blend shapes, one-component UV channels or direct prefab-asset targets.
- One to four midpoint subdivision levels; at most 200,000 output triangles. Large allowed outputs use 32-bit indices.
- Indexed edge sharing preserves hard edges and UV seams. Submeshes and winding remain intact.
- Positions, paint and UV components interpolate linearly; interpolated normals and tangent directions normalize. The operation adds detail without smoothing the shape.
- Original renderer, geometry and serialized paint remain unchanged. The new source mesh contains paint interpolated at creation; subsequent strokes use the normal per-renderer storage policy.
- Renderer-only scene copy at the original transform. No copied colliders, children or unrelated scripts. Move or hide overlapping objects before judging appearance.
- New `.asset` destination only; existing assets cannot be overwritten. Scene Undo removes the new object while retaining the saved mesh for recovery. Failed scene setup can likewise retain the saved mesh.

The implementation uses Unity's public [Mesh API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh), not importer mutation or private mesh APIs.

## Verification

Verification is performed in an isolated Windows project with Unity `6000.4.8f1`, URP/Shader Graph `17.4.0` and D3D11. The development scene, render-pipeline assets and project settings are not test fixtures.

The final integrated EditMode run passed **177/177**, with zero failures and zero skips. This includes ten mesh-preparation cases: cube face-interior geometry and a real localized stroke that leaves corners clear; interpolated paint/UVs and shared midpoint indices; submesh/winding preservation; two invalid-level cases; unsupported topology/readability/paint rejection; triangle-budget rejection; skinning/blend-shape rejection; 32-bit indices for 66,049 vertices; and saved copy/prefab data with source preservation, overwrite refusal and Undo/Redo.

Result artifacts are local under `TestResults/PaintIntegration-final-results.xml` and `TestResults/PaintIntegration-final.log`. The earlier integration run also passed 175/175 before the strengthened cases were added. Staged whitespace checks passed.

Interactive Inspector layout, live visual appearance and the owner first-use walkthrough remain separate acceptance work.
