# Changelog

All notable changes to SimpleBlend will be documented in this file.

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
