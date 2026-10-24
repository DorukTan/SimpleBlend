# Brush tips and strokes

Texture painting uses a sequence of brush-tip stamps projected onto the mesh. Open **Window > SimpleBlend > Workspace**, choose **Texture paint**, and start painting. The controls stay in the existing Brush panel; changing a brush finishes the current stroke but leaves the tool active.

## Start with a preset

Choose **Soft brush**, **Hard brush**, **Wash**, **Flat brush** or **Chalk** in Quick preset. Soft, Hard and Wash retain their familiar radius/strength/flow values. Flat starts with 30% roundness and 80% hardness; Chalk uses a deterministic grain pattern and 20%-of-diameter spacing. These presets explicitly enable **Limit stroke to opacity**. Applying one does not change the selected surface or Paint/Erase action.

The compact tip/stroke preview shows the selected tip evaluator, spacing, flow and opacity behavior. It is a fixed-scale brush diagnostic, not a preview of material textures, mesh coverage or final rendered appearance. Its image preserves aspect ratio when docking changes.

## Shape controls

- **Brush tip**: Round, Flat, Chalk or Custom. Flat has a rectangular boundary; Round and Chalk have elliptical boundaries. The Scene outline reflects angle and roundness, rather than always showing a circle.
- **Brush radius (m)**: world-space half-size before roundness and tip aspect ratio. Flat/custom corners can extend beyond the nominal radius; the outline shows their actual boundary.
- **Brush shape > Hardness (%)**: expands the solid center of the falloff. At 100%, the in-bounds tip samples the beginning of the Falloff curve throughout. For a solid hard mark, use a curve beginning at full influence.
- **Roundness (%)**: compresses the tip vertically before rotation. Values range from 5% to 100%.
- **Angle**: rotates the tip in the projected surface plane. Orientation is relative to the Scene camera's right axis captured at stroke start; rotating the camera between strokes can change tip orientation on the object.
- **Falloff**: retains the existing curve, remapped by hardness. This keeps curve-based brush customization available.

Custom tips require a saved Texture2D asset. **Tip mask > Dark Pixels** paints dark areas on white, multiplied by alpha; **Alpha** uses alpha alone. Without inversion, transparent areas do not paint. The image fits within the tip bounds without stretching its source aspect ratio, then roundness/rotation apply. The brush snapshots at most 256 × 256 linear samples through GPU readback, so imported textures do not need Read/Write enabled. Import settings and source texture files are not modified. This is not ABR import or a Photoshop-compatible brush engine.

Custom-tip inversion reverses the selected mask, not the entire stroke influence. In **Dark Pixels** mode, it paints light pixels instead of dark pixels while retaining source alpha: fully transparent pixels still do not paint. In **Alpha** mode, it uses one minus source alpha: transparent areas inside the tip footprint become paintable and opaque areas become empty. Neither mode paints outside the rotated, aspect-correct tip boundary. Falloff, opacity and flow still reduce coverage normally. Painting and the diagnostic preview share this evaluator. Changing inversion creates a new immutable tip snapshot without changing an existing stroke or rewriting the source image. Built-in tips ignore inversion, and choosing a built-in preset resets it.

Choose **Brush tip > Custom**, then enable **Invert tip** directly below **Tip mask**. The tooltip describes the currently selected mask mode. Toggling inversion finishes the current stroke and saves the brush setting without stopping the painting tool; the diagnostic preview updates to the new tip. The checkbox is hidden for built-in tips.

## Stroke controls

**Stroke settings > Spacing (%)** is the distance between stamps as a percentage of the nominal diameter: 10% means 0.2 × radius between dabs, matching the previous spacing. Lower values make denser strokes and cost more work. Higher values reveal individual stamps. Spacing is based on traveled distance rather than mouse-event count.

**Path smoothing (%)** is a distance-based pulled-string stabilizer. It was previously labelled Smoothing; the setting and saved values are unchanged. This stabilizes brush motion, while **Brush action > Smooth** averages surface weights. Higher values allow the painted path to lag behind the pointer by up to twice the radius. A normal release over the mesh catches up through remaining regularly spaced dabs. Misses, focus loss, Alt navigation, tool/target changes and configuration edits do not add a catch-up tail. Large jumps restart the segment; resampled positions are reprojected from the captured Scene view onto the visible mesh section rather than painting across empty space. Camera or target-transform changes finish the stroke. This is not frame-rate or velocity filtering, and there is no exact Photoshop smoothing-parity claim.

**Opacity (%)** maps to the shared brush strength; **Flow (%)** scales each dab. With **Limit stroke to opacity** enabled, overlapping build-up dabs accumulate floating-point coverage but cannot move the material blend farther than the chosen opacity during one stroke. A new stroke can build further. **Capped per stroke** uses the strongest dab coverage instead of repeatedly adding overlapping dabs; **Build up along stroke** accumulates coverage from successive dabs. Paint and Erase use the same tip.

Without the opacity limit, the previous mixing rules remain: capped mode uses the maximum falloff × strength × flow; build-up applies each dab to the current weights and can exceed the displayed opacity over multiple dabs. Existing preferences and old custom presets default to Round, 0% hardness, 100% roundness, 0° angle, 10% spacing, no smoothing and no opacity limit. They are not silently migrated. Choose a new built-in preset or enable the limit explicitly for per-stroke opacity.

Build-up retains floating-point coverage throughout each stroke and derives the displayed bytes from that stroke's original weights. Small contributions can therefore add up even when an individual dab is too faint to change an 8-bit texel. This also prevents repeated low-flow dabs from stalling short of the selected surface because of per-dab byte rounding. Saved masks remain 8-bit, and starting another stroke starts from those saved weights; sub-byte coverage does not carry across strokes. Capped strokes still use the strongest dab rather than adding overlapping dabs.

Paint, Erase and weight Smooth use the same tip coverage and stroke settings. Weight Smooth blends all active surface weights toward a footprint-local average from the stroke baseline; see [Texture surface painting](texture-painting.md). Presets do not change this action.

**Fill** ignores tip shape, angle, spacing, path smoothing and opacity limit, and applies uniform strength × flow to the selected section. It is unavailable while the Smooth action is selected; choose Paint or Erase first. Normal stroke Undo/redo and automatic owned-asset saving remain intact. Mask uploads are batched per input event rather than per stamp.

## Save a brush

**Save preset…** retains tip type, custom texture/mask interpretation and inversion, hardness, roundness, angle, spacing, smoothing and opacity-limit choice, in addition to radius, strength, flow, stroke behavior and falloff. Selecting a Saved preset applies independent setting/curve copies; manual edits do not rewrite the preset. Existing preferences and presets without an inversion field remain non-inverted. Keep custom tip textures alongside the preset in the project. Global brush preferences may refer to unavailable textures when switching projects; choose a valid texture or built-in tip in that project.

## Verification boundary

The custom-tip inversion follow-up on 2026-10-23 passed all 493 isolated editor tests, including 13 added cases. Coverage includes both mask modes, retained transparency, inversion before falloff, rotated aspect-correct bounds, immutable cached snapshots, source PNG and importer metadata preservation, preset reload and built-in reset, and actual Paint/Erase mask writes with opacity/flow and Undo/redo on a rotated, nonuniformly scaled mesh. After adding the Workspace checkbox, compilation and all 45 brush-stroke tests passed in the isolated project. The full suite was not repeated for this control-only addition, and native visual acceptance is not recorded.

The low-flow follow-up on 2026-10-23 passed all 415 isolated editor tests, including nine new accumulation cases. Before the fix, four regression cases failed: 600 dabs at 1% flow stalled at 205/255 for Paint and 50/255 for Erase, and 100 sub-byte dabs made no change in either action. The fixed tests cover convergence, competing weights, alpha preservation, opacity-limited and capped behavior, independent surface strokes and Undo/redo. This is numerical and persistence evidence, not native brush-feel acceptance.

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-20 passed all 406 editor tests, including 23 brush-stroke cases. Coverage checks the evaluator and actual texture writes, custom non-readable texture sampling without source changes, aspect ratio, rotation, deterministic grain, spacing across event subdivisions, stabilizer catch-up/reset, opacity-limited Paint/Erase, batched uploads, Fill, preset reload, invalid custom presets and legacy defaults. Native stroke feel, docking, varied mesh curvature, custom-tip orientation and production performance still require observed owner review. Pressure/tilt, scatter, color dynamics, smudge, ABR import and image-decal layers are not implemented here.

The control vocabulary follows familiar painting conventions; Adobe describes hardness, opacity and flow in [Painting tools](https://helpx.adobe.com/ie/photoshop/using/painting-tools.html) and the responsiveness trade-offs of spacing and smoothing in [painting performance](https://helpx.adobe.com/photoshop/kb/optimize-painting-and-cloning.html). SimpleBlend implements its own surface-weight brush behavior, not Adobe's engine.
