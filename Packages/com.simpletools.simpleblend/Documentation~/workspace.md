# Paint workspace

Open **Window > SimpleBlend > Workspace** and dock it beside the Scene view. The paint component Inspector and **SimpleBlend Brush** overlay also provide an open-workspace button.

The native workspace provides target preflight, explicit setup/painting, shared brush controls, optional denser mesh copies, [named surface palettes](surface-palettes.md) and [Scene feedback/input controls](scene-input.md). This is not the full browser design prototype or the complete product roadmap; generated geometry, richer presentation and remaining input/acceptance work stay separate.

## Begin painting

1. Select one active mesh object in the Hierarchy. Select a scene instance or open a prefab in Prefab Mode, rather than selecting a prefab asset in the Project window.
2. Read the target guidance. **Set up painting** adds/initializes per-renderer paint data through the existing storage policy. Selection alone does not initialize data, alter import settings, assign materials or activate a tool. Setup can create an owned generated mesh when the storage policy requires it; the imported source stays unchanged.
3. Choose a Paint workflow. **Named surfaces** guides you through creating a palette, explicitly assigning its material to a mesh section, and choosing a texture/tint row. **Direct channels** keeps the existing raw RGBA workflow for custom shaders. See [surface palette setup](surface-palettes.md).
4. Choose **Start painting** explicitly. Choose **Stop painting** to restore the previous editor tool. Closing the workspace leaves the Scene tool's lifetime alone; it does not silently stop an active stroke or replace another tool.
5. Choose Operation, Brush size (world-space radius in meters), Strength and View. Direct channels shows Paint channel and Channel values directly; Named surfaces places those overrides in More paint options. The Scene overlay and workspace edit the same brush settings, stored in the user's Unity preferences, not the package.
6. Paint in the Scene view, then save the scene/prefab. Undo restores a complete stroke. See [first stroke](first-stroke.md) for the material lesson and persistence check.

Existing paint cannot be reinitialized by the workspace setup action. Incompatible topology or a missing source is blocked; inspect the component to diagnose/recover it. Multiple selections do not trigger partial or batch setup. Refresh repeats read-only checks after changes made by other editors.

## Brush options

**More paint options** contains Flow, Falloff, Accumulate and the existing normal/submesh/value masks. Collapsing it hides controls, not their effect: enabled masks and saved brush settings still apply, and active masks are summarized while collapsed. The brush engine and capped/continuous behavior have not changed. Vertex alpha is a stored channel, separate from Strength. See [exact brush behavior](brush-controls.md).

View includes Material, which leaves actual assigned materials visible with no diagnostic overlay, plus channel/RGB/density diagnostics. Preview section only can limit the latter to the masked section. Changing settings repaints the Scene view. The workspace refreshes its controls after changes made in the overlay or by color sampling. Lock paint target pins the header renderer without forcing Unity selection; stopping/unlocking releases the pin.

## Mesh detail and materials

**Mesh detail > Make a more detailed paint copy** uses the same bounded subdivision operation as the Inspector. It creates a renderer-only copy at the same position, preserving interpolated paint, UVs and normals without changing the original. Move/hide one overlapping object before painting. Colliders, children and other scripts are not copied. Cancelling the save dialog changes nothing; an existing asset cannot be overwritten. Undo removes the new object but retains its saved mesh asset. See [sparse mesh preparation](first-stroke.md#painting-a-cube-or-another-sparse-mesh).

**Materials on this object** lists existing slots without editing them. Named surfaces provides a separate explicit palette-material assignment action. Channel painting writes values, not visible surface colors. A material's shader decides how those values render; see [Layer Blend setup](layer-blend-shaders.md). Surface thumbnails are real Albedo/Tint inputs, not a rendered-result preview. Selection does not infer layer names or mutate a material.

## Verification boundary

Read-only preflight, setup safety, Undo, source preservation and window/tool ownership have isolated editor regression coverage. Native light/dark appearance, 320/440-point docking, keyboard focus and the first-use walkthrough still require observed review. The browser prototype is not evidence of native layout correctness.

The foundation's isolated Unity 6000.4.8f1 EditMode suite passed 203 tests with no failures or skips, including 14 workspace cases. This baseline predates the surface-palette addition; its verification is recorded separately. Tests do not establish native visual acceptance or completion of the broader workflow.
