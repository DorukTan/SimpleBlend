using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class SimpleBlendWorkspace : EditorWindow
    {
        private readonly SimpleBlendBrushControls _brush = new SimpleBlendBrushControls();
        private PaintWorkspaceTarget _target;
        private Vector2 _scroll;
        private bool _meshDetails;
        private bool _materialDetails;
        private int _subdivisionLevels = 3;
        private string _operationMessage;
        private MessageType _operationType;

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
            // The Scene tool owns its lifetime; closing a settings window must not take another tool's input.
        }

        private void OnSelectionChanged()
        {
            _operationMessage = null;
            Refresh();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => Refresh();

        private void Refresh()
        {
            _target = PaintWorkspaceTarget.Inspect(Selection.objects);
            Repaint();
        }

        private void OnFocus() => Refresh();

        private void OnGUI()
        {
            if (_target == null) Refresh();
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.4f, 112, 160);
            try
            {
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
                }
                if (!string.IsNullOrEmpty(_operationMessage)) EditorGUILayout.HelpBox(_operationMessage, _operationType);
                if (_target.CanPaint)
                {
                    if (_target.IsSparse)
                        EditorGUILayout.HelpBox("Small strokes may only reach this mesh's corners. Open Mesh detail to make a denser copy, or keep the existing vertices.", MessageType.Warning);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                        _brush.Draw();
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
            using (new EditorGUI.DisabledScope(!_target.CanPrepare && !_target.CanPaint))
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
                    var current = PaintWorkspaceTarget.Inspect(Selection.objects);
                    if (current.CanPaint) ToolManager.SetActiveTool(typeof(SimpleBlendPaintTool));
                    else { _operationMessage = current.Message; _operationType = MessageType.Error; }
                }
                Refresh();
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
                    var current = PaintWorkspaceTarget.Inspect(Selection.objects);
                    if (!current.CanPaint) throw new System.InvalidOperationException(current.Message);
                    var copy = PaintMeshSubdivision.CreatePaintableCopy(current.Data, _subdivisionLevels, path);
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
            EditorGUILayout.HelpBox("Channel painting writes vertex values, not material tints. Use a vertex-color-aware shader such as SimpleBlend/Layer Blend 3 or 4 to see surface blending. Materials are never assigned automatically. Named layers and palettes are a separate upcoming workflow.", MessageType.None);
            var materials = _target.Renderer.sharedMaterials;
            using (new EditorGUI.DisabledScope(true))
                for (var i = 0; i < materials.Length; i++)
                    EditorGUILayout.ObjectField($"Material slot {i}", materials[i], typeof(Material), false);
        }
    }
}
