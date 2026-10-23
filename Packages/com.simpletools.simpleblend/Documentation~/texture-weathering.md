# Texture weathering

Open **Window > SimpleBlend > Workspace**, choose **Texture paint**, and select a prepared paint object. Expand **Weathering** below the Brush panel. It uses the selected named surface and the existing Paint/Erase choice; it does not create a separate window.

## Preview a treatment

1. Choose **Noise**, **Mesh borders** or **UV seams** as Source.
2. Set the **Seed**, or choose **Randomize** for a different deterministic seed. Tune Noise scale, Coverage, Softness and Strength; border/seam sources also show **Band width (local)**.
3. Choose **Preview**. Generation reports processed mask texels and can be cancelled; the preparation stage may remain at zero while geometry is inspected. No result is applied automatically.
4. Inspect the temporary Scene highlight and the panel's changed-texel count. The highlight uses Unity's selection color to show affected texels, not the final lit material. Unchanged texels are transparent. The saved paint, original renderer assignments and property blocks are untouched.
5. Choose **Apply** to commit one undoable operation, or **Cancel** / Scene-view **Esc** to discard it. The paint tool remains active throughout. Scene paint input in both backends, including vertex shortcuts, pauses while a preview is generating or displayed and resumes afterward. A locked vertex target stays locked; any existing vertex gesture finishes before generation begins.

**Refresh preview** rebuilds from saved authoritative paint, not the previous preview. Editing weathering settings or Randomize discards the old preview; choose Preview again. Selecting another surface or Paint/Erase action, editing surfaces, switching target or painting method, collapsing Weathering, changing tools, Undo/redo, closing the workspace, entering Play mode or reloading scripts discards the preview. Starting a preview in another workspace also discards the earlier one. Project-asset selection can retain the bound target. External changes to captured paint/material/geometry invalidate the preview; Apply revalidates immediately, while background validation can discard it within a quarter-second.

Generation rechecks request ownership and selected target before publishing. Cancel or a scene-selection change during progress discards the candidate. If another request starts, only the newer request can become active, even when both requests use the same workspace session. Cleanup of an older request cannot destroy the newer preview. Selecting a Project asset during generation can retain the bound scene target.

Starting/stopping manual painting or choosing Fill discards the preview before that explicit action. Saving an asset while previewing saves only authoritative paint, never the temporary highlight. Settings persist with the workspace; preview resources do not. Finish an in-progress manual stroke before inspecting weathering: starting Preview finishes that prior stroke normally and captures the resulting baseline.

## Mask sources

- **Noise** uses seeded three-dimensional value noise in the owned mesh's local coordinates. Moving, rotating or scaling its scene object does not move the pattern within the mesh. Noise scale is inverse local length, not world metres.
- **Mesh borders** follows edges with one incident triangle across the entire mesh, including unpainted material sections. A material boundary alone is not an open border. Split vertices at hard normals or atlas seams do not create borders when their endpoint positions match exactly.
- **UV seams** follows geometrically shared edges with two incident triangles whose original UV0 coordinates disagree at either endpoint. Generated paint UV4 seams are not used. The source requires UV0 for every vertex.

Edge incidence uses exact local endpoint positions. Edges with more than two incident triangles are ambiguous/nonmanifold and are not UV seam sources; degenerate zero-length edges are ignored. Nearly coincident vertices are not welded by tolerance. Coincident shells can also produce ambiguous incidence. This is not mesh repair or an arbitrary-soup topology guarantee.

Edge distance is Euclidean distance in mesh-local units, not a geodesic distance constrained to the surface. Nearby folded or disconnected parts can therefore share an edge influence. A bounds tree prunes segment-distance queries, but production responsiveness on dense assets has not been established.

## Parameters

Settings store Source, Seed, Noise scale, Coverage, Width, Softness and Strength. Coverage adjusts a noise threshold; it is not a promise that an exact percentage of texels will be affected. Zero coverage or strength changes nothing; full coverage removes noise variation. Width limits the border/seam band in local units; zero width disables that band. Softness feathers the noise threshold and the band's outer edge. Strength is the maximum mixing amount toward the selected named surface, or the erase amount toward Base under the existing erase rules. Weathering strength is independent of manual brush opacity/flow.

Settings reject nonfinite or out-of-range values. Noise scale must be greater than zero and at most 1000, width is between zero and 1,000,000 local units, and coverage/softness/strength are in [0, 1]. Scaled noise coordinates must lie within ±100,000,000. Noise uses a private integer hash and does not consume Unity's global random state. A deterministic next-seed helper is available for the later Randomize action.

## Preview and Apply safety

`TextureWeatheringPreview` captures the selected texture target, material section, surface, Paint/Erase choice, original pixels, surface names, material state and geometry fingerprint. Generate computes detached CPU pixels from that baseline; rebuilding never builds on the previous preview. CopyPixels returns a copy, not writable access to saved paint. Cancel/Dispose closes the transaction without writing textures, registering Undo or queuing a save.

Generation accepts a cancellation callback. Cancellation throws before publishing a new preview and retains the previous completed result; the caller can then cancel the whole transaction. No partial result is applied automatically.

Apply validates the capture again. New paint, changed surface configuration, material changes, geometry/UV/topology changes, missing references, changed mask dimensions, an inactive target or a different section require a fresh preview. Object transforms do not invalidate this local-space operation. Apply cannot run twice. A no-op creates no Undo entry or dirty/save operation. A successful Apply writes the exact preview result in one Undo operation and queues only the owned paint asset for normal scoped saving.

Generation is limited to the configured material section and its generated gutters. It preserves alpha and unused weight channels, including gutter bytes; painting a selected surface intentionally reduces competing active overlay weights. Uncovered and unselected section texels remain unchanged. Weathering is section-wide and does not use the brush's camera-visibility filter. It never modifies source meshes, source materials, source palettes or texture import settings.

The Scene overlay draws only the configured section using a temporary change texture/material, with depth testing and no renderer, material-property-block or mesh assignments. It does not replace the object's material, predict final lighting, or display hidden faces through other geometry. Apply still operates on the entire section, including hidden faces. This first UI supports texture weathering only; vertex-mode operations and observed native theme/docking/input/appearance/performance acceptance remain unfinished.

## Verification

The request-ownership follow-up on 2026-10-23 passed all 480 isolated editor tests, including nine new cases. Five regression cases first reproduced lost cancellation, changed selection and superseded/nested requests publishing incorrectly. The fixed cases cover cancellation from progress and cancellation polls, same-session/different-session request supersession, preserved newer resources, Project selection, input suspension during generation/display and release afterward, and finishing a locked vertex gesture without deactivating its tool, unlocking its target or changing either paint baseline. No new controls or native visual acceptance are claimed by this follow-up.

The compact Studio-panel follow-up on 2026-10-23 passed all 471 isolated editor tests, including 16 new session/lifecycle/GPU cases. Checks cover transient-only rendering resources, untouched renderer assignments/property blocks/files/Undo during preview, Apply/Cancel with the texture tool still active, one-operation Undo/redo, single-session ownership, cancellation, stale state, selection/tool/Play-mode transitions, workspace disposal, processed-texel progress, surface/action changes, Fill, no-op Apply and offscreen shader use of UV4 with unchanged-texel discard. Native theme, docking, Scene input delivery and appearance still require observed owner review; offscreen GPU evidence is not a native UI verdict.

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-23 passed all 455 editor tests, including 25 weathering cases. Coverage includes distinct border/UV0 sources, a closed split-vertex cube, material-section boundaries, indexed distances against brute-force segment queries, deterministic settings/asset reload, seed isolation from Unity random state, preview/cancel file and dirty-state safety, cancellation, stale paint/material/position/UV/topology/dimension/target rejection, reserved gutter channels, section isolation, no-op Apply, detached-copy isolation, saved reimport and Undo/redo. These are engine and persistence checks, not approval of a finished artist workflow.
