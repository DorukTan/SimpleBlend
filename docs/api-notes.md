# SimpleBlend Phase 0 API notes

Date verified: 2026-08-28

## Installed versions

- Unity Editor: `6000.4.8f1` (`f8b72d3d7343`)
- Universal Render Pipeline: `17.4.0`
- Shader Graph: `17.4.0`
- Unity Test Framework: `1.6.0`
- Active desktop URP asset: `Assets/Settings/PC_RPAsset.asset`

The build brief names Unity 6.3 LTS and Unity 6.5, but neither is the installed project version. Initial development and any claims made from this workspace must therefore say **tested on Unity 6000.4.8f1 with URP 17.4.0**. Unity 6.3 and 6.5 remain compatibility targets until they are run separately; they must not be described as tested.

## Package identity correction

The source brief still contains placeholder Vertex Brush naming. For this project the proposed identity is:

- Package ID: `com.simpletools.simpleblend`
- Display name: `SimpleBlend`
- Root namespaces: `SimpleBlend` and `SimpleBlend.Editor`

This identity is a proposal pending the implementation go-ahead. It avoids carrying the old `VertexBrush` name into serialized types and shader paths.

## EditorTool, ToolManager, and overlays

Verified from the installed `UnityEditor.CoreModule` XML documentation and reflected public signatures:

- `UnityEditor.EditorTools.EditorTool` is the supported base class.
- Its public extension points include `OnActivated()`, `OnWillBeDeactivated()`, `OnToolGUI(EditorWindow)`, `IsAvailable()`, `PopulateMenu(DropdownMenu)`, and `toolbarIcon`.
- `EditorToolAttribute` in 6000.4.8f1 has constructors beginning with `(string displayName, Type componentToolTarget)`. A component-aware tool can therefore register against `MeshRenderer`; a second registration or global strategy is required for `SkinnedMeshRenderer`.
- `ToolManager` publicly exposes `activeToolType`, active-tool change events, `SetActiveTool`, `RestorePreviousTool`, and `RefreshAvailableTools`.
- `UnityEditor.Overlays.Overlay` is the supported persistent Scene view panel surface. `CreatePanelContent()` returns UI Toolkit content.
- `OverlayAttribute` accepts `SceneView` as the host window type and exposes stable ID, display name, default visibility, docking, dimensions, group, and priority.

Implementation consequence: use an `EditorTool` for Scene view input and a small Scene view `Overlay` for settings. The overlay is not a replacement for the tool. During layout/input, claim the passive control with `HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive))` so paint clicks do not select objects.

## MeshRenderer.additionalVertexStreams

Installed API description: vertex attributes in the additional mesh override or add attributes on the primary mesh for that `MeshRenderer`.

A disposable Unity batch-mode probe observed the following on 6000.4.8f1:

1. The renderer reference serializes in scene YAML as `m_AdditionalVertexStreams`.
2. Two renderers sharing the same source mesh can hold different additional-stream mesh references.
3. Assigning those streams did not add colors to or otherwise mutate the shared source mesh.
4. A generated, non-asset stream is not a durable data store. Durable color data must remain on `VertexPaintData`, and the runtime/editor component must reconstruct and assign the transient stream on enable or validation.
5. The stream must match the source vertex count. A changed source vertex count is a hard validation failure; data must not be applied by index to a different topology.

This validates the primary per-renderer path for ordinary `MeshRenderer` objects that are not statically batched.

## Static batching correction

The same probe assigned red and blue streams to two renderers sharing one color-free source mesh, marked both `BatchingStatic`, and called the public `StaticBatchingUtility.Combine` API.

Observed result:

- Both renderers became part of a static batch.
- Both `additionalVertexStreams` references were cleared (`retained=False`).
- Unity produced a combined mesh with six color entries for the two three-vertex source instances.

Conclusion: static batching consumes the additional streams into the combined mesh. It is valid as an input to a batch build, but it is not a live, editable per-renderer stream after the batch is created.

Architecture correction: **objects marked for static batching must use the explicit cloned-mesh asset fallback for durable painted output**, or the tool must remove static batching while editing and rebuild it later. SimpleBlend should choose the explicit fallback because it is inspectable, survives builds, and does not rely on hidden rebatching timing. The inspector must state `Cloned Mesh — required by static batching`.

## SRP Batcher

The installed SRP Batcher contract concerns shader material constant-buffer layout. Additional vertex streams change mesh vertex input, not `UnityPerMaterial` values, so the two systems are not inherently exclusive.

Implementation requirements:

- Put all material properties in one `CBUFFER_START(UnityPerMaterial)` block.
- Keep the block layout identical across shader variants.
- Confirm `SRP Batcher: compatible` in the material inspector and confirm batch behavior in the Frame Debugger during the manual acceptance pass.

The static-batching result above is separate from SRP Batcher compatibility.

## GPU Resident Drawer

The active PC URP asset currently has `m_GPUResidentDrawerMode: 0` (disabled), so the current project does not exercise this rendering path.

Installed URP 17.4.0 exposes `UniversalRenderPipelineAsset.gpuResidentDrawerMode` publicly. Unity's compatibility documentation requires a `MeshRenderer`, static GI, a DOTS-instancing-compatible shader, no `MaterialPropertyBlock`, and no per-instance rendering callbacks. It does not document `additionalVertexStreams` either as supported or unsupported.

The installed Core RP package contains an internal `DisallowGPUDrivenRendering` component that changes an internal renderer flag. It is not a supported public package API, and using reflection to reach it would violate the brief.

Architecture correction: when the active URP asset enables GPU Resident Drawer, select the cloned-mesh fallback unless a later rendering acceptance test proves the transient stream path on every supported Unity version. The custom shaders should still support DOTS instancing so cloned meshes remain renderable through the GPU Resident Drawer, although unique painted meshes reduce instancing opportunities.

Official reference: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html

## URP Lit and vertex color

Verified directly in installed URP 17.4.0 source:

- `Shaders/LitForwardPass.hlsl` declares position, normal, tangent, UV0, static-lightmap UV, and dynamic-lightmap UV inputs.
- It declares no `COLOR` semantic and does not pass vertex color to the fragment stage.

The assumption in the brief is correct: stock URP Lit does not consume painted vertex color. SimpleBlend must ship its own URP shaders and visualization shader.

## Undo.RegisterCompleteObjectUndo

Installed API behavior:

- `RegisterCompleteObjectUndo(Object, string)` stores a complete snapshot immediately.
- The array overload creates one undo operation for all supplied objects.
- Registering a scene object marks the scene modified even if no later state changes occur.
- Transform parenting, component creation, and destruction need their dedicated Undo APIs.

A disposable batch-mode probe registered one snapshot of a `40,000`-entry `Color32[]`, changed one element, and observed exact undo and redo restoration.

Implementation consequence: register the `VertexPaintData` component once on mouse-down, mutate for the complete drag, mark it dirty and record prefab modifications when the stroke ends, then collapse the stroke's undo group. Never snapshot on mouse-move. The cost is proportional to the full serialized component, so color storage should remain compact (`Color32[]`) and unrelated state should not be duplicated into the same snapshot.

Official reference: https://docs.unity3d.com/current/ScriptReference/Undo.RegisterCompleteObjectUndo.html

## Storage-mode decision after Phase 0

| Renderer / project state | Storage mode | Reason |
|---|---|---|
| Ordinary `MeshRenderer`, GPU Resident Drawer disabled, not static-batched | Additional vertex stream | Per-renderer, source-safe, prefab-serializable color data |
| `SkinnedMeshRenderer` | Cloned mesh asset | No `additionalVertexStreams` property |
| `BatchingStatic` or already part of a static batch | Cloned mesh asset | Static combine consumes and clears stream references |
| GPU Resident Drawer enabled | Cloned mesh asset until explicitly proven | Stream interaction is undocumented; public opt-out is unavailable |
| Source vertex count changed | Refuse to apply | Prevent index-based paint corruption |

The mode must be visible in the inspector, with the reason and output asset path. Automatic fallback must never overwrite the imported source asset.

## Remaining manual verification gates

These cannot be honestly claimed from headless API probing and remain acceptance-test work after implementation:

- Scene view tool palette presence, overlay behavior, surface-projected brush disc, and input capture.
- Prefab instance overrides and Prefab Mode persistence.
- Visual parity across edit mode, play mode, and player build.
- Frame Debugger confirmation for SRP Batcher and GPU Resident Drawer modes.
- Shader Graph subgraph import and parity with the hand-written HLSL shaders.
- 40k-vertex interaction profiling in an interactive editor.

## Phase 0 disposition

Proceed with the brief after applying these corrections:

1. Rename the package and namespaces to SimpleBlend.
2. State 6000.4.8f1 / URP 17.4.0 as the tested baseline from this machine.
3. Extend cloned-mesh fallback to static batching and conservatively to GPU Resident Drawer.
4. Keep `additionalVertexStreams` as the primary ordinary `MeshRenderer` path.
5. Ship custom URP shaders; stock URP Lit is not sufficient.

## M2 implementation observations

Verified during the `0.1.0-dev.2` data-safety slice on 2026-08-30:

- `Mesh.MeshData.GetColors(NativeArray<Color32>)` returned the complete color channel after `UploadMeshData(true)` made a procedural source non-readable.
- Read-only MeshData plus writable MeshData copied all ordinary vertex streams, index buffers, index format, and submesh descriptors into a readable generated mesh. Bind poses, bone weights, and blend shapes were copied through their dedicated public Mesh APIs.
- A color-only transient Mesh created with `SetVertexBufferParams` and an `UNorm8` Color descriptor worked as `MeshRenderer.additionalVertexStreams`; it did not require duplicate positions or indices.
- `UniversalRenderPipelineAsset.gpuResidentDrawerMode` is public in URP 17.4, but its public enum is defined by the separate `Unity.RenderPipelines.GPUDriven.Runtime` assembly. The Editor asmdef must reference both assemblies.
- The MeshData boundary requires the `Unity.Collections` assembly. SimpleBlend uses NativeArray only for public mesh-data transfer; it does not call Burst APIs or add Burst as a package dependency. URP 17 already supplies Collections through Render Pipelines Core.
- The imported URP sample `UVSphere.fbx` remained SHA-256 byte-identical before and after two renderers received independent red and blue streams: `42ee416f3a28f520588bb1254d60c991a63e9dfbf03046e9565c7d5fe39a3306`.
- Reimporting a generated OBJ fixture from three to four vertices produced `VertexCountMismatch`, detached the stale stream, and left the serialized colors available for an explicit clear or future migration workflow.
