using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [CustomEditor(typeof(VertexPaintData))]
    internal sealed class VertexPaintDataInspector : UnityEditor.Editor
    {
        private string _lastOperationMessage = string.Empty;
        private MessageType _lastOperationMessageType = MessageType.Info;
        private int _subdivisionLevels = 3;
        private bool _showMeshDetail;
        private bool _showDataDetails;

        public override void OnInspectorGUI()
        {
            var data = (VertexPaintData)target;
            serializedObject.UpdateIfRequiredOrScript();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(SimpleBlendEditorIcons.Load("Blend"), GUILayout.Width(22), GUILayout.Height(22));
                EditorGUILayout.LabelField("Simple Blend", EditorStyles.boldLabel);
            }

            var validationType = data.ValidationFailure == VertexPaintValidationFailure.None
                ? MessageType.Info
                : MessageType.Error;
            var message = data.ValidationFailure != VertexPaintValidationFailure.None
                ? data.ValidationMessage
                : data.SourceMesh == null
                    ? "Set up painting for this object. Your original mesh stays unchanged."
                    : "Ready to paint. Your original mesh stays unchanged.";
            EditorGUILayout.HelpBox(message, validationType);

            if (!string.IsNullOrEmpty(_lastOperationMessage))
            {
                EditorGUILayout.HelpBox(_lastOperationMessage, _lastOperationMessageType);
            }

            var renderer = data.GetComponent<Renderer>();
            var currentDecision = VertexPaintStoragePolicy.Resolve(
                renderer,
                VertexPaintStoragePolicy.IsGpuResidentDrawerEnabled());
            if (data.SourceMesh != null
                && currentDecision.Mode != VertexPaintStorageMode.Unresolved
                && (currentDecision.Mode != data.StorageMode || currentDecision.Reason != data.StorageReason))
            {
                EditorGUILayout.HelpBox(
                    $"Renderer settings now require {currentDecision.Mode}: "
                    + VertexPaintText.For(currentDecision.Reason),
                    MessageType.Warning);
                if (GUILayout.Button("Update Storage Policy"))
                {
                    UpdateStoragePolicy(data);
                }
            }

            EditorGUILayout.Space();
            if (data.SourceMesh == null)
            {
                if (GUILayout.Button("Initialize Vertex Painting"))
                {
                    Initialize(data);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(!SimpleBlendPaintTool.IsSupportedTarget(data)))
                {
                    if (GUILayout.Button("Activate SimpleBlend Paint Tool"))
                    {
                        ToolManager.SetActiveTool(typeof(SimpleBlendPaintTool));
                    }
                }

                EditorGUILayout.HelpBox(
                    "Choose your brush and painting channel in the SimpleBlend Brush panel in the Scene view.",
                    MessageType.None);
                if (data.SourceMesh.vertexCount <= 64)
                {
                    EditorGUILayout.HelpBox(
                        "This mesh has few vertices, so small strokes may only affect its corners. "
                        + "Open Mesh detail to make a more detailed copy, or keep painting with the existing vertices.",
                        MessageType.Warning);
                }

                _showMeshDetail = EditorGUILayout.Foldout(_showMeshDetail, "Mesh detail", true);
                if (_showMeshDetail) DrawMeshPreparation(data);
            }

            EditorGUILayout.Space();
            _showDataDetails = EditorGUILayout.Foldout(_showDataDetails, "Source and saved-data details", true);
            if (_showDataDetails) DrawDataDetails(data);
        }

        private void DrawDataDetails(VertexPaintData data)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Source Mesh", data.SourceMesh, typeof(Mesh), false);
                EditorGUILayout.IntField("Vertex Colors", data.ColorCount);
                EditorGUILayout.IntField("Signed Vertices", data.SourceSignature.VertexCount);
                EditorGUILayout.IntField("Signed Submeshes", data.SourceSignature.SubMeshCount);
                EditorGUILayout.LongField("Topology Hash", data.SourceSignature.TopologyHash);
                EditorGUILayout.EnumPopup("Storage Mode", data.StorageMode);
                EditorGUILayout.TextField("Storage Reason", data.StorageDescription);
                if (!string.IsNullOrEmpty(data.FallbackAssetPath))
                {
                    EditorGUILayout.TextField("Generated Asset", data.FallbackAssetPath);
                    EditorGUILayout.TextField("Ownership ID", ShortOwnerId(data.FallbackOwnerId));
                }
            }

            if (data.SourceMesh != null)
            {
                if (GUILayout.Button("Reapply Paint Data")) Report(VertexPaintDataAuthoring.Apply(data));
                if (GUILayout.Button("Clear Paint Data")) Clear(data);
            }

            EditorGUILayout.HelpBox(
                "Prefab instances keep their own paint overrides. Editing in Prefab Mode changes the prefab itself. "
                + "Generated mesh assets are retained when paint is cleared or reverted.", MessageType.None);
        }

        private void Initialize(VertexPaintData data)
        {
            var renderer = data.GetComponent<Renderer>();
            var result = VertexPaintSetupUtility.Enable(renderer, null, out _);
            Report(result);
        }

        private void DrawMeshPreparation(VertexPaintData data)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Paintable Mesh Copy", EditorStyles.boldLabel);
                _subdivisionLevels = EditorGUILayout.IntSlider("Subdivision Levels", _subdivisionLevels, 1, 4);
                EditorGUILayout.HelpBox(
                    "Creates a renderer-only copy at the same position, with interpolated paint, UVs and normals. "
                    + "The original stays unchanged. Move or hide one copy to avoid overlapping surfaces. "
                    + "Colliders, children and other scripts are not copied. Undo removes the new object; the saved mesh asset remains.",
                    MessageType.None);
                var reason = data.GetComponent<SkinnedMeshRenderer>() != null
                    ? "Skinned renderers are not supported by subdivision."
                    : PaintMeshSubdivision.UnsupportedReason(data.SourceMesh);
                if (EditorUtility.IsPersistent(data)) reason = "Open Prefab Mode or select a scene instance to create a copy.";
                if (!string.IsNullOrEmpty(reason)) EditorGUILayout.HelpBox(reason, MessageType.Info);
                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(reason)))
                {
                    if (GUILayout.Button("Create Subdivided Paintable Copy"))
                    {
                        var path = EditorUtility.SaveFilePanelInProject(
                            "Save Subdivided Mesh", "SubdividedPaintMesh", "asset", "Choose a new mesh asset. Existing assets are never overwritten.");
                        if (string.IsNullOrEmpty(path)) return;
                        try
                        {
                            var copy = PaintMeshSubdivision.CreatePaintableCopy(data, _subdivisionLevels, path);
                            Selection.activeGameObject = copy;
                            ToolManager.SetActiveTool(typeof(SimpleBlendPaintTool));
                        }
                        catch (System.Exception exception)
                        {
                            _lastOperationMessage = exception.Message;
                            _lastOperationMessageType = MessageType.Error;
                        }
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void Clear(VertexPaintData data)
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear SimpleBlend Paint Data?",
                    "This removes the serialized vertex colors from this component. "
                    + "The imported source mesh and any generated fallback asset are not deleted.",
                    "Clear Paint Data",
                    "Cancel"))
            {
                return;
            }

            var meshOwner = VertexPaintDataAuthoring.GetRendererMeshOwner(data);
            if (meshOwner == null)
            {
                Undo.RegisterCompleteObjectUndo(data, "Clear SimpleBlend Vertex Paint");
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(
                    new Object[] { data, meshOwner },
                    "Clear SimpleBlend Vertex Paint");
            }

            VertexPaintDataAuthoring.Clear(data);
            VertexPaintDataAuthoring.RecordPrefabModifications(data);
            _lastOperationMessage = "Paint data cleared. The source mesh was not modified.";
            _lastOperationMessageType = MessageType.Info;
        }

        private void UpdateStoragePolicy(VertexPaintData data)
        {
            const string undoName = "Update SimpleBlend Storage Policy";
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var meshOwner = VertexPaintDataAuthoring.GetRendererMeshOwner(data);
            if (meshOwner == null)
            {
                Undo.RegisterCompleteObjectUndo(data, undoName);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { data, meshOwner }, undoName);
            }

            var result = VertexPaintDataAuthoring.EnsureCurrentStorageForPaint(data, null, out var changed);
            if (result.IsValid && changed)
            {
                VertexPaintDataAuthoring.RecordPrefabModifications(data);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Report(result);
        }

        private void Report(VertexPaintValidationResult result)
        {
            _lastOperationMessage = result.Message;
            _lastOperationMessageType = result.IsValid ? MessageType.Info : MessageType.Error;
            Repaint();
        }

        private static string ShortOwnerId(string ownerId)
        {
            return string.IsNullOrEmpty(ownerId)
                ? "Missing — next paint will repair ownership"
                : ownerId.Substring(0, Mathf.Min(12, ownerId.Length));
        }
    }
}
