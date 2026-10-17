# SimpleBlend visual language

Owner reference: the supplied SimpleTransform/Simple Platform Inspector screenshot, reviewed on 2026-10-16. The reference image is not copied into the repository. This direction extends the SimpleTools family; it is not a requirement to use only Unity's unstyled default controls.

## Shared family traits

- Unity-like neutral panels and dark input fields, with a readable light-theme equivalent.
- Blue active task tabs with both a custom icon and an action name.
- Compact playback/tool-style action rows; keep the primary action above advanced settings.
- Related controls grouped into a contained section, with a concise heading and context on the opposite side.
- Restrained green selection accents and labeled surface chips. Never rely on color alone.
- Clear foldouts for advanced options, rather than exposing internal state first.

SimpleBlend uses its own brush/layer/mesh symbols, not SimpleTransform's movement or timeline symbols. Do not add a timeline, step numbering or animation controls to a painting workflow merely because the reference has them.

## Language for ordinary use

| Main control | Technical detail, when needed |
| --- | --- |
| Paint object | Renderer and mesh ownership |
| Choose a surface | Material layer, property mapping and channel |
| Start / Stop painting | EditorTool activation |
| Set up painting | Initialize serialized per-renderer paint |
| Make a more detailed paint copy | Non-destructive subdivision |
| Brush size | World-space radius, shown in meters |
| More paint options | Flow, falloff, channel profiles, targeting and masks |
| Source and saved-data details | Signature, storage policy, generated paths and ownership IDs |

“Shape”, “Surfaces” and “Save” are concise task labels; their detailed panels must still explain what the operation changes. Simplification must not disguise destructive actions, saved-data ownership or unavailable features.

## Icon family

Seven original vector drawings use the family blue/green accents. Sources live in `tools/icon-sources`; transparent 64px PNGs live in the package's editor-only `Icons` directory. The prototype uses exact generated SVG copies. Each glyph must remain recognizable at 16, 32 and 64 pixels on dark and light surfaces.

- Blend: the paint-data component and package identity.
- Paint: the active brush tool and its script.
- Terrain: the terrain binding window and its script.
- Sculpt, Create, Materials and Output: reserved for the corresponding future authoring surfaces; these do not represent implemented runtime components.

All drawings are original project content under the repository MIT license. They are geometric vector assets, not sourced pictograms or raster image edits. Rebuild with `node tools/Build-EditorIcons.cjs` using the development-only `sharp` package. No dependency is added to the Unity package. Existing script `.meta` GUIDs remain unchanged; icon references are persistent importer configuration, with no startup script that rewrites importers or triggers reimport loops.

Keep internal strokes clipped to their parent silhouette and badges inset from the canvas edge. Joined shapes must overlap rather than leave hairline transparency. Run `node tools/Test-EditorIcons.cjs` after rebuilding: it checks source/copy equality, reproducible PNGs, silhouette fixtures, transparent margins, connected shapes and accidental holes at 16/32/64px. These checks supplement visual inspection on light and dark surfaces; they do not establish that a symbol is understandable.

## Scope and verification

This pass updates the browser review prototype, current script/tool/window icons and the existing component Inspector. The Inspector now puts setup/painting first, keeps the sparse-mesh warning visible, and groups mesh preparation and saved-data diagnostics in foldouts. Existing operations and their safety checks remain unchanged. It does not implement the full custom Unity workbench in #25/#26. #24 remains open for owner review of the revised screens.

Verify the browser's interaction boundaries and primary-action visibility at both dock widths/themes. Verify icon transparency, import settings, persistent MonoImporter assignments, toolbar content and window title through isolated Unity tests. A passing test count does not establish general usability; keep the first-use script and observed review gate.

Verification on 2026-10-16: 55 browser checks and 189 isolated Unity EditMode tests passed with no failures or skips. Browser captures were inspected in both themes and dock widths. Native Inspector visual review remains pending; compilation and behavior tests do not establish its appearance in Unity.

Icon correction follow-up: rebuilt the silhouettes and joins after owner feedback about gaps and overflow. Inspected all seven icons at 16/32/64px on light and dark backgrounds. The 63 asset checks, 12 isolated Unity icon tests and 55 browser checks passed; the full 189-test run above predates this asset-only correction. Existing icon GUIDs and importer assignments are unchanged.

Native foundation (#66): the dockable paint workspace uses the corrected icon family, Unity-themed controls, a target/action-first layout and compact grouped brush settings shared with the Scene overlay. Advanced masks have a visible summary when collapsed and enabled. It does not expose nonfunctional future task tabs. The first slice is direct-channel painting; named layers, palettes and guided material creation remain in #25. Native screenshots, theme/dock/focus review and the complete owner walkthrough remain pending.
