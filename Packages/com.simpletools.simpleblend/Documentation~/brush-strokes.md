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

Custom tips require a saved Texture2D asset. **Tip mask > Dark Pixels** paints dark areas on white, multiplied by alpha; **Alpha** uses alpha alone. Transparent areas do not paint. The image fits within the tip bounds without stretching its source aspect ratio, then roundness/rotation apply. The brush snapshots at most 256 × 256 linear samples through GPU readback, so imported textures do not need Read/Write enabled. Import settings and source texture files are not modified. This is not ABR import or a Photoshop-compatible brush engine.

## Stroke controls

**Stroke settings > Spacing (%)** is the distance between stamps as a percentage of the nominal diameter: 10% means 0.2 × radius between dabs, matching the previous spacing. Lower values make denser strokes and cost more work. Higher values reveal individual stamps. Spacing is based on traveled distance rather than mouse-event count.

**Smoothing (%)** is a distance-based pulled-string stabilizer. Higher values allow the painted path to lag behind the pointer by up to twice the radius. A normal release over the mesh catches up through remaining regularly spaced dabs. Misses, focus loss, Alt navigation, tool/target changes and configuration edits do not add a catch-up tail. Large jumps restart the segment; resampled positions are reprojected onto the actual mesh rather than painting across empty space. Smoothing is not frame-rate or velocity filtering, and there is no exact Photoshop smoothing-parity claim.

**Opacity (%)** maps to the shared brush strength; **Flow (%)** scales each dab. With **Limit stroke to opacity** enabled, overlapping build-up dabs accumulate floating-point coverage but cannot move the material blend farther than the chosen opacity during one stroke. A new stroke can build further. **Capped per stroke** uses the strongest dab coverage instead of repeatedly adding overlapping dabs; **Build up along stroke** accumulates coverage from successive dabs. Paint and Erase use the same tip.

Without the opacity limit, the previous mixing rules remain: capped mode uses the maximum falloff × strength × flow; build-up applies each dab to the current weights and can exceed the displayed opacity over multiple dabs. Existing preferences and old custom presets default to Round, 0% hardness, 100% roundness, 0° angle, 10% spacing, no smoothing and no opacity limit. They are not silently migrated. Choose a new built-in preset or enable the limit explicitly for per-stroke opacity.

**Fill** ignores tip shape, angle, spacing, smoothing and opacity limit, and applies uniform strength × flow to the selected section. Normal stroke Undo/redo and automatic owned-asset saving remain intact. Mask uploads are batched per input event rather than per stamp.

## Save a brush

**Save preset…** retains tip type, custom texture/mask interpretation, hardness, roundness, angle, spacing, smoothing and opacity-limit choice, in addition to radius, strength, flow, stroke behavior and falloff. Selecting a Saved preset applies independent setting/curve copies; manual edits do not rewrite the preset. Keep custom tip textures alongside the preset in the project. Global brush preferences may refer to unavailable textures when switching projects; choose a valid texture or built-in tip in that project.

## Verification boundary

The isolated Windows Unity 6000.4.8f1 / URP 17.4.0 / D3D11 run on 2026-10-20 passed all 406 editor tests, including 23 brush-stroke cases. Coverage checks the evaluator and actual texture writes, custom non-readable texture sampling without source changes, aspect ratio, rotation, deterministic grain, spacing across event subdivisions, stabilizer catch-up/reset, opacity-limited Paint/Erase, batched uploads, Fill, preset reload, invalid custom presets and legacy defaults. Native stroke feel, docking, varied mesh curvature, custom-tip orientation and production performance still require observed owner review. Pressure/tilt, scatter, color dynamics, smudge, ABR import and image-decal layers are not implemented here.

The control vocabulary follows familiar painting conventions; Adobe describes hardness, opacity and flow in [Painting tools](https://helpx.adobe.com/ie/photoshop/using/painting-tools.html) and the responsiveness trade-offs of spacing and smoothing in [painting performance](https://helpx.adobe.com/photoshop/kb/optimize-painting-and-cloning.html). SimpleBlend implements its own surface-weight brush behavior, not Adobe's engine.
