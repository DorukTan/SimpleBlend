# Terrain material binding

Open **Window > SimpleBlend > Terrain Binding**, select the source Terrain and a destination material, then choose **Bind / Refresh**. Save the material through Unity to persist the change. Binding is one undoable operation and changes only the destination material.

Create a material using **SimpleBlend/Terrain Blend** and assign it to the mesh. The destination implements terrain binding contract v1. Existing Layer Blend 3 and Layer Blend 4 shaders do not implement that contract. Hidden test fixture shaders are not rendering solutions.

Use a separate material for each source terrain. Persistent destinations must be editable, standalone `.mat` files under `Assets`, and their TerrainData and assigned textures must be saved assets. Duplicate package materials or extract imported material subassets first.

## Refresh behavior

Alphamap painting updates the bound control texture's pixels without rebinding. Run Bind / Refresh again after changing terrain position or size, replacing TerrainData or its control texture, or changing layer assignments, textures, tile sizes, offsets, surface values, or remaps. There is no automatic runtime refresh.

## Supported inputs

- One Terrain with one to four assigned, non-null Terrain Layers.
- Zero world rotation and unit world scale, including parent transforms.
- Finite values and nonzero tile-size components.
- Optional diffuse, normal, and mask textures. Missing maps use neutral textures and explicit presence flags.

Binding rejects invalid inputs before changing any material property. Rebinding fewer layers clears unused slots. It preserves unrelated material properties and does not change the Terrain, TerrainData, Terrain Layers, meshes, or vertex paint data.

The shader samples the bound terrain as the unpainted base. Red, Green, and Blue paint select mesh-UV overlays with the established height blending; alpha remains unused. Configure overlay textures and multipliers in the material Inspector. Terrain Base Height Influence defaults to zero; raise it only when terrain mask height should compete with overlay heights. Bind again after changing the layer smoothness source; older bound materials also need refresh to populate that setting.

Terrain layers use normalized linear control blending. Use URP Terrain Lit without terrain height blending or opacity-as-density for the supported reference case. Custom terrain shading, holes, vegetation, additional control maps, and arbitrary terrain shader effects are outside this contract. Matching geometry and normals are required at seams; world-XZ texturing alone does not hide a geometric mismatch. Control weights clamp outside the terrain footprint, while layer textures repeat.

Meshes need valid normals and tangents for normal mapping. World-XZ projection is intended for ground-facing surfaces; vertical faces can stretch textures and are not triplanar mapped. Four terrain layers plus three overlays are texture-heavy: profile the target hardware before using this material widely.

Visual terrain matching still requires the rendering gate in issue #4; successful binding and compilation alone do not establish appearance parity.
