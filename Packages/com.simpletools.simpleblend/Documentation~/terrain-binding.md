# Terrain material binding

Open **Window > SimpleBlend > Terrain Binding**, select the source Terrain and a destination material, then choose **Bind / Refresh**. Save the material through Unity to persist the change. Binding is one undoable operation and changes only the destination material.

The destination shader must implement terrain binding contract v1. The existing Layer Blend 3 and Layer Blend 4 shaders do not implement that contract. The production terrain-matching shader is tracked separately in [issue #3](https://github.com/DorukTan/SimpleBlend/issues/3); the test fixture shader is not a rendering solution.

Use a separate material for each source terrain. Persistent destinations must be editable, standalone `.mat` files under `Assets`, and their TerrainData and assigned textures must be saved assets. Duplicate package materials or extract imported material subassets first.

## Refresh behavior

Alphamap painting updates the bound control texture's pixels without rebinding. Run Bind / Refresh again after changing terrain position or size, replacing TerrainData or its control texture, or changing layer assignments, textures, tile sizes, offsets, surface values, or remaps. There is no automatic runtime refresh.

## Supported inputs

- One Terrain with one to four assigned, non-null Terrain Layers.
- Zero world rotation and unit world scale, including parent transforms.
- Finite values and nonzero tile-size components.
- Optional diffuse, normal, and mask textures. Missing maps use neutral textures and explicit presence flags.

Binding rejects invalid inputs before changing any material property. Rebinding fewer layers clears unused slots. It preserves unrelated material properties and does not change the Terrain, TerrainData, Terrain Layers, meshes, or vertex paint data.

Custom terrain shading, holes, vegetation, additional control maps, and arbitrary terrain shader effects are outside this contract. Visual terrain matching still requires shader and rendering verification; successful binding alone does not establish appearance parity.
