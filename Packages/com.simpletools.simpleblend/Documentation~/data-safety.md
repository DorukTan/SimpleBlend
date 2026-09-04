# Data safety and storage

`VertexPaintData` is the durable authority for one renderer's vertex colors. It stores compact `Color32[]` data beside a signature of the original source mesh. Imported FBX, OBJ, and package meshes are never modified.

## Validation

The signature records the stable asset identity when Unity provides one, vertex count, submesh topology and index counts, and a deterministic hash of the indexed topology. SimpleBlend refuses to apply colors when the source identity, vertex count, submesh layout, or topology changes.

Validation failures remain serialized as explicit reason codes with teaching messages. A rejected source detaches transient output rather than applying colors by best-effort vertex index.

## Storage modes

- Ordinary `MeshRenderer`: a hidden, transient color-only Additional Vertex Stream is reconstructed from `VertexPaintData` when enabled.
- `SkinnedMeshRenderer`: a persistent generated mesh asset is required.
- Static batching: a persistent generated mesh asset is required because Unity consumes and clears Additional Vertex Streams during static combination.
- GPU Resident Drawer: a persistent generated mesh asset is used conservatively because stream compatibility is undocumented and no supported public opt-out exists.

Generated assets are created beside writable source assets. Sources outside writable `Assets/` locations use `Assets/SimpleBlendGenerated/`. Names are collision-safe, and each fallback records an ownership ID. Painting a prefab instance forks an inherited or shared fallback before writing so sibling instances and the prefab asset remain unchanged.

SimpleBlend reevaluates the storage policy at stroke start and on request from the Inspector. A target can migrate between Additional Vertex Streams and a cloned mesh when static batching or GPU Resident Drawer requirements change. The serialized `Color32[]` remains authoritative throughout the migration, so changing adapters does not discard paint.

## Prefabs and lifecycle

- Painting a prefab instance records the `VertexPaintData` property override and any renderer mesh assignment needed by clone mode. **Revert All** restores inherited paint and mesh state.
- Painting in Prefab Mode writes the prefab asset intentionally; saving updates instances that do not have their own paint-data override.
- Unity may list `Mesh Renderer` in the Overrides popup for an ordinary stream-mode instance because `additionalVertexStreams` is a per-instance transient adapter. The authoritative paint override is the separate `Vertex Paint Data` entry.
- Stream-mode output is rebuilt after enable, domain reload, and play-mode transitions. Clone-mode output remains assigned and is revalidated.

Clearing or reinitializing paint data never deletes a generated asset automatically. This avoids destroying an asset that another prefab or scene may reference; orphan cleanup will be an explicit workflow later.

## Current boundary

Version `0.4.0-beta.1` supports ordinary, static, skinned, and conservative GPU-resident storage paths plus prefab, play-mode, complete brush-operation, non-authoritative visualization, and hand-written URP height-blend shader workflows. Automatic orphan cleanup remains a later milestone.
