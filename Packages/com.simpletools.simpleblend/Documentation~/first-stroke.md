# Paint the first red-channel stroke

Version `0.2.0-alpha.2` provides a deliberately narrow brush with production storage and prefab behavior for `MeshRenderer` and `SkinnedMeshRenderer` objects.

## Set up a target

1. Select a GameObject with a `MeshFilter`/`MeshRenderer` or a `SkinnedMeshRenderer`.
2. Choose **GameObject > SimpleBlend > Enable Vertex Painting**, or use **Enable Vertex Painting** from the Renderer component context menu.
3. Confirm that the Inspector reports valid paint data and shows the signed vertex/submesh counts, topology hash, storage mode and reason, and any generated output path/ownership ID.
4. If the component was added manually, select **Initialize Vertex Painting** in its Inspector.
5. Select **Activate SimpleBlend Paint Tool** if setup did not already activate it. The tool appears in the Scene view palette with an `SB` badge.

## Paint

- Drag the primary mouse button over the selected mesh to paint.
- Hold Alt to use Unity's normal Scene view camera navigation.
- Press Ctrl+Z once to undo the complete drag gesture.
- Press Ctrl+Y to redo it on Windows.

The brush is fixed to the R channel, solid red, radius `1.25`, and strength `1.0` in this checkpoint. The projected wire disc follows the hit point and surface normal. The red preview is temporary editor rendering; it does not replace or edit the renderer's materials.

## Safety and current limitations

- No `MeshCollider` is required. SimpleBlend reads triangle data through Unity's public mesh-data API and performs its own hit test.
- Imported source meshes remain unchanged. Per-renderer `Color32[]` data is authoritative.
- Skinned painting raycasts and paints the current baked pose while preserving the source mesh, bind poses, bone weights, and blend shapes in the generated fallback.
- Painting a prefab instance creates local data and renderer overrides that can be reverted. Painting and saving in Prefab Mode updates inheriting instances.
- Static-batched, skinned, and GPU Resident Drawer targets use owned generated clones. Generated assets are retained when data is cleared or reverted; automatic orphan deletion is intentionally deferred.
- Complete brush settings, other channels and modes, and blend shaders remain later milestones.
- Meshes without triangle geometry are rejected with an explicit validation message.
