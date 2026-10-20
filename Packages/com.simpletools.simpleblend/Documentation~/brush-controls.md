# Brush controls and exact behavior

The **SimpleBlend Brush** overlay appears in the Scene view. The [paint workspace](workspace.md) uses the same controls and brush settings. These controls configure a pure brush command; neither settings surface owns paint data or mesh storage. Operation, Paint channel, View, Channel values, Brush radius (m), Strength and Fill scope are shown as relevant to the operation. Flow, Falloff, Accumulate and masks are grouped under **More paint options**. Collapsed options remain effective.

## Radius and vertex influence

Brush radius is a world-space spherical distance from the hit point; diameter is twice the shown value. The outer ring is its intersection with the hit plane, not a guarantee of a uniformly painted disk. Default falloff reaches zero at the outer edge. Radius can reach through thin geometry; the existing normal/section/value masks still determine eligibility. This change does not introduce screen-space projection or automatic backface rejection.

For Paint, Erase and Smooth, dots mark reachable vertices that pass masks and have positive input influence. Dot intensity follows the brush command's falloff × Strength × Flow. During capped strokes, threshold masking uses the stroke baseline. Radius/strength adjustment previews use the temporary gesture values without saving them. Fill and Sample do not display a radius-based vertex footprint.

Dots show input influence, not shader opacity, final stored channel values or a promise of additional changes: accumulation, capped-stroke limits, byte rounding, already-matching values and material blending affect results. Occluded markers respect Scene depth. A coarse mesh can have no dots inside a small ring; increase radius or explicitly create a more detailed copy rather than expecting new vertices to appear.

Preview work is bounded to 8,192 spatial-cell/vertex checks, at most 2,048 candidate results and 256 markers per repaint. **Vertex preview limited** discloses truncation or an invalid radius; absence of dots in a limited preview does not prove there are no reachable vertices. Painting queries retain their normal complete results. Preview does not write colors, preferences, materials or Undo records.

Verification (2026-10-20): 318 isolated Unity 6000.4.8f1 EditMode tests passed, no failures/skips, including 11 footprint cases. Tests cover exact input weights/positions, zero strength/flow, threshold baselines, non-radius operations, read-only settings/colors/Undo, gesture overrides, marker/work budgets, dense cells without matches and invalid radii. Eleven changed source/meta files matched the isolated copies. Documentation, roadmap, scoped whitespace and 63 icon checks passed. Native marker visibility, perceived intensity, compact text and owner comparison with painting remain pending; these tests are not a visual verdict.

## Channels

- **Red**, **Green**, **Blue**, and **Alpha** alter only that component.
- **RGB** alters red, green, and blue together while preserving alpha.

## Modes

- **Paint** moves selected channels toward the chosen color.
- **Erase** moves selected channels toward zero. RGB erase preserves alpha.
- **Smooth** moves selected channels toward the uniform average of the vertex's one-ring neighbors. It uses an immutable source snapshot for each stamp, so iteration order cannot change the result. Split/disconnected vertex indices never smooth across each other.
- **Fill** applies the chosen color once to every vertex or once to each unique vertex participating in the clicked submesh.
- **Sample** barycentrically interpolates all four color components from the clicked triangle and places the result in the overlay color field. It does not modify paint data or create an Undo entry.

## Strength, flow, falloff, and accumulation

For each stamp, effective weight is `strength × flow × falloff`. Fill uses `strength × flow` without a radial falloff.

Drag strokes are resampled at fixed travel intervals equal to 20% of the stroke-start radius. The same geometric path therefore produces the same stamps across reasonable differences in Scene view event frequency.

- With **Accumulate** enabled, overlapping stamps build from the current color.
- With **Accumulate** disabled, each vertex is evaluated from its stroke-start color and keeps the strongest stamp seen during that drag.

One drag remains one Undo operation in both modes.

## Masks

Enabled masks compose as an intersection: a vertex must pass every enabled mask.

- **Normal Mask** compares the transformed world-space vertex normal with the normalized direction. Vertices at exactly the maximum angle are included. Rotation, non-uniform scale, and negative scale use the inverse-transpose normal convention.
- **Submesh Mask** accepts vertices referenced by the selected submesh. A shared vertex can belong to more than one submesh.
- **Value Threshold** tests an existing channel against the displayed minimum/maximum range. Bounds can be inclusive or exclusive. When RGB is selected as the threshold channel, the value is the arithmetic mean of R, G, and B.

Masks restrict destination vertices. Smooth may still read an adjacent vertex outside the mask when calculating the accepted destination's one-ring average.

## Preview modes

The **View** control changes only the Scene view visualization. It never edits `VertexPaintData` or renderer materials.

- **Material** draws no diagnostic overlay: the actual assigned materials remain visible while painting. New preferences use this mode; older preview indices are preserved.

- **Red**, **Green**, **Blue**, and **Alpha** isolate one authoritative component.
- **RGB** displays the three color components together while ignoring alpha as color.
- **Density** displays topology-relative world-space spacing from cold blue (sparse) through green to hot red (dense). Density is normalized per mesh, uses one-ring edge spacing, and is rebuilt when topology or the target transform changes.

Version `0.3.0-alpha.2` keeps preview rendering and density derivation outside the paint authority. Changing preview mode therefore creates no Undo operation and cannot alter a saved stroke.

**Preview section only** limits diagnostic overlays to the enabled Submesh Mask section; Material view does not hide other sections. See [Scene feedback and keyboard controls](scene-input.md) for target locking, unbound Shortcut Manager commands, temporary erase and the current Escape/focus behavior.
