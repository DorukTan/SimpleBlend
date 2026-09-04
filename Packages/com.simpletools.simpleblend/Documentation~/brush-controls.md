# Brush controls and exact behavior

The **SimpleBlend Brush** overlay appears in the Scene view. Its controls configure a pure brush command; the overlay does not own paint data or mesh storage.

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

The **Preview** control changes only the Scene view visualization. It never edits `VertexPaintData` or renderer materials.

- **Red**, **Green**, **Blue**, and **Alpha** isolate one authoritative component.
- **RGB** displays the three color components together while ignoring alpha as color.
- **Density** displays topology-relative world-space spacing from cold blue (sparse) through green to hot red (dense). Density is normalized per mesh, uses one-ring edge spacing, and is rebuilt when topology or the target transform changes.

Version `0.3.0-alpha.2` keeps preview rendering and density derivation outside the paint authority. Changing preview mode therefore creates no Undo operation and cannot alter a saved stroke.
