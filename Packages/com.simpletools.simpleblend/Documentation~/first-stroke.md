# Paint the first stroke

SimpleBlend paints per-renderer vertex colors and provides channel previews, storage/prefab handling and three/four-layer URP height blending. Check the installed revision and [compatibility scope](compatibility.md) before following preview-specific instructions.

## Set up a target

For the dockable setup and painting controls, open **Window > SimpleBlend > Workspace**. Select one object and choose **Set up painting**. For the guided material path, use **Named surfaces** to create a palette, explicitly assign its material and choose a surface before **Start painting**. Use **Direct channels** for the manual/custom-shader path below. See [Paint workspace](workspace.md) and [surface palettes](surface-palettes.md). The component/menu path remains available.

For an owned first lesson, create a Unity Plane and assign a new Layer Blend 3 material. Give the Base a neutral tint and the Red overlay a contrasting tint; default white Albedo and black Height inputs are sufficient for a basic channel test. Use a URP asset in both Graphics and the active Quality level. Imported meshes with existing vertex colors initialize from those colors; meshes without colors initialize with zero channels.

1. Select a GameObject with a `MeshFilter`/`MeshRenderer` or a `SkinnedMeshRenderer`.
2. Choose **GameObject > SimpleBlend > Enable Vertex Painting**, or use **Enable SimpleBlend Vertex Painting** from the Renderer component context menu.
3. Confirm that the Inspector reports ready paint data. Open **Source and saved-data details** when you need vertex/submesh counts, the topology hash, storage mode/reason or generated output path/ownership ID.
4. If the component was added manually, select **Initialize Vertex Painting** in its Inspector.
5. Select **Activate SimpleBlend Paint Tool** if setup did not already activate it. The tool appears in the Scene view palette with the custom brush icon (`SB` is the missing-icon fallback).

## Painting a cube or another sparse mesh

Vertex painting colors existing vertices, not texture pixels. A default cube has corner vertices but no face-interior vertices, so small strokes on the face center cannot produce localized detail. The Inspector and brush overlay flag meshes with 64 or fewer vertices as a simple sparse-mesh heuristic; this is not a spacing measurement. Larger meshes can still have sparse areas. Use **Density** preview to inspect spacing.

After initializing paint, open **Mesh detail** and use **Create Subdivided Paintable Copy** in the Inspector. Start with three subdivision levels for a default cube and save a new `.asset` under Assets. This creates a renderer-only copy at the same position with interpolated RGBA paint, UVs, normals and tangents. Move or hide one object to avoid overlapping surfaces, then paint the new copy. The original mesh, original paint and original object remain unchanged. Subsequent strokes remain per-renderer paint data; the saved source copy contains the colors interpolated at creation time.

Subdivision splits triangles without smoothing the shape. It supports readable static triangle meshes without skinning or blend shapes, preserves submesh boundaries and indexed seams, and rejects results above 200,000 triangles. It does not copy children, colliders or other scripts. Undo removes the new object; the saved mesh asset remains for recovery and can be removed manually when no longer referenced. Cancelling the save dialog creates nothing. Existing assets are never overwritten.

## Paint

- Use the workspace or **SimpleBlend Brush** Scene view overlay to choose Operation, Paint channel, Channel values, View, Brush size (radius), Strength and Fill scope. Flow, Falloff, Accumulate and optional masks are under **More paint options**; hidden settings still apply.
- Start with Paint, Red channel, a brush color whose Red component is 1, strength 0.35, flow 1, accumulation on, and masks off. Brush color components are channel values, not the visible material tint: Red paint selects the Red overlay even if that overlay is green moss.
- Drag the primary mouse button over the selected mesh to paint.
- **Fill** and **Sample** act once when the mesh is clicked.
- Hold Alt to use Unity's normal Scene view camera navigation.
- Press Ctrl+Z once to undo the complete drag gesture.
- Press Ctrl+Y to redo it on Windows.

Save the scene and reopen it before treating the lesson as complete. The saved Color32 array should remain attached to this renderer; the transient stream is reconstructed rather than saved as the paint authority.

To render the paint as material layers, assign a material using **SimpleBlend/Layer Blend 3** or **SimpleBlend/Layer Blend 4**. Clear vertices select the base layer; Red, Green, and optionally Blue select the overlay layers. See [Layer Blend shader setup](layer-blend-shaders.md).

The projected wire disc follows the hit point and surface normal. Choose Red, Green, Blue, Alpha, RGB, or Density from **View**. The preview does not replace or edit the renderer's materials.

## Safety and current limitations

- No `MeshCollider` is required. SimpleBlend reads triangle data through Unity's public mesh-data API and performs its own hit test.
- Imported source meshes remain unchanged. Per-renderer `Color32[]` data is authoritative.
- Skinned painting raycasts and paints the current baked pose while preserving the source mesh, bind poses, bone weights, and blend shapes in the generated fallback.
- Painting a prefab instance creates local data and renderer overrides that can be reverted. Painting and saving in Prefab Mode updates inheriting instances.
- Static-batched, skinned, and GPU Resident Drawer targets use owned generated clones. Generated assets are retained when data is cleared or reverted; automatic orphan deletion is intentionally deferred.
- Hand-written blend shader passes and Windows player compilation are automated. Live visual parity, Frame Debugger batching, and lightmapping appearance remain intentionally separate manual evidence.
- Meshes without triangle geometry are rejected with an explicit validation message.
