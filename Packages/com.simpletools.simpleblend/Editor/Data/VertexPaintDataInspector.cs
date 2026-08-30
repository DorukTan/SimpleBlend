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
                EditorGUILayout.EnumPopup("Storage Mode", data.StorageMode);
                EditorGUILayout.TextField("Storage Reason", data.StorageDescription);
                if (!string.IsNullOrEmpty(data.FallbackAssetPath))
                {
                    EditorGUILayout.TextField("Generated Asset", data.FallbackAssetPath);
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
                "Checkpoint C paints the red channel with a fixed 1.25-unit brush. "
                + "The Scene view preview is editor-only and never replaces renderer materials.",
                MessageType.None);
        }

        private void Initialize(VertexPaintData data)
        {
            var renderer = data.GetComponent<Renderer>();
            Undo.RegisterCompleteObjectUndo(data, "Initialize SimpleBlend Vertex Painting");
            var result = VertexPaintDataAuthoring.Initialize(data, renderer);
            if (result.IsValid)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }

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

            Undo.RegisterCompleteObjectUndo(data, "Clear SimpleBlend Vertex Paint");
            VertexPaintDataAuthoring.Clear(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            _lastOperationMessage = "Paint data cleared. The source mesh was not modified.";
            _lastOperationMessageType = MessageType.Info;
        }

        private void Report(VertexPaintValidationResult result)
        {
            _lastOperationMessage = result.Message;
            _lastOperationMessageType = result.IsValid ? MessageType.Info : MessageType.Error;
            Repaint();
        }
    }
}
