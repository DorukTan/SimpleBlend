# Texture weathering engine

This revision adds an internal texture-mode weathering engine, not a user-facing Studio feature. There is no Weathering panel, window, menu command or live Scene preview yet. The layout choice and user-facing preview/apply/cancel workflow remain in roadmap issue #28.

## Mask sources

- **Noise** uses seeded three-dimensional value noise in the owned mesh's local coordinates. Moving, rotating or scaling its scene object does not move the pattern within the mesh. Noise scale is inverse local length, not world metres.
- **Mesh borders** follows edges with one incident triangle across the entire mesh, including unpainted material sections. A material boundary alone is not an open border. Split vertices at hard normals or atlas seams do not create borders when their endpoint positions match exactly.
- **UV seams** follows geometrically shared edges with two incident triangles whose original UV0 coordinates disagree at either endpoint. Generated paint UV4 seams are not used. The source requires UV0 for every vertex.

Edge incidence uses exact local endpoint positions. Edges with more than two incident triangles are ambiguous/nonmanifold and are not UV seam sources; degenerate zero-length edges are ignored. Nearly coincident vertices are not welded by tolerance. Coincident shells can also produce ambiguous incidence. This is not mesh repair or an arbitrary-soup topology guarantee.

Edge distance is Euclidean distance in mesh-local units, not a geodesic distance constrained to the surface. Nearby folded or disconnected parts can therefore share an edge influence. A bounds tree prunes segment-distance queries, but production responsiveness on dense assets has not been established.

## Parameters

The internal settings store Source, Seed, Noise scale, Coverage, Width, Softness and Strength. Coverage adjusts a noise threshold; it is not a promise that an exact percentage of texels will be affected. Zero coverage or strength changes nothing; full coverage removes noise variation. Width limits the border/seam band in local units; zero width disables that band. Softness feathers the noise threshold and the band's outer edge. Strength is the maximum mixing amount toward the selected named surface, or the erase amount toward Base under the existing erase rules.

Settings reject nonfinite or out-of-range values. Noise scale must be greater than zero and at most 1000, width is between zero and 1,000,000 local units, and coverage/softness/strength are in [0, 1]. Scaled noise coordinates must lie within ±100,000,000. Noise uses a private integer hash and does not consume Unity's global random state. A deterministic next-seed helper is available for the later Randomize action.

## Preview and Apply safety

`TextureWeatheringPreview` captures the selected texture target, material section, surface, Paint/Erase choice, original pixels, surface names, material state and geometry fingerprint. Generate computes detached CPU pixels from that baseline; rebuilding never builds on the previous preview. CopyPixels returns a copy, not writable access to saved paint. Cancel/Dispose closes the transaction without writing textures, registering Undo or queuing a save.

Generation accepts a cancellation callback. Cancellation throws before publishing a new preview and retains the previous completed result; the caller can then cancel the whole transaction. No partial result is applied automatically.

Apply validates the capture again. New paint, changed surface configuration, material changes, geometry/UV/topology changes, missing references, changed mask dimensions, an inactive target or a different section require a fresh preview. Object transforms do not invalidate this local-space operation. Apply cannot run twice. A no-op creates no Undo entry or dirty/save operation. A successful Apply writes the exact preview result in one Undo operation and queues only the owned paint asset for normal scoped saving.

Generation is limited to the configured material section and its generated gutters. It preserves alpha and unused weight channels, including gutter bytes; painting a selected surface intentionally reduces competing active overlay weights. Uncovered and unselected section texels remain unchanged. Weathering is section-wide and does not use the brush's camera-visibility filter. It never modifies source meshes, source materials, source palettes or texture import settings.

The preview is an internal CPU transaction only. Isolated tests do not establish user-visible preview appearance, first-use clarity, final material appearance or production-scale performance. The Studio controls, Scene preview rendering, vertex-mode operations and native acceptance remain unfinished.

## Verification

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-23 passed all 455 editor tests, including 25 weathering cases. Coverage includes distinct border/UV0 sources, a closed split-vertex cube, material-section boundaries, indexed distances against brute-force segment queries, deterministic settings/asset reload, seed isolation from Unity random state, preview/cancel file and dirty-state safety, cancellation, stale paint/material/position/UV/topology/dimension/target rejection, reserved gutter channels, section isolation, no-op Apply, detached-copy isolation, saved reimport and Undo/redo. These are engine and persistence checks, not approval of a finished artist workflow.
