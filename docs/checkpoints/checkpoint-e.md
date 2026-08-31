# Checkpoint E — complete brush behavior

- Date: 2026-08-31
- Package: `com.simpletools.simpleblend` `0.3.0-alpha.1`
- Editor: Unity `6000.4.8f1`, URP `17.4.0`

## Automated outcome

The M5 deterministic gate passed. SimpleBlend now has one pure brush operation model for R/G/B/A/RGB paint, erase, smooth, fill, and sample; composable masks; flow and accumulation; and fixed-travel stroke sampling. The Scene view overlay is a thin settings adapter and does not own colors, topology, Undo, or storage behavior.

## Headless evidence

- EditMode result: 66 passed, 0 failed, 0 skipped in 5.838 seconds.
- No C# compiler warnings or errors were found in the test log.
- Windows x64 player build: success. `SimpleBlend.Runtime.dll` entered the player build and `SimpleBlend.Editor.dll` did not.
- Tests cover channel isolation, zero-baseline erase, immutable one-ring smoothing, disconnected/degenerate topology, whole and shared-vertex submesh fill, barycentric sampling, normal/submesh/value masks, inclusive/exclusive threshold bounds, transformed normals under rotation/non-uniform/negative scale, zero strength/flow, accumulation, sparse/dense event parity, one-operation fill Undo, settings normalization, and overlay construction.

## Explicitly unobserved

At the user's request, this slice used no desktop/GUI control. The overlay's visual layout, control feel, and erase/smooth defaults were therefore not manually reviewed. The package compiles and its overlay construction path is tested, but this checkpoint does not claim a live ergonomic sign-off.

The temporary preview remains red-channel-only. Full R/G/B/A/RGB isolation and vertex-density visualization belong to M6.

## Architectural boundary

Serialized `Color32[]` remains the only editable authority. Topology and submesh membership are cached read-only context, brush commands are deterministic values, and Scene view code only supplies hits/settings and applies the tested operation result through the existing Undo/storage lifecycle.

## Next boundary

M6 adds channel-isolation and density views, a repeatable 40k-vertex fixture, allocation/timing instrumentation, and a measured decision on whether optional Burst acceleration is justified.
