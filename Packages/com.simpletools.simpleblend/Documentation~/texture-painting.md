# Texture surface painting

Texture paint is the workspace's default painting method. It writes blend weights into a linear texture instead of interpolating vertex colors. A small brush can paint the middle of an unsubdivided cube face. It brushes materials onto a surface; it does not place image stickers or Unity Decal Projectors.

## First stroke

1. Open **Window > SimpleBlend > Workspace**. Keep **Painting method > Texture paint** and select one active static mesh object.
2. Read the setup notice and choose **Start painting**. No palette or save dialog is required. SimpleBlend creates a uniquely named asset under `Assets/SimpleBlendPaint/`, selects the painted copy and activates the brush. Nothing is created merely by selecting an object or opening the workspace.
3. Setup creates a renderer-only sibling and hides the original renderer. It does not delete the original, move its children, copy scripts/colliders or migrate its vertex paint. The original collider remains where it was. The painted copy starts at Base. Without a palette, common source-material albedo/tint/normal inputs, tiling and basic scalar settings seed Base, with an editable overlay. Custom shader effects and differently packed PBR maps are not translated automatically.
4. Choose a surface and drag in the Scene view. Surface selection, Paint/Erase, brush edits, Fill and presets keep the paint tool active. Changing configuration finishes the current stroke before the next one starts. Project asset selection keeps the bound paint object; selecting a different scene object, multiple objects or clearing selection stops the tool. Alt navigates; Escape stops painting. Focus loss finishes the stroke and releases the mouse, without deactivating the tool.
5. Try a radius of 0.1 m on a default one-metre cube. The ring is the projected outer radius. Falloff controls softness; Strength × Flow controls the maximum influence of a dab. **Capped per stroke** prevents overlapping dabs from repeatedly strengthening one stroke. **Build up along stroke** accumulates them. Existing brush preferences are preserved rather than silently reset.
6. Choose **Erase** to reduce the selected overlay toward Base while preserving competing overlay weights. Erasing Base reduces all active overlays. **Fill section with selected surface** uses Strength × Flow uniformly on the configured section and ignores radius, falloff and the Paint/Erase choice.
7. Undo once to restore a complete stroke; redo restores it. Paint assets save automatically after strokes and surface edits, including Undo/redo. **Save painted asset** remains an immediate save action. Save the scene or prefab yourself to retain its object references; reopen and inspect the result before relying on the saved scene.

**Advanced setup** retains an optional [surface palette](surface-palettes.md), mesh section and 256/512/1024-square mask resolution. Higher resolutions retain smaller details but use more memory, save space and Undo storage. A chosen palette is copied; its original material is not edited.

Paint moves toward the selected surface and reduces competing weights. Base is the remaining weight. These are blended material weights, not an ordered layer stack. New texture paint copies use smooth blending even when their source palette uses height contrast; the original palette material remains unchanged. The copied material's Inspector still exposes Height contrast for explicit edits.

## Edit surfaces

Use **Edit** beside a surface to rename it or change its tint, Albedo, Normal, packed Mask, Height, tiling, offset and material scalars. Clearing a texture field removes that texture assignment. Mask packing is R = metallic, G = occlusion, A = smoothness; texture import settings are not changed automatically. Height textures affect the result only when Height contrast is enabled on the copied material.

**+ Add surface** adds an overlay and selects it without stopping painting. There can be up to four surfaces including Base. **Remove surface** asks for confirmation, then returns that overlay's painted weight to Base, compacts the remaining surfaces and preserves their weights and texture settings. Base cannot be removed but all its inputs can be edited. Removal and addition support complete Undo/redo. Adding a surface clears its newly activated mask channel; structural removal clears unused RGB channels. Alpha is preserved.

All these edits affect the owned material and paint asset, not the source palette. Objects sharing that paint asset also share these changes; the removal prompt explicitly identifies this consequence.

## Brush presets

**Quick preset** offers Soft brush (0.15 m, strength 0.6, capped), Hard brush (0.15 m, strength 1, constant falloff, capped), and Wash (0.5 m, strength 0.15, flow 0.5, build-up). Soft/Hard use flow 1. The selector applies a preset only when chosen; opening the workspace does not reset existing brush preferences.

**Flat brush**, **Chalk** and custom texture tips extend these presets with shape and stroke controls. Opacity/Flow are displayed as percentages. New built-in presets enable an explicit per-stroke opacity limit; existing preferences/custom presets keep legacy mixing unless changed. See [Brush tips and strokes](brush-strokes.md) for hardness, angle, roundness, spacing, smoothing and the diagnostic stroke preview.

**Save preset…** stores radius, opacity/strength, flow, stroke behavior, falloff and the tip/stroke settings as a reusable asset. Choose it in **Saved preset** to apply it, or use **Apply saved preset** to restore it after manual adjustments. Existing preset paths are rejected rather than overwritten. Curves and settings are copied so editing a brush cannot silently modify a saved preset. Presets leave the selected surface, Paint/Erase action and vertex-specific channels/masks unchanged. They do not stop the active texture tool. Custom preset assets can also be created through **Assets > Create > SimpleBlend > Brush Preset**.

## Ownership and saving

Each `TexturePaintAsset` owns a mesh, material and mask as sub-assets in one file. Its serialized `Color32[]` is the authority for Undo and reconstruction. The linear RGBA32 mask has bilinear filtering, clamp addressing and no mipmaps. The RGB channels store overlay weights; unused channels are preserved by brush mixing. No PNG import, compression or color-space conversion occurs during a stroke.

Setup generates a unique atlas on the owned mesh using Unity's unwrap API. It stores paint coordinates in **UV4** (zero-based channel 3), leaving surface-texture UV0, static-lightmap UV2 and dynamic-lightmap UV3 intact through vertex remapping. UV4 on the owned copy is reserved for paint; source UV4 is not changed. Topology can gain seam vertices, but setup does not subdivide faces. The atlas includes two-pixel painted gutters to support bilinear sampling at chart edges.

The original mesh, material palette and vertex-paint data are not edited. Setup's Undo removes the new scene object and restores the original renderer; its saved asset remains for recovery. Existing asset paths are rejected. There is no automatic asset deletion on ordinary Undo, object removal or package removal.

Duplicating a painted object shares its paint asset and therefore its paint. There is no automatic per-instance fork in this path. Do not edit the owned mesh's topology or paint UVs after painting; there is no automatic remap. Painting is blocked when the assigned mesh, material or mask no longer matches the paint target's references. Automatic saving is debounced outside active input and scoped to queued paint assets, not every dirty project asset. Save exceptions are reported in the Console and kept queued for retry. Normal Unity Save Project also saves dirty paint assets. Scene/prefab saving remains explicit.

## Projection and current scope

The brush projects a round footprint along the hit normal. It accepts texels within a quarter-radius of depth and rejects normals with a dot product below 0.25. Positions are evaluated in world space, so nonuniform object scale does not stretch the brush radius. A dab can cross UV chart seams because coverage is tested against surface positions, not a circle in UV space. A sharp change in surface direction may fall outside the depth/normal limits.

Projection is not a full visibility test: nearby, similarly oriented hidden surfaces inside the projection slab may receive paint. Opposing faces are excluded. The first path supports readable static triangle meshes up to 100,000 vertices, one painted material section per object, and Paint/Erase/Fill with shaped/textured brush-tip masks. It does not yet offer texture Smooth/Sample, color-image decal placement, skinned texture painting, arbitrary UV selection, resolution changes after setup, per-instance asset forking, or texture-mode Shader Graph references. Vertex shortcuts, target locking, diagnostic vertex overlays and advanced vertex masks remain specific to **Vertex paint**.

Forward, deferred, depth-normal and meta passes of the hand-written Layer Blend materials share the texture-weight lookup. Existing materials default to vertex weights. Terrain materials and Shader Graph examples keep their previous contracts. This does not establish visual acceptance for every rendering feature or target platform.

## Verification

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-20 passed all 406 editor tests, including 24 texture-paint, 19 authoring-workflow and 23 brush-stroke cases. Checks cover small-brush cube texels, soft falloff, mixing/Base/Erase, capped/build-up strokes, zero flow, world radius, gutters, separated UV charts, section isolation, source preservation, setup Undo, stroke Undo/redo, asset reimport, scene save/reopen and GPU weight lookup. Authoring checks cover automatic setup and collision-safe paths, copied Base inputs, optional palettes, active-tool configuration and selection events, surface edit/removal/remapping Undo, one-to-four surfaces, preset isolation/reload and scoped saving. Brush checks are detailed in [Brush tips and strokes](brush-strokes.md). Native first-use clarity, appearance on varied meshes, chart-edge appearance at distance and performance on production assets still need observed owner review.

Implementation references: [Unity unique UV generation](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Unwrapping.GenerateSecondaryUVSet.html), [texture upload behavior](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Texture2D.Apply.html), [complete-object Undo](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Undo.RegisterCompleteObjectUndo.html).
