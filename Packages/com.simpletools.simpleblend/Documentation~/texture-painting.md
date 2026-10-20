# Texture surface painting

Texture paint is the workspace's default painting method. It writes blend weights into a linear texture instead of interpolating vertex colors. A small brush can paint the middle of an unsubdivided cube face. It brushes materials onto a surface; it does not place image stickers or Unity Decal Projectors.

## First stroke

1. Open **Window > SimpleBlend > Workspace**. Keep **Painting method > Texture paint** and select one active static mesh object.
2. Create or choose a [surface palette](surface-palettes.md). Choose the mesh section to paint and a mask resolution: 256, 512 or 1024 square. Higher resolutions retain smaller details but use more memory, save space and Undo storage.
3. Read the setup notice, then choose **Create texture paint copy** and a new `.asset` path inside Assets. Setup creates a renderer-only sibling and hides the original renderer. It does not delete the original, move its children, copy scripts/colliders or migrate its vertex paint. The original collider remains where it was. The painted copy starts at Base.
4. Select a surface, choose **Paint**, then **Start painting**. Drag in the Scene view. Alt navigates; Escape stops the tool. Tool changes, selection changes, Play mode and loss of the input view finish an active stroke and release the mouse.
5. Try a radius of 0.1 m on a default one-metre cube. The ring is the projected outer radius. Falloff controls softness; Strength × Flow controls the maximum influence of a dab. **Capped per stroke** prevents overlapping dabs from repeatedly strengthening one stroke. **Build up along stroke** accumulates them. Existing brush preferences are preserved rather than silently reset.
6. Choose **Erase** to reduce the selected overlay toward Base while preserving competing overlay weights. Erasing Base reduces all active overlays. **Fill section with selected surface** uses Strength × Flow uniformly on the configured section and ignores radius, falloff and the Paint/Erase choice.
7. Undo once to restore a complete stroke; redo restores it. **Save painted asset**, then save the scene or prefab. Reopen and inspect the result before relying on the saved scene.

Paint moves toward the selected surface and reduces competing weights. Base is the remaining weight. These are blended material weights, not an ordered layer stack. New texture paint copies use smooth blending even when their source palette uses height contrast; the original palette material remains unchanged. The copied material's Inspector still exposes Height contrast for explicit edits.

## Ownership and saving

Each `TexturePaintAsset` owns a mesh, material and mask as sub-assets in one file. Its serialized `Color32[]` is the authority for Undo and reconstruction. The linear RGBA32 mask has bilinear filtering, clamp addressing and no mipmaps. The RGB channels store overlay weights; unused channels are preserved by brush mixing. No PNG import, compression or color-space conversion occurs during a stroke.

Setup generates a unique atlas on the owned mesh using Unity's unwrap API. It stores paint coordinates in **UV4** (zero-based channel 3), leaving surface-texture UV0, static-lightmap UV2 and dynamic-lightmap UV3 intact through vertex remapping. UV4 on the owned copy is reserved for paint; source UV4 is not changed. Topology can gain seam vertices, but setup does not subdivide faces. The atlas includes two-pixel painted gutters to support bilinear sampling at chart edges.

The original mesh, material palette and vertex-paint data are not edited. Setup's Undo removes the new scene object and restores the original renderer; its saved asset remains for recovery. Existing asset paths are rejected. There is no automatic asset deletion on ordinary Undo, object removal or package removal.

Duplicating a painted object shares its paint asset and therefore its paint. There is no automatic per-instance fork in this path. Do not edit the owned mesh's topology or paint UVs after painting; there is no automatic remap. Painting is blocked when the assigned mesh, material or mask no longer matches the paint target's references. Save the asset and scene separately when in doubt. Normal Unity Save Project also saves dirty paint assets.

## Projection and current scope

The brush projects a round footprint along the hit normal. It accepts texels within a quarter-radius of depth and rejects normals with a dot product below 0.25. Positions are evaluated in world space, so nonuniform object scale does not stretch the brush radius. A dab can cross UV chart seams because coverage is tested against surface positions, not a circle in UV space. A sharp change in surface direction may fall outside the depth/normal limits.

Projection is not a full visibility test: nearby, similarly oriented hidden surfaces inside the projection slab may receive paint. Opposing faces are excluded. The first path supports readable static triangle meshes up to 100,000 vertices, one painted material section per object, and Paint/Erase/Fill. It does not yet offer texture Smooth/Sample, image stamps, skinned texture painting, arbitrary UV selection, resolution changes after setup, per-instance asset forking, or texture-mode Shader Graph references. Vertex shortcuts, target locking, diagnostic vertex overlays and advanced vertex masks remain specific to **Vertex paint**.

Forward, deferred, depth-normal and meta passes of the hand-written Layer Blend materials share the texture-weight lookup. Existing materials default to vertex weights. Terrain materials and Shader Graph examples keep their previous contracts. This does not establish visual acceptance for every rendering feature or target platform.

## Verification

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-20 passed all 364 editor tests, including 24 texture-paint cases. Checks cover small-brush cube texels, soft falloff, replacement/Base/Erase, capped/build-up strokes, zero flow, world radius, gutters, separated UV charts, section isolation, source preservation, setup Undo, stroke Undo/redo, asset reimport, scene save/reopen and GPU weight lookup. Native first-use clarity, appearance on varied meshes, chart-edge appearance at distance and performance on production assets still need observed owner review.

Implementation references: [Unity unique UV generation](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Unwrapping.GenerateSecondaryUVSet.html), [texture upload behavior](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Texture2D.Apply.html), [complete-object Undo](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Undo.RegisterCompleteObjectUndo.html).
