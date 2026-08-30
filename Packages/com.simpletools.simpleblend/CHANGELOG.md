# Changelog

All notable changes to SimpleBlend will be documented in this file.

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
