# Checkpoint B — durable data and validation

Date: 2026-08-30

Result: passed.

## Environment

- Unity: `6000.4.8f1` (`f8b72d3d7343`)
- Universal Render Pipeline: `17.4.0`
- Test Framework: `1.6.0`
- Build target: Windows 64-bit
- Starting commit: `1355678`
- Package version: `0.1.0-dev.2`

## Delivered boundary

- Versioned `VertexPaintData` with serialized source reference, source signature, `Color32[]`, storage mode/reason, generated fallback reference/path, and validation state.
- Deterministic source identity, vertex-count, submesh-layout, and indexed-topology validation.
- Existing color import and clear-color initialization through read-only MeshData, including non-readable sources.
- Transient, color-only Additional Vertex Streams for ordinary `MeshRenderer` objects.
- Explicit persistent clone policy for skinned, static-batched, and GPU Resident Drawer cases.
- Generated mesh assets that preserve vertex streams, indices, index format, submeshes, bounds, bind poses, bone weights, and blend shapes.
- Apply, disable/enable, prefab serialization/reconstruction, reinitialize, reject, and clear lifecycles.

No Scene view hit testing, brush mutation, undo handling, inspector, or shader code exists at this checkpoint.

## Verification evidence

| Gate | Observed result |
|---|---|
| EditMode suite | 18 total, 18 passed, 0 failed, 0 skipped |
| Imported FBX integrity | SHA-256 remained `42ee416f3a28f520588bb1254d60c991a63e9dfbf03046e9565c7d5fe39a3306` before and after independent red/blue streams |
| Shared-mesh isolation | Two renderers received separate transient meshes and distinct colors; source colors remained empty |
| Changed vertex count | OBJ reimport from 3 to 4 vertices returned `VertexCountMismatch` and detached output |
| Same-count topology mutation | Returned `TopologyMismatch` |
| Submesh mutation | Returned `SubMeshLayoutMismatch` |
| Non-readable source | Signature, color import, and ordinary generated clone succeeded through public read-only MeshData |
| Serialized stream lifecycle | Prefab instance reconstructed its transient stream from serialized colors |
| Serialized clone lifecycle | Prefab instance restored its generated fallback reference and assignment |
| Clone equivalence | Vertex attributes, submeshes, skinning, blend shape, bounds, and index format matched the source; only colors changed |
| Clear/reinitialize safety | Source assignment restored; generated assets remained on disk and were never implicitly deleted |
| Player compile | Windows 64-bit build completed with `Result: Success` and exit code 0 |
| Package/test warnings | None in the final EditMode run |

The FBX fixture is copied at test time from the installed URP sample package and is not redistributed by SimpleBlend. Test assets are created under unique disposable `Assets/__SimpleBlendTests_*` folders and deleted after each test.

Local XML, logs, and player output are under ignored `TestResults/` and `Builds/CheckpointB/` directories.

## Decisions locked by evidence

- Serialized `Color32[]` remains the sole editable authority; generated meshes and streams are adapters.
- The topology hash includes submesh topology, index count, base vertex, and every index—not vertex count alone.
- Stable source identity is `GUID:local-file-ID` when AssetDatabase provides it. Built-in or procedural sources fall back to direct reference plus topology evidence.
- Generated files use `AssetDatabase.GenerateUniqueAssetPath`, live beside writable source assets when possible, and are never deleted by clear/reinitialize.
- NativeArray is used only at the public MeshData transfer boundary. SimpleBlend has no Burst code and declares no Burst package dependency.
- Static and GPU Resident Drawer modes are policy-tested here; their interactive renderer behavior remains a Checkpoint D manual gate.
- Non-readable ordinary meshes are verified. Non-readable skinned sources with blend shapes are not yet a support claim; their complete fallback path remains a Checkpoint D gate.

## Project-diff audit

Unity again serialized unrelated URP prefilter caches, volume defaults, and standalone batching defaults during the player build. Those known build-generated edits were reverted. The checkpoint contains only package code, package metadata, tests, and documentation.
