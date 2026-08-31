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

        public override void OnInspectorGUI()
        {
            var data = (VertexPaintData)target;
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUILayout.LabelField("SimpleBlend Vertex Paint", EditorStyles.boldLabel);
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

            var validationType = data.ValidationFailure == VertexPaintValidationFailure.None
                ? MessageType.Info
                : MessageType.Error;
            EditorGUILayout.HelpBox(data.ValidationMessage, validationType);

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

                if (GUILayout.Button("Reapply Paint Data"))
                {
                    Report(VertexPaintDataAuthoring.Apply(data));
                }

                if (GUILayout.Button("Clear Paint Data"))
                {
                    Clear(data);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Use the SimpleBlend Brush Scene view overlay for channels, modes, flow, fill, and masks. "
                + "Prefab instances fork shared fallback assets on their first local stroke; Prefab Mode edits the asset intentionally.",
                MessageType.None);
        }

        private void Initialize(VertexPaintData data)
        {
            var renderer = data.GetComponent<Renderer>();
            var result = VertexPaintSetupUtility.Enable(renderer, null, out _);
            Report(result);
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
