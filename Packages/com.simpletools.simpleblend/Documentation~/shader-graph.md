# Shader Graph integration

SimpleBlend includes three subgraphs under `ShaderGraph/Subgraphs`:

- **SimpleBlend Height Weights 3** accepts three raw weights, three sampled heights, and a transition value.
- **SimpleBlend Height Weights 4** accepts four raw weights, four sampled heights, and a transition value.
- **SimpleBlend Surface Blend 4** applies one four-component weight vector to four albedos, tangent-space normals, and packed masks.

The height subgraphs use the same functions as the hand-written Layer Blend shaders. Zero-weight layers cannot win from height alone, and the result is normalized with a base-layer fallback.

## Three-layer graph

Use `saturate(1 - vertexColor.r - vertexColor.g)` for the base raw weight, followed by Red and Green. Sample each layer's height before calling **SimpleBlend Height Weights 3**.

To use **SimpleBlend Surface Blend 4**, append zero as the fourth weight. Supply a neutral fourth normal `(0, 0, 1)`; its other fourth-layer inputs do not contribute.

## Four-layer graph

Use `saturate(1 - vertexColor.r - vertexColor.g - vertexColor.b)` for the base raw weight, followed by Red, Green, and Blue. Pass the result and four sampled heights to **SimpleBlend Height Weights 4**.

## Surface inputs

Normals are tangent-space vectors. Packed masks use R metallic, G ambient occlusion, B reserved or height, and A smoothness. Apply per-layer metallic, occlusion, and smoothness controls before passing the masks to the surface subgraph.

Connect the returned Albedo, Normal, and Mask components to the corresponding Lit target inputs. Vertex alpha remains available for project-specific use.

## Reference graphs and materials

`ShaderGraph/Examples/LayerBlend3Parity.shadergraph` and `LayerBlend4Parity.shadergraph` show complete URP Lit connections. Each graph samples the layer textures, obtains height weights from its matching height subgraph, and sends those weights to **SimpleBlend Surface Blend 4**. The three-layer graph explicitly appends a zero fourth weight.

The graphs use the same `_LayerNAlbedo`, `_LayerNNormal`, `_LayerNMask`, `_LayerNHeight`, tint, and surface-control property names as the hand-written shaders. Each Albedo texture's tiling and offset applies to that layer's entire texture set. Data textures use linear sampling and normal inputs follow the normal-map packing convention.

`ShaderGraph/Examples/Materials` contains paired `Graph3`/`Hlsl3` and `Graph4`/`Hlsl4` materials. Each pair references the same original procedural textures and the same controls, including tiling and offsets. Assign a pair to duplicate painted meshes to compare their appearance under identical lighting and camera settings. These are small reference fixtures; the teaching samples are separate release work.

All reference texture content is original and distributed under the package MIT license. The editor-only `ShaderGraphReferenceFixture.CreateAssets` method reproduces the texture/material fixtures; it writes only the named reference assets and should be run intentionally when authoring them, not as part of the test suite.

Import, pass-compilation, material-input, and GPU weight/surface checks cover this integration. GPU probes require a graphics device and are explicitly skipped under `-nographics`. Live lighting and reference-image acceptance remain a separate review gate.
