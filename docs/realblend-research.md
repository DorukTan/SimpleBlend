# Product research and feature ledger

Research date: 2026-10-16. This is a public-source requirements comparison, not a hands-on review of RealBlend or a claim that SimpleBlend already matches it.

## Sources and confidence

- **store**: [RealBlend listing](https://assetstore.unity.com/packages/tools/painting/realblend-mesh-painting-creation-352952), identifying version 1.3.2 and release date September 17, 2026. The structured page description was inspected because the text-only page renderer omitted its feature sections. Advertising is evidence of scope, not proof of correctness.
- **manual**: publisher-linked [13-page user guide](https://drive.google.com/file/d/1Rievns4WDSr1WxWg3CmxLlX_bd6FK3AB/view). Public PDF fetched and text inspected, including painting, production shaders, sculpt/cut, creation, saving and ProBuilder sections. Snapshot SHA-256: `c74e8f03e04fa8844ec3ce3f66706a4490200d16575dc36ff3eefc79343c65c0`. Its metadata creation date is August 30, 2026; that is not a verified product-version identifier.
- **portfolio**: [publisher portfolio](https://adamn007.github.io/RealBlendPortfolio/), confirming the paint/sculpt/create/bake direction and material semantics.
- **publisher-update**: [developer feature update](https://www.reddit.com/r/Unity3D/comments/1r76rd8/thanks_for_the_support_and_feedback_on_realblend/), used for additional interaction and creation-preview evidence.
- **publisher-wip**: [developer depth-aware painting demonstration](https://www.reddit.com/r/Unity3D/comments/1ubvoem/resident_evil_inspired_depthaware_vertex_painting/). The author calls it work in progress. Do not present it as a verified shipping feature.
- **improvement**: an additional SimpleBlend requirement, not an assertion about a competitor's absence or shortcomings.

No asset package, proprietary source, shader implementation, textures or branding was copied. The publisher's public guide is not redistributed. Implement equivalent user outcomes independently, using reviewed permissive dependencies where appropriate.

## Discrepancies and research limits

1. The current listing advertises five-layer URP and HDRP; the linked manual's compatibility table limits its five-way shader to HDRP. SimpleBlend must implement and verify five-layer URP as well as HDRP rather than use the older table to reduce scope.
2. The manual recommends array-backed three-layer rendering with mapping and surface effects; that workflow is additional to conventional three/five-layer materials, not a synonym for them.
3. The original SimpleBlend mapping is base+R/G(/B), alpha free. The reference product also documents semantic channel profiles. Existing SimpleBlend material/data meaning must remain unchanged; new profiles and explicit copy migration provide additional semantics.
4. Listing keywords do not establish direct pixel texture painting, runtime painting, geo-scattering or a general-purpose modeler. UV texture-mask painting is explicitly planned as an improvement; runtime painting and geo-scattering are not invented parity obligations.
5. Demonstrated depth-aware/tessellation-to-LOD ideas are tracked as qualification work, not independently confirmed shipped behavior. Ordinary POM, silhouette effects and actual geometry displacement must be distinguished.
6. The listing advertises three render pipelines. SimpleBlend currently has one tested Windows/URP development baseline. Optional pipeline packaging and independent consumer tests are required; marking a compatibility target is not support evidence.
7. Reviews and publisher replies suggest useful safety, input, documentation and sample checks, but historical complaints are not treated as proof of present defects. SimpleBlend quality gates stand on its own observations.
8. Public documentation may omit behavior or change later. Full public-feature coverage is the committed scope; absolute parity cannot be certified without closing the ledger, resolving source differences and testing comparable user tasks.

## Current SimpleBlend baseline

Integrated development commit `3312bb4` includes vertex painting, channel/density previews, storage/prefab/Undo handling, three/four-layer URP shaders, terrain binding/shading, graph references, teaching samples and an explicit whole-mesh subdivided copy. The isolated suite passed 177/177 on Unity 6000.4.8f1 / URP 17.4.0 / Windows D3D11. This does not establish usability or public-feature parity.

“Partial” below means a relevant foundation exists, not that the requirement is accepted. Every row still needs its issue's complete evidence. The geometry tools are initially static-mesh authoring; existing skinned painting support must not regress. Do not imply new topology tools support skinning until separately qualified.

## Feature ledger

| Requirement | Required outcome | Evidence | Current state | Owning task |
| --- | --- | --- | --- | --- |
| UX-01 | Task-based setup and authoring workflow | improvement | Missing | [#24](https://github.com/DorukTan/SimpleBlend/issues/24) |
| UX-02 | Unified workspace, contextual fixes and layer thumbnails | manual | Partial | [#25](https://github.com/DorukTan/SimpleBlend/issues/25) |
| PA-01 | Layer-based and direct RGBA workflows with reusable palettes | manual | Partial | [#25](https://github.com/DorukTan/SimpleBlend/issues/25) |
| PA-02 | Size, strength, sensitivity, capped/continuous strokes and shortcuts | manual | Partial | [#26](https://github.com/DorukTan/SimpleBlend/issues/26) |
| PA-03 | Channel/result previews, selection lock and multi-material feedback | manual | Partial | [#26](https://github.com/DorukTan/SimpleBlend/issues/26) |
| PA-04 | Alpha stamps with rotation and inversion | portfolio | Missing | [#27](https://github.com/DorukTan/SimpleBlend/issues/27) |
| PA-05 | Seeded full-surface and border/UV-edge procedural painting | manual | Missing | [#28](https://github.com/DorukTan/SimpleBlend/issues/28) |
| PA-06 | Automatic visible-submesh targeting and explicit submesh lock | manual | Partial | [#29](https://github.com/DorukTan/SimpleBlend/issues/29) |
| MA-01 | Manual mask packing, defaults, roughness conversion and HDRP profile | manual | Missing | [#30](https://github.com/DorukTan/SimpleBlend/issues/30) |
| MA-02 | Folder discovery, suffix matching, batch preparation and optional source cleanup | manual | Missing | [#31](https://github.com/DorukTan/SimpleBlend/issues/31) |
| MA-03 | Reusable array libraries and non-destructive standard-to-array conversion | manual | Missing | [#32](https://github.com/DorukTan/SimpleBlend/issues/32) |
| SH-01 | Base plus four overlays in URP | store | Missing | [#33](https://github.com/DorukTan/SimpleBlend/issues/33) |
| SH-02 | HDRP three/five-layer material workflows | store | Missing | [#34](https://github.com/DorukTan/SimpleBlend/issues/34) |
| SH-03 | Built-in three-layer blending with variation | store | Missing | [#35](https://github.com/DorukTan/SimpleBlend/issues/35) |
| SH-04 | Material variation, painted wetness and optional wetness mask | portfolio | Missing | [#36](https://github.com/DorukTan/SimpleBlend/issues/36) |
| SH-05 | UV/triplanar mapping and parallax occlusion | manual | Missing | [#37](https://github.com/DorukTan/SimpleBlend/issues/37) |
| SH-06 | Painted overlay alpha cutout | manual | Missing | [#37](https://github.com/DorukTan/SimpleBlend/issues/37) |
| SH-07 | Array-backed URP/HDRP materials | manual | Partial | [#38](https://github.com/DorukTan/SimpleBlend/issues/38) |
| SH-08 | Configurable custom-shader mappings and reusable graph functions | manual | Partial | [#38](https://github.com/DorukTan/SimpleBlend/issues/38) |
| GE-01 | Saved geometry, attributes, topology and material-section state | manual | Partial | [#39](https://github.com/DorukTan/SimpleBlend/issues/39) |
| GE-02 | Pose/topology restoration with paint-preserving recovery | portfolio | Missing | [#40](https://github.com/DorukTan/SimpleBlend/issues/40) |
| SC-01 | Sculpt brush family with strength and pose revert | publisher-update | Missing | [#41](https://github.com/DorukTan/SimpleBlend/issues/41) |
| SC-02 | Localized triangle refinement | portfolio | Partial | [#42](https://github.com/DorukTan/SimpleBlend/issues/42) |
| SC-03 | Front-facing removal brushes and existing-rim rounding | manual | Missing | [#43](https://github.com/DorukTan/SimpleBlend/issues/43) |
| SC-04 | Element move, rotate and scale | store | Missing | [#44](https://github.com/DorukTan/SimpleBlend/issues/44) |
| SC-05 | Normals/tangents recalculation, secondary UV repair and statistics | manual | Partial | [#45](https://github.com/DorukTan/SimpleBlend/issues/45) |
| CR-01 | Floor and surface/solid/custom-profile wall generators | manual | Missing | [#46](https://github.com/DorukTan/SimpleBlend/issues/46) |
| CR-02 | Curved walls and configurable boxes | manual | Missing | [#47](https://github.com/DorukTan/SimpleBlend/issues/47) |
| CR-03 | Spline-based structures with profiles, caps and separate floors | manual | Missing | [#48](https://github.com/DorukTan/SimpleBlend/issues/48) |
| CR-04 | Bounded smart preview, placement layers and increment snapping | publisher-update | Missing | [#49](https://github.com/DorukTan/SimpleBlend/issues/49) |
| CU-01 | Closed-cutter boolean subtraction, open/interior options and batch Undo | manual | Missing | [#50](https://github.com/DorukTan/SimpleBlend/issues/50) |
| CU-02 | Grid-aligned through-wall openings and interior submeshes | manual | Missing | [#51](https://github.com/DorukTan/SimpleBlend/issues/51) |
| TR-01 | Static mesh revision transfer with position/projection modes | store | Missing | [#52](https://github.com/DorukTan/SimpleBlend/issues/52) |
| OU-01 | Permanent mesh bake, lightmap UVs and optional component removal | portfolio | Missing | [#53](https://github.com/DorukTan/SimpleBlend/issues/53) |
| TR-02 | ProBuilder inbound/outbound paint sync and topology-owner protection | manual | Missing | [#54](https://github.com/DorukTan/SimpleBlend/issues/54) |
| PLUS-01 | Named editable paint layers and reusable procedural recipes | improvement | Missing | [#55](https://github.com/DorukTan/SimpleBlend/issues/55) |
| PLUS-02 | Controlled multi-object strokes and previewed LOD paint propagation | improvement | Missing | [#56](https://github.com/DorukTan/SimpleBlend/issues/56) |
| PLUS-03 | Texture-mask painting independent of vertex density | improvement | Missing | [#57](https://github.com/DorukTan/SimpleBlend/issues/57) |
| PLUS-04 | Depth-aware material blending with bounded quality modes | publisher-wip | Missing | [#58](https://github.com/DorukTan/SimpleBlend/issues/58) |
| QA-01 | Observed usability and accessible authoring acceptance | improvement | Missing | [#59](https://github.com/DorukTan/SimpleBlend/issues/59) |
| QA-02 | Expanded compatibility, performance and clean consumer evidence | improvement | Partial | [#60](https://github.com/DorukTan/SimpleBlend/issues/60) |
| QA-03 | Complete advertised-feature coverage and beyond-baseline release gate | improvement | Missing | [#61](https://github.com/DorukTan/SimpleBlend/issues/61) |

## Verification rules

Each row progresses through planned, implemented, automated verification, observed workflow/rendering verification and accepted. Record the implementation PR, exact versions/hardware, test fixture/result, manual observation and remaining limits. “Missing” and “Partial” are the initial inventory, not a substitute for those later states.

The final feature-ledger issue blocks the release candidate. All baseline feature rows and the four planned improvement capabilities must be accepted for the expanded 1.0 scope unless the user explicitly approves a scope change. Source conflicts cannot be silently dropped and unit-test counts cannot close user-experience gates.

## Technical references

- [Unity Texture2DArray API](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/texture2darray): validate platform capabilities, compatible dimensions/formats and memory before choosing array layouts.
- [Unity ProBuilder sources and documentation](https://github.com/Unity-Technologies/com.unity.probuilder): compiled mesh ownership requires an explicit adapter, not competing mesh replacement.
- [Unity shortcut management](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-navigating-managing/shortcuts-manager/shortcuts-manage): use native remappable shortcuts and resolve binding conflicts.
