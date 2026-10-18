# Named surfaces and reusable palettes

The workspace supports **Named surfaces** for the existing **SimpleBlend/Layer Blend 3** and **Layer Blend 4** URP shaders. A surface palette is an editor-only asset containing a material reference and surface names. The renderer receives only the material; no editor palette component is added to the scene or player.

## Create, assign, choose

1. Open **Window > SimpleBlend > Workspace**, select one mesh object and choose **Set up painting** if needed.
2. Choose **Named surfaces** from Paint workflow, then **Create surface palette**. The creation window is also available at **Window > SimpleBlend > Create Surface Palette**.
3. Choose three or four surfaces, including Base. Give each a distinct name, tint and optional Albedo. **Extra texture inputs** exposes Normal, Mask and Height. Inputs are used as-is: no importer, texture packing or active-pipeline changes occur. The mask convention is R Metallic, G AO, B Reserved, A Smoothness. Configure normal-map import and data color space yourself.
4. Choose **Create palette assets** and a new `.asset` path inside an existing Assets folder. This creates that palette and a matching `.mat` beside it. Both destinations must be unused. Cancelling creates nothing; neither existing assets nor renderer slots are overwritten by creation. These are URP materials; a non-URP project is not converted automatically.
5. Back in the workspace, choose the palette and mesh section. Choose **Assign palette material to this slot** explicitly. Undo restores the old reference; the previous material asset and paint data remain unchanged. Assignment on a prefab instance is an override, not an edit to the prefab asset. In Prefab Mode, saving edits the prefab itself.
6. Choose a named surface, then **Start painting**. Rows show the material's actual Albedo and Tint, not a generated material-result preview. When no Albedo is assigned, the row shows its tint.

Creation leaves the scene target selected. The workspace picks up the new palette but does not assign it or write paint. Selecting an existing palette is also read-only with respect to assets. Changing workflow, palette or section stops the SimpleBlend paint tool; changing scene selection in Named surfaces also stops it unless a paint target is explicitly locked. Stopping releases that lock, so confirm the header target before further assignment. Other tools are not replaced. Closing the workspace still leaves the Scene tool's lifetime alone.

## What a surface paints

| Choice | Stored channels | Other values |
| --- | --- | --- |
| Base | RGB toward zero | Alpha preserved |
| First overlay | Red toward one | Green, Blue and Alpha preserved |
| Second overlay | Green toward one | Red, Blue and Alpha preserved |
| Third overlay (four-surface material only) | Blue toward one | Red, Green and Alpha preserved |

Choosing a surface configures the existing brush: Paint operation, mapped channel/value, Material view and a mask for the chosen mesh section. It enables section-only diagnostic preview if you later choose a channel view. It does not immediately paint. Strength, size, flow, falloff and other masks are retained. Base is a strength-controlled return toward the unpainted surface, not a separate fourth stored weight. Overlay choices do not erase competing overlays; height blending and channel normalization retain their existing semantics.

Operation can still be changed to Erase, Smooth, Fill or Sample. More paint options exposes raw channel/value controls as an explicit override. Overlay edits and sampled values share the same brush settings; if they no longer match a named choice, the workspace removes its selected-row indication and asks you to choose again. Nothing silently resets your advanced settings. Direct channels remains available for custom shaders, alpha work and non-palette workflows; switching workflow does not clear active masks.

## Mesh sections are not independent vertex storage

The section choice targets vertices referenced by that submesh. A vertex shared by two sections still has one paint value, so a stroke can also change the neighboring section. Make an appropriately split mesh if independent weights are required. **Preview section only** can restrict channel/RGB/density overlays to the masked section; **Material** view shows the actual assigned materials with no replacement overlay. It does not hide neighboring sections. See [Scene controls](scene-input.md) for target locking and remaining observed-acceptance boundaries.

## Share and edit

Share the palette `.asset`, its matching `.mat`, referenced textures and their `.meta` files together. Names/material references persist in the project; brush preferences and the workspace's selected palette/layout remain per-user editor state, not package data. You can also create a palette through **Assets > Create > SimpleBlend > Surface Palette** and explicitly reference an existing supported material in its Inspector. Use distinct, nonempty names. A shader/count mismatch or invalid palette is blocked with guidance, not repaired during selection.

Editing a shared material changes every renderer using it. Use a separate material/palette copy when different surfaces are needed. The creation form is intentionally create-new-only; it does not modify existing palettes or materials. Named surfaces currently supports these two hand-written shader contracts, not arbitrary Shader Graph or terrain profiles.

Native theme/dock/focus appearance and the complete first-use walkthrough remain pending observed acceptance. Automated persistence, mapping, Undo and source-preservation checks are not evidence of visual quality or complete product parity.

Verification on 2026-10-18: 226 isolated Unity 6000.4.8f1 EditMode tests passed with no failures or skips, including 23 palette cases. These include save/reimport references, asset collision rejection, slot assignment/Undo, prefab overrides and real Base/overlay strokes preserving alpha and source data. Documentation, roadmap and existing icon asset checks also passed. No desktop automation or native visual verdict was used.
