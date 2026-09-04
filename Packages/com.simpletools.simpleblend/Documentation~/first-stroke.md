# Paint the first stroke

Version `0.3.0-alpha.2` provides the complete brush operation set, production storage and prefab behavior, channel-isolated previews, and a vertex-density heatmap for `MeshRenderer` and `SkinnedMeshRenderer` objects.

## Set up a target

1. Select a GameObject with a `MeshFilter`/`MeshRenderer` or a `SkinnedMeshRenderer`.
2. Choose **GameObject > SimpleBlend > Enable Vertex Painting**, or use **Enable Vertex Painting** from the Renderer component context menu.
3. Confirm that the Inspector reports valid paint data and shows the signed vertex/submesh counts, topology hash, storage mode and reason, and any generated output path/ownership ID.
4. If the component was added manually, select **Initialize Vertex Painting** in its Inspector.
5. Select **Activate SimpleBlend Paint Tool** if setup did not already activate it. The tool appears in the Scene view palette with an `SB` badge.

## Paint

- Use the **SimpleBlend Brush** Scene view overlay to choose a mode, target channel, preview, radius, strength, flow, falloff, accumulation, fill scope, and optional masks.
- Drag the primary mouse button over the selected mesh to paint.
- **Fill** and **Sample** act once when the mesh is clicked.
- Hold Alt to use Unity's normal Scene view camera navigation.
- Press Ctrl+Z once to undo the complete drag gesture.
- Press Ctrl+Y to redo it on Windows.

The projected wire disc follows the hit point and surface normal. Choose Red, Green, Blue, Alpha, RGB, or Density from **Preview**. The preview does not replace or edit the renderer's materials.

## Safety and current limitations

- No `MeshCollider` is required. SimpleBlend reads triangle data through Unity's public mesh-data API and performs its own hit test.
- Imported source meshes remain unchanged. Per-renderer `Color32[]` data is authoritative.
- Skinned painting raycasts and paints the current baked pose while preserving the source mesh, bind poses, bone weights, and blend shapes in the generated fallback.
- Painting a prefab instance creates local data and renderer overrides that can be reverted. Painting and saving in Prefab Mode updates inheriting instances.
- Static-batched, skinned, and GPU Resident Drawer targets use owned generated clones. Generated assets are retained when data is cleared or reverted; automatic orphan deletion is intentionally deferred.
- Blend shaders remain a later milestone. The 40k-vertex managed performance gate is automated; live visual and interaction review remains intentionally separate from that timing evidence.
- Meshes without triangle geometry are rejected with an explicit validation message.
