# Checkpoint D — prefab and fallback integration

- Date: 2026-08-31
- Package: `com.simpletools.simpleblend` `0.2.0-alpha.2`
- Editor: Unity `6000.4.8f1`, URP `17.4.0`

## Outcome

Checkpoint D passed. SimpleBlend now treats prefab boundaries, generated-mesh ownership, storage-policy transitions, skinned poses, Undo, and edit/play reconstruction as explicit parts of the paint workflow while keeping serialized `Color32[]` data as the sole editable authority.

## Live editor evidence

A disposable fixture exercised ordinary prefab instances, Prefab Mode, stream reconstruction, static fallback, skinned fallback, and GPU Resident Drawer policy display.

- Painting an ordinary prefab instance produced visible red paint and listed `Vertex Paint Data (Script)` in Unity's Overrides popup. **Revert All** removed the local paint.
- Painting and saving the prefab asset in Prefab Mode propagated visible paint to an untouched instance without creating a local paint-data override.
- Entering and leaving Play Mode preserved the propagated paint.
- A static-batched renderer used an owned clone, displayed its reason/path/ownership ID, and accepted visible paint.
- A skinned renderer used an owned clone and accepted visible paint against its baked pose.
- A GPU-policy fixture displayed the cloned-mesh reason and offered policy migration when checked under the project's currently disabled GPU Resident Drawer setting.
- The Console remained at 0 errors and 0 warnings.

Unity may show a `Mesh Renderer` override for an ordinary stream-mode instance because its transient `additionalVertexStreams` adapter is per instance. The authoritative paint override remains `Vertex Paint Data (Script)`.

The validation scene, prefabs, meshes, materials, and temporary helper were removed after the check; no disposable validation asset remains in `Assets/`.

## Automated evidence

- EditMode result: 36 passed, 0 failed, 0 skipped.
- Coverage includes one-operation setup Undo/Redo, prefab override/revert, Prefab Mode propagation, copy-on-write fallback ownership, skinned baked-pose painting and mesh preservation, real play-mode/domain reload, and stream/clone policy migration with color retention.
- Windows x64 player build: success. `SimpleBlend.Runtime.dll` entered the player build and `SimpleBlend.Editor.dll` did not.

## Ownership and cleanup boundary

Fallback meshes receive an ownership ID. Painting forks an inherited or shared fallback before modifying it, preventing paint bleed into siblings or source prefab assets. Clear, revert, and policy migration never delete generated assets automatically; an explicit orphan-cleanup workflow remains deferred.

## Next boundary

M5 adds the complete deterministic brush operation layer: R/G/B/A/RGB channels, erase, smooth, fill, sample, masks, flow, accumulation, and the compact Scene view overlay. Shader blending remains deferred to M7.
