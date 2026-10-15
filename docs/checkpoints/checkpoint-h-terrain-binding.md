# Checkpoint H — public terrain binder

## Implemented scope

Issue #2 adds an editor-only Terrain Binding window and a public-API binder. The binder validates the entire input before modifying a destination material. It copies control texture 0, terrain origin and size, and the first one to four layers' textures, tiling, offsets, surface scalars, and remaps.

The material property layout is versioned and documented in `docs/terrain-contract.md`. Three hidden test shaders cover valid properties, a wrong version, and a wrong property type. They are property fixtures, not production terrain renderers.

## Observed verification

Baseline: Unity `6000.4.8f1`, URP `17.4.0`, Shader Graph `17.4.0` on Windows. Tests ran in a hidden batch process with `-nographics`; the desktop was not driven.

- Complete SimpleBlend EditMode suite: **116 passed, 0 failed, 0 skipped**.
- Included terrain coverage: 32 binder cases and two public terrain contract cases.
- XML: `TestResults/TerrainBinding-verified.xml`; log: `Logs/TerrainBinding-verified.log` (local generated evidence, not package assets).
- All supported layer counts copy the expected values. Missing optional maps receive neutral defaults and presence flags; shrinking the layer count clears unused slots.
- Invalid sources and incompatible shaders leave the material unchanged. Validation does not dirty it or insert an Undo operation.
- Undo/Redo restores the complete material state. Saving and reimporting retains control texture and layer references.
- Changing alphamap pixels remains live through the same control texture reference. Layer replacement and parameter or position changes update only after explicit refresh.
- Source TerrainData, TerrainLayer, texture, and mesh asset bytes remain identical after binding and saving. Terrain/transform and painted data serialization also remain unchanged.
- Read-only standalone materials, embedded material subassets, unsaved TerrainData, and unsaved assigned textures are rejected.

The read-only regression exposed that `AssetDatabase.IsOpenForEdit` alone does not reject the filesystem read-only attribute on this baseline. The binder now checks both version-control editability and the destination file attribute before any write.

## Remaining gates

- Production terrain shader: issue #3. Existing Layer Blend shaders are intentionally rejected by the binder because they do not implement the terrain contract.
- Terrain/Shader Graph rendering and appearance parity: issue #4.
- Interactive window usability, lightmapping, batching, and editor/player image parity were not observed in this batch run.
- Unity 6.3 LTS and 6.6 support remain unverified until their dedicated test matrix runs.

No runtime or painting implementation changed in this slice. No new player build was run for this editor-only binder; player and shader verification remain explicit subsequent gates.
