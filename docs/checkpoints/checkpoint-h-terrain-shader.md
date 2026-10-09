# Checkpoint H — terrain-matching shader

## Scope

Issue #3 adds `SimpleBlend/Terrain Blend`, dependent on the binder in PR #15. The shader samples a normalized linear blend of one to four bound terrain layers as its base, then applies Red, Green, and Blue mesh-UV overlays. Vertex alpha remains free.

Terrain input decoding, coordinate math, and projected normal conversion are shared by every terrain pass. Lighting, GBuffer packing, shadows, depth, depth normals, and Meta reuse the existing package pass implementation. The old Layer Blend shaders retain their original surface evaluation through an overload; Meta and DepthNormals now carry world position for terrain sampling.

The public binder also copies `TerrainLayer.smoothnessSource` into Surface W, gated by public texture-format alpha information. Existing v1 materials require explicit refresh to populate this previously reserved component. No private terrain material properties or URP editor implementation are accessed.

## Research and interpretation

- [Unity terrain layer documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/class-TerrainLayer.html) identifies mask channels and remapping.
- [Public smoothness-source API](https://docs.unity3d.com/kr/6000.0/ScriptReference/TerrainLayer-smoothnessSource.html) distinguishes constant smoothness from diffuse alpha.
- The installed URP `17.4.0` TerrainLitPasses and TerrainLitShaderGUI sources were inspected for texel-center control sampling, mask defaults, diffuse tint scale, and normal orientation. These sources informed behavior; no terrain shader source is copied into this package.

Supported terrain blending is linear. URP terrain height blending, opacity-as-density, custom terrain shaders, holes, vegetation, and automatic runtime rebinding are excluded. Terrain geometric normals are not copied: matching seam geometry remains a requirement. Terrain Base Height Influence defaults to zero; mask height can be enabled deliberately for overlay competition.

## Observed checks

Baseline: Unity `6000.4.8f1`, URP `17.4.0`, Shader Graph `17.4.0`, Windows D3D11. Tests run in a hidden batch process without `-nographics`.

- Complete package suite: **150 passed, 0 failed, 0 skipped**.
- Thirty-one terrain shader/fixture cases, including actual GPU reads from the production surface and math includes.
- Three additional binder cases verify constant/diffuse-alpha smoothness selection and absent diffuse maps.
- Probes cover translated origins, non-square sizes, positive/negative tiling, offsets, endpoint texel-center sampling, clamped control weights, normalization, unused layers, and zero-control fallback.
- All four terrain channels contribute diffuse and mask values. Tests cover mask remaps, no-mask constants, AO/height fallback, smoothness-source selection, and mask precedence.
- Actual surface probes cover clear/extreme vertex colors, active overlay height competition, high unpainted overlay rejection, unused alpha, and normal-map orientation under mesh tangent rotation.
- All six required public terrain passes compile. Existing painting, binding, and Layer Blend tests pass in the same suite.

Local XML/log evidence: `TestResults/TerrainShader-accepted.xml` and `Logs/TerrainShader-accepted.log`.

## Reference fixture

`TerrainBlendReferenceFixture.Create(assetRoot)` accepts an existing empty asset folder and writes procedural terrain layers, TerrainData, a bound mesh material, a dense RGB strip spanning the terrain edge, a light, a camera, and a scene. Its caller owns generated assets. Untitled open scenes cause an actionable rejection before any scene or asset writes. Named scenes remain open; only the fixture scene is closed after saving. New objects are created in the fixture's active scene and only generated assets are saved. The EditMode fixture check observed the untitled rejection path; successful creation was observed during the player build.

The isolated player-build entry point creates a GUID-owned folder, a clean empty batch bootstrap scene if needed, the reference scene, and a retained Resources material. It removes only its generated asset folder in `finally`; the build output remains under `Builds/SimpleBlendTerrain`.

Run it in a dedicated Unity batch process with `-executeMethod SimpleBlend.Editor.Tests.TerrainBlendPlayerBuildFixture.Build`. Never run it against a live editor session.

Observed Windows x64/D3D11 build: succeeded, exit code 0, with a bound terrain material retained in Resources. The log records compilation of `SimpleBlend/Terrain Blend` and its shared terrain passes. The reference scene and its asset references built successfully. Local evidence: `Logs/TerrainShader-player-final.log`; executable: `Builds/SimpleBlendTerrain/SimpleBlend.exe`. Temporary fixture assets were cleaned up. The player was built, not launched for an appearance check.

## Remaining gates

Graph/terrain seam appearance and editor/player capture comparison remain issue #4. Forward/deferred visual parity, Frame Debugger batching, lightmap output, steep-surface projection quality, and performance are not established by isolated surface probes. Unity 6.3 LTS and 6.6 remain untested.
