using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleBlend.Editor
{
    [Overlay(typeof(SceneView), OverlayId, "SimpleBlend Brush", defaultDisplay = true)]
    internal sealed class SimpleBlendBrushOverlay : Overlay
    {
        internal const string OverlayId = "SimpleBlend.BrushOverlay";
        private readonly SimpleBlendBrushControls _controls = new SimpleBlendBrushControls();

        public override VisualElement CreatePanelContent()
        {
            return new IMGUIContainer(DrawSettings) { name = "SimpleBlend Brush Settings" };
        }

        private void DrawSettings()
        {
            var selected = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<VertexPaintData>();
            if (selected != null && selected.SourceMesh != null && selected.SourceMesh.vertexCount <= 64)
                EditorGUILayout.HelpBox("Sparse mesh: strokes only affect existing vertices. Open Mesh detail in the workspace or Inspector to make a denser copy.", MessageType.Warning);
            _controls.Draw();
            if (GUILayout.Button("Open workspace")) SimpleBlendWorkspace.Open();
        }
    }
}
