# Checkpoint C — first visible paint

- Date: 2026-08-30
- Package: `com.simpletools.simpleblend` `0.1.0-dev.3`
- Editor: Unity `6000.4.8f1`, URP `17.4.0`

## Outcome

Checkpoint C passed. SimpleBlend can paint one visible red-channel stroke on an initialized ordinary `MeshRenderer` without a collider, treat the complete drag as one Undo operation, redo it exactly, and preserve per-renderer isolation when another object shares the source mesh.

## Live Scene view evidence

The disposable scene used two built-in Plane renderers sharing Unity's Plane mesh. The selected target had initialized `VertexPaintData`; both `MeshCollider` components were disabled.

- The `SB` tool activated from the custom Inspector and appeared selected in Unity's tool palette.
- The projected wire disc followed the surface under the pointer.
- One drag produced a localized red-channel preview while the renderer material remained URP Lit.
- The paint input did not change the selected object.
- The sibling Plane remained gray.
- Ctrl+Z removed the complete stroke with one command.
- Ctrl+Y restored it.
- No new Console errors occurred after the corrected tool assembly finished compiling.

The scene was intentionally not saved and was discarded after validation. No demo scene or generated validation asset was retained in `Assets/`.

## Automated evidence

- EditMode result: 29 passed, 0 failed, 0 skipped in 3.969 seconds.
- Coverage includes non-readable snapshots, submesh/index handling, double-sided ray hits, collider absence, grid/brute-force parity, brush boundaries, untouched channels, transform invalidation, one-stroke Undo/Redo, shared-mesh isolation, tool eligibility, shader discovery, and safe toolbar construction.
- Windows x64 player build: success. The build compiled and copied `SimpleBlend.Runtime.dll`; no `SimpleBlend.Editor` assembly entered the player pipeline.

## Defect found and closed

An icon lookup in the `EditorTool` static initialization path caused Unity's `get_skinIndex` lifecycle exception. The tool now uses a text-only `SB` palette badge with no editor-style lookup during `ScriptableObject` construction, and an EditMode regression test covers the failure.

## Current boundary

This slice is intentionally fixed to radius `1.25`, strength `1.0`, solid red, and the R channel. Full brush controls, prefab and Prefab Mode behavior, skinned painting, static/GPU-resident visual validation, other channels and modes, and blend shaders remain later checkpoints.
