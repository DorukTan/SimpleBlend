using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed partial class SimpleBlendWorkspace : EditorWindow
    {
        private readonly SimpleBlendBrushControls _brush = new SimpleBlendBrushControls();
        private PaintWorkspaceTarget _target;
        private Vector2 _scroll;
        private bool _meshDetails;
        private bool _materialDetails;
        private int _subdivisionLevels = 3;
        private string _operationMessage;
        private MessageType _operationType;
        [SerializeField] private SurfacePalette _palette;
        [SerializeField] private bool _useSurfaces = true;
        private int _materialSection;

        [MenuItem("Window/SimpleBlend/Workspace", false, 0)]
        internal static void Open()
        {
            GetWindow<SimpleBlendWorkspace>().Show();
        }

        private void OnEnable()
        {
            titleContent = SimpleBlendEditorIcons.Content("Blend", "SimpleBlend", "Set up and paint mesh objects.");
            minSize = new Vector2(320, 320);
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.projectChanged += Refresh;
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += Refresh;
            ToolManager.activeToolChanged += Repaint;
            SimpleBlendBrushSettings.Changed += Repaint;
            SimpleBlendPaintFeedback.Changed += Repaint;
            SurfacePaletteCreator.Created += OnPaletteCreated;
            SimpleBlendTargetLock.Changed += Refresh;
            SimpleBlendTexturePaintTool.Changed += Repaint;
            Refresh();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Undo.undoRedoPerformed -= Refresh;
            ToolManager.activeToolChanged -= Repaint;
            SimpleBlendBrushSettings.Changed -= Repaint;
            SimpleBlendPaintFeedback.Changed -= Repaint;
            SurfacePaletteCreator.Created -= OnPaletteCreated;
            SimpleBlendTargetLock.Changed -= Refresh;
            SimpleBlendTexturePaintTool.Changed -= Repaint;
            // The Scene tool owns its lifetime; closing a settings window must not take another tool's input.
        }

        private void OnSelectionChanged()
        {
            if (_texturePainting && SimpleBlendTexturePaintTool.IsActiveFor(SimpleBlendTexturePaintTool.Target)
                && SimpleBlendTexturePaintTool.KeepTargetForSelection(SimpleBlendTexturePaintTool.Target, Selection.objects))
            { Refresh(); return; }
            if (SimpleBlendTargetLock.IsLocked) { Refresh(); return; }
            if (_useSurfaces) StopForSurfaceConfiguration();
            _operationMessage = null;
            _materialSection = 0;
            Refresh();
        }

        private void OnPaletteCreated(SurfacePalette palette)
        {
            StopForSurfaceConfiguration();
            _palette = palette;
            _useSurfaces = true;
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => Refresh();

        private void Refresh()
        {
            _target = InspectTarget();
            Repaint();
        }

        private static PaintWorkspaceTarget InspectTarget() => PaintWorkspaceTarget.Inspect(
            SimpleBlendTargetLock.IsLocked ? new Object[] { SimpleBlendTargetLock.Data } : Selection.objects);

        private void OnFocus() => Refresh();

        private void OnGUI()
        {
            if (_target == null) Refresh();
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.4f, 112, 160);
            try
            {
                if (DrawPaintBackend()) return;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label(SimpleBlendEditorIcons.Content("Paint", " Paint", "Paint mesh vertices."), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
                }
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Paint object", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(_target.Renderer, typeof(Renderer), true);
                    EditorGUILayout.HelpBox(_target.Message,
                        _target.State == PaintWorkspaceState.Invalid ? MessageType.Error : MessageType.Info);
                    DrawPrimaryAction();
                    if (SimpleBlendTargetLock.IsLocked)
                    {
                        EditorGUILayout.HelpBox(SimpleBlendTargetLock.Data == null
                            ? "The locked target is unavailable. Unlock before choosing another object."
                            : "Paint target locked. Hierarchy selection stays usable; the brush still targets the object shown here.", MessageType.Info);
                        if (GUILayout.Button("Unlock paint target")) SimpleBlendPaintTool.UnlockTarget();
                    }
                    else if (_target.CanPaint && GUILayout.Button("Lock paint target")) SimpleBlendTargetLock.Lock(_target.Data);
                }
                if (!string.IsNullOrEmpty(_operationMessage)) EditorGUILayout.HelpBox(_operationMessage, _operationType);
                if (_target.CanPaint)
                {
                    if (_target.IsSparse)
                        EditorGUILayout.HelpBox("Small strokes may only reach this mesh's corners. Open Mesh detail to make a denser copy, or keep the existing vertices.", MessageType.Warning);
                    EditorGUI.BeginChangeCheck();
                    _useSurfaces = EditorGUILayout.Popup("Paint workflow", _useSurfaces ? 0 : 1,
                        new[] { "Named surfaces", "Direct channels" }) == 0;
                    if (EditorGUI.EndChangeCheck())
                    {
                        StopForSurfaceConfiguration();
                        if (!_useSurfaces) UseDirectChannels();
                    }
                    var showBrush = !_useSurfaces || DrawSurfacePalette();
                    if (showBrush)
                    {
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                            _brush.Draw(_useSurfaces);
                            SimpleBlendPaintFeedback.Draw(_target.Data);
                        }
                    }
                    DrawMeshDetails();
                    DrawMaterialDetails();
                    EditorGUILayout.HelpBox("Save the scene or prefab to keep your paint. Undo restores a complete stroke; the original mesh is unchanged.", MessageType.None);
                }
                if (_target.Data != null && _target.State == PaintWorkspaceState.Invalid
                    && GUILayout.Button("Inspect paint data")) Selection.activeObject = _target.Data;
                EditorGUILayout.EndScrollView();
            }
            finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
        }

        private void DrawPrimaryAction()
        {
            var active = ToolManager.activeToolType == typeof(SimpleBlendPaintTool);
            if (active)
            {
                if (GUILayout.Button("Stop painting", GUILayout.Height(28)))
                    ToolManager.RestorePreviousTool();
                return;
            }
            var label = _target.CanPrepare ? "Set up painting" : "Start painting";
            var surfaceProblem = StartProblem(_target, _useSurfaces, _palette, _materialSection, SimpleBlendBrushSettings.instance);
            if (_target.CanPaint && !string.IsNullOrEmpty(surfaceProblem))
                EditorGUILayout.HelpBox(surfaceProblem, MessageType.Info);
            using (new EditorGUI.DisabledScope((!_target.CanPrepare && !_target.CanPaint) || !string.IsNullOrEmpty(surfaceProblem)))
            {
                if (!GUILayout.Button(label, GUILayout.Height(28))) return;
                if (_target.CanPrepare)
                {
                    var result = PaintWorkspaceTarget.Prepare(Selection.objects, null, out _);
                    _operationMessage = result.IsValid ? "Painting is set up. Start painting when you're ready." : result.Message;
                    _operationType = result.IsValid ? MessageType.Info : MessageType.Error;
                }
                else
                {
                    var current = InspectTarget();
                    if (current.CanPaint) ToolManager.SetActiveTool(typeof(SimpleBlendPaintTool));
                    else { _operationMessage = current.Message; _operationType = MessageType.Error; }
                }
                Refresh();
            }
            if (_target.CanPaint && _useSurfaces && !string.IsNullOrEmpty(surfaceProblem))
            {
                if (_palette == null && GUILayout.Button("Create surface palette")) SurfacePaletteCreator.Open();
                if (GUILayout.Button("Use Direct channels (no palette)"))
                {
                    UseDirectChannels();
                    Repaint();
                }
            }
        }

        private void DrawMeshDetails()
        {
            _meshDetails = EditorGUILayout.Foldout(_meshDetails, "Mesh detail", true);
            if (!_meshDetails) return;
            EditorGUILayout.LabelField($"{_target.Mesh.vertexCount:N0} vertices · {_target.Mesh.subMeshCount} mesh sections");
            _subdivisionLevels = EditorGUILayout.IntSlider("Detail levels", _subdivisionLevels, 1, 4);
            EditorGUILayout.HelpBox("Creates a renderer-only copy in the same place, with interpolated paint and UVs. Originals stay unchanged. Children, colliders and scripts are not copied. Move or hide one copy to avoid overlap. Undo removes the object; its saved mesh asset remains.", MessageType.None);
            var reason = _target.Renderer is SkinnedMeshRenderer
                ? "Denser copies currently support static meshes only."
                : PaintMeshSubdivision.UnsupportedReason(_target.Mesh);
            if (!string.IsNullOrEmpty(reason)) EditorGUILayout.HelpBox(reason, MessageType.Info);
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(reason)))
            {
                if (!GUILayout.Button("Make a more detailed paint copy")) return;
                var path = EditorUtility.SaveFilePanelInProject("Save paintable mesh copy", "SubdividedPaintMesh", "asset", "Choose a new mesh asset. Existing assets are never overwritten.");
                if (string.IsNullOrEmpty(path)) return;
                try
                {
                    var current = InspectTarget();
                    if (!current.CanPaint) throw new System.InvalidOperationException(current.Message);
                    var copy = PaintMeshSubdivision.CreatePaintableCopy(current.Data, _subdivisionLevels, path);
                    SimpleBlendPaintTool.UnlockTarget();
                    Selection.activeGameObject = copy;
                    _operationMessage = "Copy created. Move or hide one overlapping object, then start painting.";
                    _operationType = MessageType.Info;
                }
                catch (System.Exception exception)
                {
                    _operationMessage = exception.Message;
                    _operationType = MessageType.Error;
                }
                Refresh();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawMaterialDetails()
        {
            _materialDetails = EditorGUILayout.Foldout(_materialDetails, "Materials on this object", true);
            if (!_materialDetails) return;
            EditorGUILayout.HelpBox("Channel painting writes vertex values, not material tints. Named surfaces uses a palette and an explicitly assigned Layer Blend material. Custom shaders can use Direct channels. Materials are never assigned automatically.", MessageType.None);
            var materials = _target.Renderer.sharedMaterials;
            using (new EditorGUI.DisabledScope(true))
                for (var i = 0; i < materials.Length; i++)
                    EditorGUILayout.ObjectField($"Material slot {i}", materials[i], typeof(Material), false);
        }

        private string SurfaceWorkflowProblem(bool requireBrush = false)
        {
            return StartProblem(_target, true, _palette, _materialSection, SimpleBlendBrushSettings.instance, requireBrush);
        }

        internal static string StartProblem(PaintWorkspaceTarget target, bool useSurfaces, SurfacePalette palette,
            int section, SimpleBlendBrushSettings settings, bool requireBrush = true)
        {
            if (target == null) return "Select a scene mesh object first.";
            if (!target.CanPaint) return target.CanPrepare ? string.Empty : target.Message;
            if (!useSurfaces) return string.Empty;
            if (palette == null) return "Named surfaces needs a surface palette. Create one here, or choose an existing palette under Choose a surface. Direct channels paints vertex values without a palette.";
            if (!string.IsNullOrEmpty(palette.Problem)) return palette.Problem;
            if (target.Mesh == null || section < 0 || section >= target.Mesh.subMeshCount)
                return "Choose an existing mesh section.";
            if (!SurfacePaletteAuthoring.IsAssigned(target.Renderer, section, palette))
                return "Assign this palette's material to the chosen mesh section before using its surface names.";
            if (requireBrush)
            {
                for (var i = 0; i < palette.SurfaceCount; i++)
                    if (SurfacePaletteAuthoring.Matches(palette, i, section, settings)) return string.Empty;
                return "Choose a named surface before starting the brush.";
            }
            return string.Empty;
        }

        private bool DrawSurfacePalette()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Choose a surface", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _palette = (SurfacePalette)EditorGUILayout.ObjectField("Surface palette", _palette, typeof(SurfacePalette), false);
                if (EditorGUI.EndChangeCheck()) StopForSurfaceConfiguration();
                if (GUILayout.Button("Create surface palette")) SurfacePaletteCreator.Open();
                var sections = new string[_target.Mesh.subMeshCount];
                for (var i = 0; i < sections.Length; i++) sections[i] = $"Section {i} · material slot {i}";
                _materialSection = Mathf.Clamp(_materialSection, 0, sections.Length - 1);
                EditorGUI.BeginChangeCheck();
                _materialSection = EditorGUILayout.Popup("Mesh section", _materialSection, sections);
                if (EditorGUI.EndChangeCheck()) StopForSurfaceConfiguration();
                var problem = SurfaceWorkflowProblem();
                if (!string.IsNullOrEmpty(problem))
                {
                    EditorGUILayout.HelpBox(problem, MessageType.Info);
                    using (new EditorGUI.DisabledScope(_palette == null || !string.IsNullOrEmpty(_palette.Problem)))
                    {
                        if (GUILayout.Button("Assign palette material to this slot"))
                        {
                            try
                            {
                                SurfacePaletteAuthoring.Assign(_target.Renderer, _materialSection, _palette);
                                _operationMessage = "Material assigned. Choose a named surface, then start painting. Undo restores the previous material slot.";
                                _operationType = MessageType.Info;
                            }
                            catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
                            Refresh();
                        }
                    }
                    return false;
                }
                var matched = false;
                for (var i = 0; i < _palette.SurfaceCount; i++) matched |= DrawSurfaceRow(i);
                if (!matched)
                    EditorGUILayout.HelpBox("Choose a surface to set the brush. Advanced or overlay channel edits may no longer match a named choice.", MessageType.Warning);
                if (_target.Mesh.subMeshCount > 1)
                    EditorGUILayout.HelpBox("The brush targets this section's vertices. Shared vertices can also change neighboring sections. Preview section only limits the diagnostic overlay; Material view keeps all real materials visible.", MessageType.Info);
                EditorGUILayout.HelpBox("Paint moves the blend toward the selected surface and reduces competing weights. Base reveals the unpainted surface. These are blended weights, not an ordered layer stack. Alpha stays unchanged.", MessageType.None);
                for (var i = 0; i < _palette.SurfaceCount; i++)
                    if (SurfacePaletteAuthoring.Matches(_palette, i, _materialSection, SimpleBlendBrushSettings.instance))
                        EditorGUILayout.HelpBox("Selected: " + _palette.SurfaceName(i) + " · "
                            + SurfaceOperationDescription(i, SimpleBlendBrushSettings.instance.Mode), MessageType.Info);
                var heightContrast = _palette.Material.GetFloat("_HeightBlendStrength") > 0;
                EditorGUI.BeginChangeCheck();
                var blend = EditorGUILayout.Popup("Surface blend", heightContrast ? 1 : 0,
                    new[] { "Smooth weights", "Height contrast" });
                if (EditorGUI.EndChangeCheck()) SurfacePaletteAuthoring.SetHeightContrast(_palette, blend == 1);
                if (heightContrast)
                    EditorGUILayout.HelpBox("Height contrast sharpens transitions, even with flat height maps. Choose Smooth weights for a softer blend. This edits the shared palette material and supports Undo.", MessageType.Warning);
                return true;
            }
        }

        private bool DrawSurfaceRow(int index)
        {
            var settings = SimpleBlendBrushSettings.instance;
            var selected = SurfacePaletteAuthoring.Matches(_palette, index, _materialSection, settings);
            var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(38, 6, 3, 3) };
            var surfaceName = _palette.SurfaceName(index);
            var label = index == 0 ? (surfaceName == "Base" ? "Base (remaining weight)" : "Base · " + surfaceName)
                : $"Overlay {index} · {surfaceName}";
            if (GUI.Toggle(rect, selected, new GUIContent(label, "Sets the existing brush's channel values and mesh-section mask. Does not change any material or paint until you stroke."), style) && !selected)
            {
                SurfacePaletteAuthoring.SelectSurface(_palette, index, _materialSection, settings);
                settings.SaveSettings();
            }
            var texture = _palette.Material.GetTexture($"_Layer{index}Albedo") ?? Texture2D.whiteTexture;
            var color = GUI.color;
            var tint = _palette.Material.GetColor($"_Layer{index}Color");
            tint.a = 1;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 24, 24), texture, ScaleMode.ScaleToFit);
            GUI.color = color;
            return selected;
        }

        private static void StopForSurfaceConfiguration()
        {
            SimpleBlendTexturePaintTool.Stop();
            if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
        }

        private void UseDirectChannels()
        {
            StopForSurfaceConfiguration();
            _useSurfaces = false;
            SimpleBlendBrushSettings.instance.SurfaceWeightChannels = 0;
            SimpleBlendBrushSettings.instance.SaveSettings();
        }

        private static string SurfaceOperationDescription(int surface, VertexPaintBrushMode mode)
        {
            if (mode == VertexPaintBrushMode.Smooth) return "Smooth averages neighboring surface weights";
            if (mode == VertexPaintBrushMode.Sample) return "Sample reads values; reselect a surface before painting";
            if (mode == VertexPaintBrushMode.Erase && surface != 0) return "erase this overlay; preserve other overlay weights";
            return surface == 0 ? "reduce active overlays toward Base" : "paint toward this surface; competing weights decrease";
        }
    }
}
