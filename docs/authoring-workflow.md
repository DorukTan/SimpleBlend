# Authoring workflow specification

Issue: [#24](https://github.com/DorukTan/SimpleBlend/issues/24). Status: proposed interaction contract; owner screen review pending. This document does not certify implementation or release readiness.

## Direction and scope

The owner selected a Unity-native workbench on 2026-10-16: restrained styling, familiar controls and compact task-based panels, supporting editor light/dark themes. The shipping surface is a Unity editor extension, not a web application. The browser prototype is an isolated review aid; it reads/writes no Unity assets and adds no package dependency.

The subsequent SimpleTransform screenshot clarifies this direction: Unity-like custom editors, not 100% stock Unity UI. Follow [the SimpleTools visual language](simpletools-design-language.md) for custom icons, blue task tabs, grouped controls and plain-language surface selection. This supersedes an overly literal stock-control interpretation; it does not remove the native EditorTool/input and source-safety requirements.

Audience: environment artists and small teams preparing meshes, blending materials and finalizing reusable assets without repeatedly changing applications. Primary lesson: create or prepare a wall, assign a contrasting material, paint, undo, save and reopen. Success is completing the intended task safely, not discovering every feature.

Existing source evidence: `VertexPaintDataInspector.cs`, `SimpleBlendBrushOverlay.cs` and `TerrainBindingWindow.cs`. Setup, painting and terrain binding currently live in separate surfaces. Storage diagnostics precede the main paint action; layer meaning is not conveyed by thumbnails or names; brush settings expose advanced masks alongside common controls. This is a code-based assessment, not a captured visual audit.

## Proposed surface ownership

| Surface | Responsibility | Must not do |
| --- | --- | --- |
| Workbench | Target readiness, task choice, layer/material setup, advanced parameters, output/recovery | Automatically activate a scene tool or modify selection assets merely by opening |
| Scene tool/overlay | Current target and operation, active layer, radius, strength, preview, navigation hints | Maintain a second divergent copy of brush settings |
| Component Inspector | Concise saved-data status, ownership/source links, repair and workbench entry | Become the primary authoring control panel or hide validation errors |
| Material Inspector | Shader/profile capabilities and material properties | Silently reinterpret existing vertex channels |

Workbench tasks are Paint, Sculpt, Create, Materials and Output. Terrain binding is a Materials workflow, not a second competing workbench. Unimplemented capabilities are explicitly marked as planned; selecting a future mode must not imply it works today. In the eventual shipped build, unsupported actions explain their prerequisites rather than presenting nonfunctional controls.

The active target and editing scope remain visible. At most one primary next action is emphasized within a task. Common controls precede advanced foldouts. No routine success modal. Interrupt only for a destructive or ownership-changing decision that needs protected attention.

## Primary task flows

1. **Existing mesh:** select renderer -> inspect readiness -> choose existing vertices or prepare a denser copy -> choose/create material or explicitly choose direct RGBA -> activate paint -> stroke/undo -> save scene/prefab. Material setup is not mandatory for custom-shader data painting.
2. **New geometry:** choose Create -> choose wall/floor/etc. -> bounded preview -> inspect density/UV/material/output choices -> generate owned output -> keep it selected -> Paint. Cancel produces no asset or scene changes.
3. **Layer painting:** choose material section -> select named layer/thumbnail -> brush and preview -> apply gesture -> inspect saved/dirty state. Raw channels remain available but are not the default mental model.
4. **Material preparation:** manual inputs or discovered sets -> inspect grouping/profile/color-space/size -> preview output plan -> create new mask/library/material -> explicitly assign. Cleanup is separate, off by default and recoverable.
5. **Geometry authoring:** preflight ownership/capability -> prepare owned geometry if needed -> select sculpt/element/topology tool -> preview or gesture -> commit one Undo -> refresh derived data. External topology owners cannot be bypassed.
6. **Transfer/recovery:** identify source and destination -> preview correspondence and unmatched areas -> choose fallback/rejection -> create a separate result or restore a checkpoint -> save. Never force old paint onto new indices.
7. **Output:** inspect baked geometry/material/shader dependencies -> select new asset path -> optionally remove authoring data after success -> verify/reopen. A retained mesh is not proof that removed shaders still render.

## State contract

| State | Visible explanation | Permitted next action | Mutation boundary |
| --- | --- | --- | --- |
| Empty selection | Select a mesh or create one | Select target / Create | None |
| Unsupported selection | Specific reason: renderer, topology, owner or pipeline | Select supported target / documented preparation | None |
| Mixed selection | Supported/unsupported counts and per-target reasons | Narrow selection; explicit batch preflight when implemented | No partial commit |
| Unprepared target | Missing paint state/material shown separately | Initialize paint; optional owned copy; material setup or RGBA | Explicit action only |
| Sparse target | Vertex painting limitation and spacing guidance | Keep coarse paint / prepare subdivided copy | Original unchanged |
| Ready, idle | Target, material section, profile, no active tool | Activate paint or another supported task | No activation from selection alone |
| Editing | Active operation, target, layer, gesture hints | Paint/sculpt; finish/cancel gesture | One gesture = one Undo |
| Preview | Clearly not applied; Apply and Cancel | Apply / Cancel / adjust preview | No authoritative data change before Apply |
| Dirty | Explain what is unsaved and where it belongs | Save scene/prefab; keep editing | Never imply a transient stream is the save authority |
| Topology mismatch | Explain incompatible source and retained paint | Recovery/transfer preview; intentionally clear through confirmation | Reject index reuse |
| Operation error | Operation, reason and retained inputs | Retry / choose destination / cancel | Retain current valid state; atomic rollback |
| Busy | Progress, resource estimate, cancel where safe | Cancel or await bounded operation | Commit only after full validation |

Switching away during an active gesture commits or cancels it consistently before changing mode. Escape first cancels a preview or gesture; it does not clear saved paint. In Unity, ordinary dirty scenes use native save behavior: do not add a modal for every target change. The review prototype may use an explicit reset confirmation because its demo state is session-local.

Native paint behavior currently differs from the cancellation proposal above: Escape finishes the active drag and stops the tool, leaving one Undo available; it does not roll paint back automatically. #70 documents this explicitly while procedural preview cancellation remains a separate workflow. Do not present prototype cancellation as implemented paint input.

## Interaction, ranges and accessibility

- Radius is a world-space distance, strength is 0-1 and vertex alpha is a separate stored value. Explain capped versus continuous buildup in ordinary terms. Do not silently change existing accumulation behavior.
- Use the native Shortcut Manager for configurable bindings; proposed radius/strength gestures must be tested for conflicts. Alt/navigation and text entry retain ownership. Do not make an unverified shortcut a documented shipping promise.
- Name layers and material sections; channel colors are supplementary. Provide focus indicators, tooltips and useful disabled-state explanations. State changes are announced without stealing keyboard focus.
- Validate 320/440-point docked panels and both themes at normal and increased display scaling. Wrap task navigation rather than clipping labels. Keep active target and primary action reachable when sections scroll.
- Review examples include no selection, a sparse cube, one wall, a multi-material mesh, mixed supported/unsupported objects, long names and a stale topology signature. Values shown in the prototype are illustrative, not measured performance.
- Selection lock is explicit. Batch editing must preflight every target. Preview and diagnostics cannot mutate source meshes, materials, texture importers or saved palettes.

## Native implementation handoff

The full #25/#26 workbench still requires owner review of this flow and prototype. At the owner's request to continue, #66 implements a focused native foundation: a dockable direct-channel paint workspace, read-only target preflight, explicit setup/tool activation, shared brush controls and the existing mesh-copy operation. #68 adds guided create-new palette/material assets, explicit slot assignment and named texture/tint rows for the existing three/four-layer shader contracts. Neither slice is approval of the entire unseen native workflow or completion of #25. Keep the existing brush model as the shared authority. The EditorWindow hosts settings while the native EditorTool/overlay owns Scene-view gestures. Use theme-derived native controls and context-aware validation rather than hard-coded browser styles. Unity's [editor window guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-HowTo-CreateEditorWindow.html) is an implementation reference, not evidence that the prototype is native.

## Five-minute first-use script

Use a fresh disposable project and a novice participant. Do not demonstrate the controls first. Start timing when the workbench opens. Ask: “Make a wall with a second surface painted across part of it, undo one stroke, save it and show that it remains after reopening.” Allow the participant to use in-tool help. Do not count time spent resolving an unrelated Unity installation problem as interface time; report it separately.

| Observation | Current tool baseline | Proposed workflow |
| --- | --- | --- |
| Find setup / prepare target | Not measured | Not measured |
| Assign material / choose layer | Not measured | Not measured |
| First intentional stroke | Not measured | Not measured |
| Undo and recover | Not measured | Not measured |
| Save and confirm after reopen | Not measured | Not measured |
| Total time / assistance / safety errors | Not measured | Not measured |

Record completion, task time, wrong turns, assistance, source-change mistakes and participant comments. The five-minute target is a goal, not a result. #59 owns the later three-participant acceptance gate; this issue establishes the script and obtains owner prototype feedback.

## Review record

- Confirmed: Unity-native visual direction; light/dark themes; compact task-based panels.
- Prepared for review: state contract and task flows.
- Browser checks: 55 interaction/overflow assertions passed, with 320/440-point dock captures in light/dark plus a small-browser capture. This verifies the prototype, not native Unity layout or rendering. The mechanical design pass found insufficient primary-button contrast, which was corrected; native compact typography is deliberate rather than adopting a web display-heading scale.
- SimpleTools reference follow-up: custom blue icon-and-label tabs, grouped brush controls and compact surface chips now follow the owner's SimpleTransform screenshot. The existing native Inspector puts painting/setup first with mesh and data foldouts. Seven original icons were added; 189 isolated Unity tests passed, including importer/texture/toolbar/window checks. Native visual review is not yet recorded.
- Review revisions: labeled unmodeled section/channel previews explicitly; added illustrative per-channel stroke colors; added keyboard focus handoffs; exposed both coarse-vertex and subdivided-copy choices. These are prototype corrections, not implemented Unity features.
- Read-only verdict pass: the reviewer scored those three findings resolved and cleared the browser review aid within that scope. The reviewer inspected source, test definitions and five captures, but did not rerun the tests. This is neither whole-product certification nor owner screen approval.
- Pending: actual prototype layout/interaction approval at both dock widths and themes, corrections, and baseline timings.
- Native foundation (#66): implemented separately from the full prototype, initially limited to direct channels. The next slice (#68) adds named surfaces, real Albedo/Tint rows, reusable palettes and explicit material creation/assignment. The generated-wall lesson, richer previews and #26 input work remain unfinished. Native theme/dock/focus appearance and owner walkthrough are not established by editor-state tests. Corrected icon approval does not close #24.
- Foundation verification (2026-10-18): 203 isolated Unity EditMode tests passed with no failures or skips, including 14 workspace cases. Documentation checks passed for 19 files/38 local links/13 source controls/12 indexed pages; the roadmap graph and 63 icon asset checks also passed. Source review found a missing triangle-only preflight guard; the guard and tests were added and the reviewer scored that finding resolved. No native visual verdict is recorded.
- Palette follow-up verification (2026-10-18, #68): 226 isolated EditMode tests passed with no failures/skips, including 23 palette cases. Read/reimport references, no-overwrite creation, slot assignment/Undo, prefab overrides and Base/overlay strokes are covered. Documentation checks passed for 20 files/42 links/16 control labels/13 indexed pages; roadmap and 63 icon asset checks passed. Native theme/dock/focus review and the generated-wall/first-use acceptance gates remain open.
- Scene/input slice (#70): Material view leaves real renderer materials visible, channel overlays can target the masked section, and a session-only pin keeps the paint target independent of Hierarchy selection. Thirteen Scene-scoped Shortcut Manager commands are unbound by default, including temporary erase latched at stroke start. No default binding/focus conflict verdict is implied. Pointer-drag size/strength gestures, richer section/result presentation, sculpt input and native theme/focus/multi-view walkthroughs remain in #26; #25/#26 stay open.
- Scene/input verification (2026-10-18): 259 isolated EditMode tests passed with no failures/skips, including 33 new input/preview/lock cases. Documentation checks passed for 21 files/47 links/18 labels/14 indexed pages; roadmap and 63 icon checks passed. Multi-view ownership and pre-activation lock cleanup were corrected during source review and covered by regressions. The result is editor-state/policy evidence, not observed native event/rendering acceptance.
- Approval of style is not approval of unseen screens. Keep #24 open until feedback and revisions are recorded. A prototype PR references this issue rather than closing it prematurely.
- Brush adjustment follow-up (#72): two unbound clutch commands preview size or strength through a horizontal left-drag. Mouse release commits once; early key release, Escape, navigation and lost ownership cancel without changing saved settings or mesh data. Sensitivity is a per-user advanced brush option. This implements another bounded part of #26; native binding, focus and multi-view event acceptance remains unobserved.
- Adjustment verification (2026-10-19): 280 isolated EditMode tests passed with no failures/skips, including 21 new session/settings/cleanup cases. Documentation, roadmap and 63 icon checks passed. No desktop automation was used. The input milestone and owner-observed native acceptance remain open.
