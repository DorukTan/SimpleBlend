# Paint the first red-channel stroke

Version `0.1.0-dev.3` provides a deliberately narrow first painting slice for ordinary `MeshRenderer` objects.

## Set up a target

1. Select a GameObject with a `MeshFilter` and `MeshRenderer`.
2. Add the `VertexPaintData` component.
3. Select **Initialize Vertex Painting** in the component Inspector.
4. Confirm that the Inspector reports valid paint data and shows the source mesh, vertex count, storage mode, and storage reason.
5. Select **Activate SimpleBlend Paint Tool**. The tool appears in the Scene view palette with an `SB` badge.

## Paint

- Drag the primary mouse button over the selected mesh to paint.
- Hold Alt to use Unity's normal Scene view camera navigation.
- Press Ctrl+Z once to undo the complete drag gesture.
- Press Ctrl+Y to redo it on Windows.

The brush is fixed to the R channel, solid red, radius `1.25`, and strength `1.0` in this checkpoint. The projected wire disc follows the hit point and surface normal. The red preview is temporary editor rendering; it does not replace or edit the renderer's materials.

## Safety and current limitations

- No `MeshCollider` is required. SimpleBlend reads triangle data through Unity's public mesh-data API and performs its own hit test.
- Imported source meshes remain unchanged. Per-renderer `Color32[]` data is authoritative.
- Only initialized `MeshRenderer` plus `MeshFilter` targets can activate this paint tool today.
- `SkinnedMeshRenderer`, prefab workflows, static/GPU-resident rendering validation, complete brush settings, other channels, and blend shaders remain later milestones.
- Meshes without triangle geometry are rejected with an explicit validation message.
