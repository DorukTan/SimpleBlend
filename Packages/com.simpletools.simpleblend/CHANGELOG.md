# Changelog

All notable changes to SimpleBlend will be documented in this file.

## [Unreleased]

### Teaching samples

- Registered Rock and Moss, Roof and Snow, and Wall and Grime as independent Package Manager samples.
- Added procedural meshes, textures, editable per-renderer paint, saved scenes and in-scene channel/brush captions.
- Included sample instructions, MIT notices, provenance records and Package Manager import regression checks.


### Added

- Editor-only terrain binding window using public Terrain, TerrainData, and TerrainLayer APIs.
- Versioned material property validation, complete preflight checks, single-operation Undo, and explicit refresh.
- Regression coverage for one to four layers, live control textures, saved binding references, read-only destinations, and source asset preservation.

Visual verification remains separate work.

### Terrain shader

- Added `SimpleBlend/Terrain Blend` with a world-XZ terrain base and Red, Green, and Blue mesh overlays.
- Reused established height blending and URP rendering passes, including original-world-position sampling in Meta.
- Added terrain mask remapping, explicit diffuse-alpha smoothness selection, texel-center control sampling, and projected normal conversion.
- Added GPU surface probes and a disposable seam/reference player-build fixture. Visual parity remains an unverified acceptance gate.

### Added

- Three-layer and four-layer Shader Graph height-weight subgraphs and shared four-way surface composition.
- Complete URP Lit reference graphs with paired HLSL/graph materials and original procedural textures.
- Graph import, pass-compilation, material-input, and graphics-device parity checks for the shared height and surface functions.

### Changed

- Hand-written shaders use the same surface-composition include as the graph subgraph.
- The test reference calculation ignores the unused fourth height in three-layer blending.

### Fixed

- Height-subgraph property nodes reference their material slots correctly during clean imports.

## [0.4.0-beta.1] - 2026-09-04

### Added

- `SimpleBlend/Layer Blend 3` for a base layer plus Red and Green overlays.
- `SimpleBlend/Layer Blend 4` for a base layer plus Red, Green, and Blue overlays.
- Shared height-aware weighting for albedo, tangent-space normals, metallic, ambient occlusion, and smoothness.
- Forward, deferred GBuffer, ShadowCaster, DepthOnly, DepthNormals, and Meta passes for URP.
- One shared `UnityPerMaterial` buffer, GPU instancing variants, DOTS declarations, and a channel-aware material inspector.
- Shader math, mapping, pass-compilation, batching-declaration, and Windows player-build regression coverage.

### Changed

- The base-plus-overlay vertex-channel convention is now a public, documented contract; vertex alpha remains free for custom data.
- Both public shaders delegate to one hidden pass implementation to prevent pass behavior from drifting between layer counts.

### Verification

- All nine focused shader tests and the complete 82-test package suite passed; a Windows x64 player build compiled forced references to both public shaders for D3D11.
- Live rendering, Frame Debugger batching, lightmapping output, and editor/player visual parity remain separate manual checks.

## [0.3.0-alpha.2] - 2026-09-04

### Added

- Independent Red, Green, Blue, Alpha, and RGB Scene view preview modes.
- A cold-to-hot vertex-density view derived from world-space one-ring edge spacing.
- Transform-revision caching for density data and preview UV uploads.
- A deterministic 40,000-vertex fixture with separate cache, spatial-query, and brush-kernel timing output.
- Regression coverage for preview isolation, shader controls, sparse/dense classification, transform invalidation, fixture checksum, timing budgets, and recurring managed allocations.

### Changed

- The temporary red-only preview shader was replaced by one multi-mode visualization shader.
- Burst remains optional and absent because the managed drag path passes the documented local budget with zero recurring allocations.

## [0.3.0-alpha.1] - 2026-08-31

### Added

- R, G, B, A, and RGB channel targeting with untouched-channel guarantees.
- Paint, zero-baseline erase, one-ring smooth, whole/submesh fill, and barycentric sample modes.
- Strength, falloff, flow, accumulation, and deterministic travel-based stroke sampling.
- World-normal angle, submesh membership, and existing-channel threshold masks.
- A compact persistent Scene view overlay backed by user-local brush preferences.
- Deterministic operation, boundary, topology, mask, transform, event-density, overlay-construction, and Undo tests.

### Changed

- The first-slice red brush was replaced by one generic command and kernel shared by every brush mode.
- Mesh paint snapshots now own reusable adjacency and per-submesh vertex membership.

## [0.2.0-alpha.2] - 2026-08-31

### Added

- One-command, Undo-safe vertex-paint setup for mesh and skinned renderers.
- Prefab-instance override recording, clean reversion, and Prefab Mode propagation.
- Explicit generated-mesh ownership with copy-on-write isolation for inherited or shared fallbacks.
- Baked-pose hit testing and painting for `SkinnedMeshRenderer` targets.
- Inspector visibility for source signatures, storage decisions, generated output paths, and ownership IDs.
- Domain-reload, play-mode, prefab, skinned, ownership, and storage-migration regression coverage.

### Changed

- Storage policy is reevaluated at stroke start and can migrate between streams and clones without losing authoritative colors.
- Fallback validation now compares the complete source topology signature.
- Transient streams are detached before clone-mode application.

## [0.1.0-dev.3] - 2026-08-30

### Added

- Collider-independent, double-sided CPU triangle raycasting for readable and non-readable meshes.
- A transform-aware mesh paint cache with explicit topology and world-data invalidation.
- Uniform-grid vertex-radius queries and a deterministic red-channel brush kernel.
- A Scene view `EditorTool` with projected brush feedback and input capture.
- Non-destructive red-channel preview rendering that does not replace renderer materials.
- One-gesture Undo/Redo for authoritative `VertexPaintData` colors.
- Read-only paint-data Inspector status, initialization, reapply, clear, and tool activation controls.
- Geometry, brush, lifecycle, shared-mesh isolation, toolbar-construction, and real-stroke EditMode tests.

### Changed

- Triangle-free and unsupported paint targets now produce explicit validation failures.
- The first-stroke slice uses fixed R-channel settings: radius `1.25`, strength `1.0`, and solid red. The complete brush UI remains a later milestone.

## [0.1.0-dev.2] - 2026-08-30

### Added

- Versioned `VertexPaintData` with compact `Color32[]` authority.
- Deterministic source identity, vertex-count, submesh-layout, and topology signatures.
- Non-readable source color initialization through Unity's read-only mesh data API.
- Transient Additional Vertex Streams for ordinary per-renderer meshes.
- Explicit generated-mesh fallback for skinned, static-batched, and GPU Resident Drawer cases.
- Validation, lifecycle, shared-mesh, imported-FBX hash, reimport, and clone-equivalence tests.

## [0.1.0-dev.1] - 2026-08-28

### Added

- Initial embedded UPM package structure.
- MIT license and compatibility statement.
- Separate runtime, editor, and EditMode test assemblies.
- Foundation smoke test for package discovery and assembly boundaries.
