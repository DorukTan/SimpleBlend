# SimpleBlend roadmap

Status: planning approved; package implementation has not started.

Verified development baseline: Unity `6000.4.8f1`, URP `17.4.0`, Shader Graph `17.4.0`.

Package identity:

- Package ID: `com.simpletools.simpleblend`
- Display name: `SimpleBlend`
- Runtime namespace: `SimpleBlend`
- Editor namespace: `SimpleBlend.Editor`
- Shader menu: `SimpleBlend/...`
- License: MIT

## Release goal

SimpleBlend 1.0 is a self-contained UPM package that lets a solo developer paint per-renderer vertex colors in the Scene view and use those colors in production-ready URP height-blend shaders, without changing imported source meshes or requiring paid tooling.

The release is complete only when all ten acceptance tests from the brief have been observed. Feature presence alone is not the ship gate.

## Planning lens

- **Entropy** (the natural tendency of systems toward decay, disorder, and complexity without value) is concentrated at Unity serialization, mesh reimport, prefab overrides, generated assets, rendering-path changes, and package removal.
- **Negentropy** (the deliberate reversal of decay through growth, compounding value, and increasing order) comes from one durable color model, an explicit storage policy, vertical-slice tests, and documentation that records what was actually observed.
- **Tacit knowledge** (the unwritten knowledge of how things really work, including assumptions and workarounds) must be captured in API notes, storage reasons shown in the Inspector, and the acceptance-test record.

## Non-negotiable product rules

1. Imported FBX, OBJ, and other source meshes are immutable.
2. Paint belongs to one renderer unless the user paints a prefab asset intentionally.
3. Serialized `Color32[]` data is the authoritative editable state.
4. Transient additional-stream meshes are render adapters, not data storage.
5. Cloned mesh assets are used only when the primary stream path is unsafe or unsupported.
6. Storage mode, reason, source signature, and generated asset path are visible in the Inspector.
7. A topology mismatch prevents application; it never produces a best-effort smear.
8. One drag gesture creates one undo operation.
9. Runtime code has no `UnityEditor` references; editor assemblies never enter player builds.
10. No reflection into Unity internals and no hard Burst dependency.
11. Every dependency must be compatible with MIT redistribution inside another asset pack.
12. No support claim is made for a Unity version that has not been run.

## System boundary

```text
Scene view input
      |
      v
Surface hit test -----> projected brush preview
      |
      v
Mesh paint cache (vertices, normals, triangles, submeshes, spatial indices)
      |
      v
Brush command -----> pure color operations -----> VertexPaintData Color32[]
                                                  |
                                                  v
                                         Storage policy resolver
                                           /                 \
                                          v                   v
                              transient additional      cloned mesh asset
                                  vertex stream          (explicit fallback)
                                           \                 /
                                            v               v
                                               Renderer
                                                  |
                                                  v
                                      SimpleBlend URP shaders
```

The risky interfaces are Scene view hit testing, serialized array undo, source topology changes, prefab boundaries, static batching, GPU Resident Drawer, generated asset ownership, and shader parity.

## Version roadmap

| Milestone | Working release | Demonstrable outcome | Gate |
|---|---|---|---|
| M0 — API verification | Complete | Installed Unity behavior documented | `docs/api-notes.md` reviewed |
| M1 — Package foundation | `0.1.0-dev.1` | Empty embedded package imports cleanly | Editor and player compilation pass |
| M2 — Data safety | `0.1.0-dev.2` | Colors persist per renderer without touching source assets | Data EditMode suite passes |
| M3 — First visible paint | `0.2.0-alpha.1` | Paint one red-channel stroke and see it | One-stroke undo/redo observed |
| M4 — Production storage and prefabs | `0.2.0-alpha.2` | Streams, fallback assets, prefab overrides, and play-mode restoration work | Acceptance tests 1–8 pass except shader build parity |
| M5 — Complete brush engine | `0.3.0-alpha.1` | All brush modes, channels, masks, and fill scopes work | Deterministic brush suite passes |
| M6 — Visualization and performance | `0.3.0-alpha.2` | Channel isolation and density overlay remain interactive on 40k vertices | Profile gate passes without mandatory Burst |
| M7 — HLSL blend shaders | `0.4.0-beta.1` | Three- and four-layer height blending renders in URP editor and player | Shader parity/build gates pass |
| M8 — Shader Graph and terrain matching | `0.4.0-beta.2` | Subgraphs and terrain variant match documented contracts | Visual reference scenes pass |
| M9 — Samples, removal safety, release | `0.9.0-rc.1` → `1.0.0` | Importable package with self-teaching samples and complete evidence | All ten acceptance tests pass |

No calendar dates are assigned yet. Progress advances only when the milestone gate passes.

## M1 — Package foundation

### Deliverables

- Embedded package at `Packages/com.simpletools.simpleblend`.
- `package.json`, `LICENSE`, `README.md`, `CHANGELOG.md`.
- Runtime, Editor, and EditMode test assembly definitions.
- Empty `Runtime/`, `Editor/`, `Tests/Editor/`, `Shaders/`, `ShaderGraph/`, `Samples~/`, and `Documentation~/` structure.
- Development support statement naming only Unity 6000.4.8f1 and URP 17.4.0 as tested.
- A batch-mode EditMode test command documented in the package README.

### Gate

- Unity imports the package with zero compile errors or warnings owned by SimpleBlend.
- Runtime assembly compiles in a player build target without editor references.
- Package contents contain no third-party code or incompatible license.

## M2 — Data safety

### Deliverables

- `VertexPaintData` component with schema version, source mesh reference, source signature, `Color32[]`, resolved storage mode, fallback mesh reference, and validation state.
- A stronger mesh signature than vertex count alone: source identity/local ID, vertex count, submesh layout, index topology, and a deterministic topology hash.
- Initial colors copied from a complete source color stream when present; otherwise initialized to clear.
- Additional Vertex Streams (AVS) builder that creates a transient color stream without altering the source mesh.
- Storage policy resolver for ordinary `MeshRenderer`, `SkinnedMeshRenderer`, static batching, and GPU Resident Drawer.
- Generated-mesh ownership metadata and collision-free output naming.
- Fallback path beside writable source assets; safe redirection to `Assets/SimpleBlendGenerated/` when the source is read-only or inside `Packages/`.
- Clear-data operation that never automatically deletes a generated asset.
- Import/reimport validation that refuses changed topology.

### Edge cases included now

- Shared imported mesh used by multiple renderers.
- Source meshes with and without existing vertex colors.
- Non-readable imported meshes, using public read-only mesh data APIs where supported.
- Multiple submeshes and 16/32-bit index buffers.
- Same vertex count but changed index topology.
- Sources in writable `Assets/` and read-only `Packages/` locations.

### Gate

- Automated source-file hash remains identical before and after paint-data operations.
- Two renderers sharing a source mesh retain independent colors.
- Invalid topology cannot apply.
- Stream and clone modes both survive serialization reload.

## M3 — First visible paint

### Deliverables

- Scene view `EditorTool` that activates from Unity's tool palette.
- Compact Scene view `Overlay` with only radius, strength, target color, and R channel for this slice.
- Public `HandleUtility.RaySnap` path plus a collider-free CPU triangle-raycast fallback; no `MeshCollider` or other temporary component is added to the target.
- Surface-projected brush disc built from the hit normal and drawn as a world-space polyline.
- Input capture through a passive default control.
- Minimal mesh paint cache and uniform vertex grid.
- Pure red-channel paint operation with configurable falloff.
- Hidden, non-destructive channel-preview shader/draw path.
- Undo registered once on stroke start, finalized once on stroke end.

### Gate

On a mesh without a collider: select the object, activate SimpleBlend, drag one stroke, see red weights, press undo once to restore the exact previous array, and redo once to restore the stroke.

## M4 — Production storage and prefab behavior

### Deliverables

- Automatic `VertexPaintData` creation through the dedicated Undo API.
- Prefab instance property overrides recorded through public prefab APIs.
- Prefab Mode editing and save behavior.
- Explicit cloned-mesh fallback for skinned, static-batched, and GPU Resident Drawer cases.
- Inspector showing storage mode, reason, source signature status, and fallback output path.
- Edit-time and load-time stream reconstruction.
- Fallback mesh assignment that preserves all source vertex attributes, submeshes, blend shapes, bounds, and index format.
- Safe clear/reset workflow and teaching error messages.

### Gate

- Prefab instance override appears and reverts cleanly.
- Prefab Mode edit propagates after save.
- Entering and exiting play mode preserves colors without errors.
- Static and skinned fallbacks are visible and render correctly.

## M5 — Complete brush engine

### Deliverables

- Channels: R, G, B, A, and full RGB.
- Modes: paint, erase, smooth, fill, and sample.
- Radius, strength, `AnimationCurve` falloff, flow, and accumulation.
- Whole-mesh and submesh fill.
- Normal-angle, submesh-index, and existing-channel-threshold masks.
- Mesh adjacency cache for smoothing.
- Deterministic stroke sampling based on time and/or brush travel so event frequency does not change results unpredictably.
- Pure operation layer covered by tests independently of Scene view UI.

### Gate

Every mode and mask has deterministic EditMode tests for boundary values, empty selections, degenerate triangles, and untouched channels.

## M6 — Visualization and performance

### Deliverables

- Isolation views for R, G, B, A, and RGB.
- Vertex-density overlay generated from local neighborhood spacing.
- Non-destructive preview rendering that never replaces `sharedMaterials`.
- Cached world positions, normals, vertex grid, triangle acceleration data, and adjacency.
- Cache invalidation on selection, source, topology, transform, and relevant settings changes.
- Profiling fixture with at least 40,000 vertices.

### Performance gate

- No full-vertex scan per mouse-move sample.
- Target paint sample cost: under 8 ms median and no recurring sample above 16 ms on the local 40k fixture.
- Burst remains absent unless profiling proves the managed path cannot meet the gate after cache and allocation fixes.

## M7 — Hand-written URP shaders

### Deliverables

- `SimpleBlend/Layer Blend 3` and `SimpleBlend/Layer Blend 4`.
- Height-aware layer weighting, not sequential linear interpolation.
- Albedo, tangent-space normal, metallic, smoothness, occlusion, and height inputs per layer.
- Consistent UV scale/offset controls and blend transition control.
- Forward/Forward-only, ShadowCaster, DepthOnly, DepthNormals, and Meta behavior required by URP.
- One `UnityPerMaterial` constant buffer with stable layout.
- GPU instancing and DOTS-instancing-compatible declarations where URP permits.
- Material inspectors with clear channel-to-layer mapping.

### Channel mapping decision gate

Before shader implementation, lock one convention and document it everywhere. Recommended default:

- Layer Blend 3: base layer plus R and G overlays.
- Layer Blend 4: base layer plus R, G, and B overlays.
- Unpainted clear color selects the base layer.
- Alpha remains available for custom masks and sample-specific effects.

This convention gives useful output on an unpainted mesh and avoids requiring a normalization pass before the first stroke.

### Gate

- Height transitions preserve visible surface structure.
- Normal and mask channels blend consistently with albedo weights.
- Material inspector reports SRP Batcher compatibility.
- Editor, play mode, and built player produce matching reference captures.

## M8 — Shader Graph and terrain matching

### Shader Graph deliverables

- Three-layer and four-layer height-weight subgraphs.
- Normal and mask blending subgraphs or clearly composed graph functions.
- A parity material demonstrating the same channel convention as HLSL.
- Graph assets that import without modifying generated code by hand.

### Terrain variant contract

Terrain matching is kept narrow enough to ship:

- World-space projection aligns the mesh blend with the selected terrain.
- An editor binder copies the required public `TerrainData`/`TerrainLayer` parameters to the material.
- V1 supports a documented finite terrain-layer set rather than pretending arbitrary terrain shaders are compatible.
- No access to URP terrain internals and no hidden dependency on a particular terrain material inspector.

Before building this slice, run a one-day-equivalent technical spike and lock either:

1. live sampling of the first supported terrain layers and control map, or
2. a generated terrain basemap/normal input consumed by the terrain-match shader.

The second option is the release fallback if live sampling proves version-fragile.

### Gate

- HLSL and Shader Graph examples match within an agreed visual tolerance.
- Terrain seam remains visually continuous from the documented camera distances.
- All source terrain and mesh assets remain unchanged.

## M9 — Samples, documentation, and release

### Deliverables

- Sample scene with an original low-poly rock/moss setup, roof/snow setup, and wall/grime setup.
- In-scene captions naming channel mapping and brush settings.
- Only original, MIT-compatible, or clearly redistributable sample assets.
- Package README, installation guide, first-stroke tutorial, shader guide, troubleshooting, storage-mode guide, and tested-version statement.
- Changelog and semantic version set to `1.0.0` only after gates pass.
- Clean-project installation test from the package folder or packed archive.
- Package-removal test in a disposable copy of the project.

### Package-removal test boundary

Paint data and cloned meshes must not crash the scene when the package is removed. Materials that directly use a shader supplied by the removed package cannot keep that shader; the removal test therefore separately verifies:

1. painted objects using an unrelated base material reopen and render that base material, and
2. SimpleBlend shader materials fail as ordinary missing-shader assets without null-reference spam or scene corruption.

### Final gate

All acceptance tests pass and are recorded with Unity/URP versions, test date, automated result paths, and manual observations.

## Acceptance-test ownership

| # | Acceptance test | First owning milestone | Verification |
|---|---|---|---|
| 1 | Shared FBX remains byte-identical | M2 | Automated hash plus manual imported-FBX pass |
| 2 | Shared mesh does not bleed | M2 | Automated EditMode test and visual check |
| 3 | One stroke undo/redo | M3 | Automated state test plus manual gesture check |
| 4 | Prefab instance override | M4 | Manual editor check |
| 5 | Prefab Mode persistence | M4 | Manual editor check |
| 6 | Play-mode persistence | M4 | Automated reload where possible plus manual check |
| 7 | Player render parity | M7 | Build plus reference capture |
| 8 | Changed topology is rejected | M2 | Automated reimport/signature test |
| 9 | 40k mesh remains interactive | M6 | Profiler measurement and manual drag |
| 10 | Package removal is safe | M9 | Disposable-project removal test |

## Explicitly deferred beyond 1.0

- HDRP shaders.
- Runtime painting API.
- Mesh sculpting or vertex-position editing.
- Texture painting and terrain sculpting.
- LOD paint propagation.
- Multi-object simultaneous strokes.
- Arbitrary custom terrain shader integration.
- Automatic deletion of orphaned generated mesh assets.
- General-purpose mesh utility APIs.

These items are not opportunistically added during v1, even if an implementation looks small.

## Decision log

| Decision | Status | Default |
|---|---|---|
| Package identity | Locked for implementation | `com.simpletools.simpleblend` |
| Tested baseline | Locked | Unity 6000.4.8f1 / URP 17.4.0 |
| Runtime data authority | Locked | Serialized `Color32[]` |
| Static/GPU/skinned storage | Locked | Explicit cloned-mesh fallback |
| Burst dependency | Locked | None unless measured performance requires optional acceleration |
| Shader layer convention | Review before M7 | Base + RGB overlay recommendation |
| Terrain matching contract | Spike before M8 | Live public TerrainData path, basemap fallback |
| Unity 6.3/6.5 support claim | Blocked until tested | Compatibility target only |

## Immediate next move

Execute M1, then stop for a compile/package-layout review before starting the data model. The workspace is not currently a Git repository, so establish a recoverable baseline first or explicitly accept working without version-control rollback.
