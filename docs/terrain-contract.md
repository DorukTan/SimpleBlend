# Terrain matching contract

Status: editor binder and production terrain shader implemented in separate M8 pull requests; visual verification remains open.

## Supported boundary

The first SimpleBlend terrain-matching shader supports one Unity Terrain and its first four `TerrainLayer` assets. It reads layer weights from control texture 0 and projects the corresponding diffuse, normal, and mask textures from world position.

The binder uses only these public values:

- `Terrain.terrainData` and the Terrain transform position;
- `TerrainData.size`, `terrainLayers`, and `GetAlphamapTexture(0)`;
- `TerrainLayer.diffuseTexture`, `normalMapTexture`, `maskMapTexture`, `tileSize`, `tileOffset`, `normalScale`, `metallic`, `smoothness`, and remap values.

No terrain material properties, URP editor classes, generated shader code, or private fields are read.

## Layer limit

Four terrain layers are the complete v1 contract. Unity stores layers 4–7 in a second control texture and renders them through an additional terrain pass. A terrain with more than four assigned layers is rejected by the binder instead of producing a partial visual match.

This limit is deliberate. Supporting additional control textures would multiply texture inputs and shader variants while still failing to reproduce arbitrary custom terrain materials.

## Coordinate convention

Control UV is `(worldXZ - terrainOriginXZ) / terrainSizeXZ`. Each layer UV is `(worldXZ - terrainOriginXZ + tileOffset) / tileSize`. Zero tile-size components are invalid.

The terrain-matching shader treats the sampled terrain surface as its base layer. SimpleBlend Red, Green, and Blue overlays retain the existing M7 meaning. Vertex alpha remains unconsumed.

## Update behavior

The control texture reference remains live when alphamap pixels change. Replacing Terrain Layer assets or changing their textures, tiling, offsets, or scalar values requires running the binder again so the material receives the new public values.

Binding changes only the destination material. It does not modify the Terrain, TerrainData, Terrain Layer assets, source meshes, or painted `Color32[]` data.

## Unsupported terrain features

- Five or more assigned Terrain Layers.
- Custom terrain shaders with different blending rules, terrain height blending, and opacity-as-density modes.
- Terrain holes, decals, detail meshes, and vegetation.
- Triplanar projection or stochastic terrain texturing.
- Automatic runtime rebinding.

The generated-basemap fallback remains available as a future alternative if a supported Unity version breaks the public live-binding path. It is not part of the current implementation.

## Material property contract v1

The shader declares `_SimpleBlendTerrainBindingVersion` as a Float or Range with a shader default of `1`. Validation reads shader metadata rather than a material override. All textures below must be 2D shader properties.

| Property | Type | Value |
|---|---|---|
| `_TerrainControl` | Texture | Live control texture 0 |
| `_TerrainOrigin` | Vector | World position XYZ, W = 0 |
| `_TerrainSize` | Vector | TerrainData size XYZ, W = 0 |
| `_TerrainLayerCount` | Float or Range | Assigned layer count, 1–4 |

For each index 0–3, `_TerrainLayer{index}` has these suffixes:

| Suffix | Type | Value |
|---|---|---|
| `Diffuse`, `Normal`, `Mask` | Texture | Public layer textures, or neutral defaults |
| `TileSizeOffset` | Vector | Tile size XY, tile offset XY |
| `Surface` | Vector | Normal scale, metallic, smoothness, diffuse-alpha smoothness flag |
| `Flags` | Vector | Diffuse present, normal present, mask present, 0 |
| `DiffuseRemapMin`, `DiffuseRemapMax` | Vector | Unmodified public diffuse remaps |
| `MaskRemapMin`, `MaskRemapMax` | Vector | Unmodified public mask remaps |

Missing diffuse and mask maps use white; missing normals use the built-in normal texture. Unused slots have zero flags, unit tile size, zero offset, surface `(1,0,0,0)`, zero remap minima, and unit remap maxima. The shader honors presence flags rather than inferring map presence from fallback textures. Surface W is 1 only when the public smoothness source selects diffuse alpha and the diffuse texture format has an alpha channel; otherwise it is 0. Rebind older materials to populate this flag.

## Shader sampling semantics

`SimpleBlend/Terrain Blend` uses normalized linear control weights across the assigned layers. The world-space control UV remains the convention above; sampling maps its endpoints to texel centers using `(saturate(uv) * (dimensions - 1) + 0.5) / dimensions`. Outside the terrain footprint, control weights clamp while layer textures repeat.

Diffuse RGB is multiplied by the public remap scale `max.rgb - min.rgb`, consistent with the installed URP Terrain Lit path (its inspector normally sets RGB minima to zero). Mask channels are remapped as `sample * (max - min) + min`. With a mask present, metallic, AO, smoothness, and base height come from the remapped R, G, A, and B channels. Without a mask, metallic and smoothness use public layer constants (or explicit diffuse-alpha smoothness), while AO and height use mask-remap maxima G and B.

Normals use world-XZ projection relative to the mesh geometric normal, then convert into the mesh tangent frame before mixing with mesh-UV overlays. A flat surface uses +world X for U and +world Z for V. The terrain geometric heightfield normal is not copied: a seam requires matching mesh geometry and normals as well as material inputs.

The sampled terrain is one base surface. Its mask-derived height, scaled by Terrain Base Height Influence, participates in SimpleBlend's existing height-weight function against Red, Green, and Blue overlay height textures; zero-painted overlays cannot become active through height alone. Base height influence defaults to zero so the terrain's no-mask height fallback does not suppress normal overlay painting. Enable it deliberately when terrain mask height should compete with overlay height. This does not reproduce URP terrain height blending between terrain layers. Forward, GBuffer, ShadowCaster, DepthOnly, DepthNormals, and Meta use the same input contract; Meta receives original world position before lightmap-space projection.

## Validation and persistence

The binder validates the complete source and destination before registering Undo or writing properties. It rejects missing or incompatible properties, unsupported layer counts, null layer slots, non-finite values, zero tile sizes, rotated or scaled terrains, and missing control textures. Persistent destinations must be writable standalone `.mat` assets in `Assets`, with saved TerrainData, control texture, and assigned layer textures. Package assets and embedded materials must be duplicated or extracted first.

Only the destination material is dirtied. Binding does not save assets automatically. One Undo restores all changed material properties; failed binding leaves the material and undo history unchanged. Terrain position/size changes and control texture replacement require refresh, as do layer parameter changes. Pixel edits to the same control texture remain live.
