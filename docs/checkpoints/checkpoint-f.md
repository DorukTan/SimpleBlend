# Checkpoint F — visualization and managed performance

- Date: 2026-09-04
- Package: `com.simpletools.simpleblend` `0.3.0-alpha.2`
- Editor: Unity `6000.4.8f1`, URP `17.4.0`

## Automated outcome

The M6 automated gate passed. SimpleBlend now exposes independent R/G/B/A/RGB Scene view previews and a topology-relative density heatmap without changing serialized paint authority or renderer materials. Density and its preview upload are revision-cached rather than recomputed during mouse movement.

## Performance evidence

The deterministic fixture is a 200×200 grid with exactly 40,000 vertices and checksum `374687139120000`. The warmed managed path measured spatial query and brush kernel separately across 250 drag samples.

- One-time fixture/topology/spatial-cache construction: 60.353 ms.
- Spatial query median: 0.037 ms.
- Brush kernel median: 0.142 ms.
- Combined median: 0.179 ms.
- Combined maximum: 0.499 ms.
- Samples above 16 ms: 0 of 250.
- Recurring managed allocation: 0 bytes.

The final versioned suite passed 73/73 EditMode tests in 4.688 seconds. Cache construction is intentionally reported separately from the per-sample drag numbers.

The Windows x64 player build also succeeded. `SimpleBlend.Runtime.dll` entered the player build while the editor-only visualization assembly remained outside the player boundary.

## Acceleration decision

No Burst path was added. The existing managed implementation is more than an order of magnitude inside the 8 ms median target and produced no recurring allocation. Adding a second execution path now would increase dependency, testing, and maintenance cost without solving a measured problem.

## Visualization evidence

Deterministic tests cover channel isolation, alpha/RGB behavior, shader mode controls, uniform density, sparse-versus-dense topology, isolated vertices, and density replacement after non-uniform transform changes. The preview mesh stores density separately from colors, and changing preview mode creates no Undo or paint-data mutation.

## Explicitly unobserved

At the user's request, this slice used no desktop/GUI control. Live color-ramp legibility, overlay layout, and interaction feel were not manually reviewed. This checkpoint records the automated visualization contract and managed performance gate, not a visual ergonomic sign-off.

## Next boundary

M7 locks the channel-to-layer convention and builds the shared height-weight math before expanding into complete URP passes and player-render parity.
