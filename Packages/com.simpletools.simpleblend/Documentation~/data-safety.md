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

Generated assets are created beside writable source assets. Sources outside writable `Assets/` locations use `Assets/SimpleBlendGenerated/`. Names are collision-safe.

Clearing or reinitializing paint data never deletes a generated asset automatically. This avoids destroying an asset that another prefab or scene may reference; orphan cleanup will be an explicit workflow later.

## Current boundary

Version `0.1.0-dev.2` does not provide Scene view painting, brush undo, prefab override UI, or blend shaders. Those capabilities build on this data layer in later milestones.
