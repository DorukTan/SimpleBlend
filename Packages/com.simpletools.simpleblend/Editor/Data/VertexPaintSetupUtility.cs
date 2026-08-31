using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class VertexPaintSetupUtility
    {
        private const string UndoName = "Enable SimpleBlend Vertex Painting";

        [MenuItem("GameObject/SimpleBlend/Enable Vertex Painting", false, 30)]
        private static void EnableSelected()
        {
            var renderer = Selection.activeGameObject == null
                ? null
                : Selection.activeGameObject.GetComponent<Renderer>();
            var result = Enable(renderer, null, out var data);
            if (!result.IsValid)
            {
                Debug.LogWarning($"SimpleBlend: {result.Message}", renderer);
                return;
            }

            Selection.activeObject = data.gameObject;
            if (SimpleBlendPaintTool.IsSupportedTarget(data))
            {
                ToolManager.SetActiveTool(typeof(SimpleBlendPaintTool));
            }
        }

        [MenuItem("GameObject/SimpleBlend/Enable Vertex Painting", true)]
        private static bool CanEnableSelected()
        {
            return Selection.activeGameObject != null
                   && IsSupportedRenderer(Selection.activeGameObject.GetComponent<Renderer>());
        }

        [MenuItem("CONTEXT/Renderer/Enable SimpleBlend Vertex Painting")]
        private static void EnableFromRendererContext(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            var result = Enable(renderer, null, out _);
            if (!result.IsValid)
            {
                Debug.LogWarning($"SimpleBlend: {result.Message}", renderer);
            }
        }

        internal static VertexPaintValidationResult Enable(
            Renderer renderer,
            bool? gpuResidentDrawerEnabled,
            out VertexPaintData data)
        {
            data = null;
            if (!IsSupportedRenderer(renderer))
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            data = renderer.GetComponent<VertexPaintData>();
            var meshOwner = GetRendererMeshOwner(renderer);
            if (meshOwner != null)
            {
                Undo.RegisterCompleteObjectUndo(meshOwner, UndoName);
            }

            if (data == null)
            {
                data = (VertexPaintData)Undo.AddComponent(renderer.gameObject, typeof(VertexPaintData));
                Undo.RegisterCompleteObjectUndo(data, UndoName);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(data, UndoName);
            }

            var result = VertexPaintDataAuthoring.Initialize(data, renderer, gpuResidentDrawerEnabled);
            if (!result.IsValid)
            {
                data.SetValidation(result);
                EditorUtility.SetDirty(data);
            }
            else
            {
                VertexPaintDataAuthoring.RecordPrefabModifications(data);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }

        private static bool IsSupportedRenderer(Renderer renderer)
        {
            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
        }

        private static Object GetRendererMeshOwner(Renderer renderer)
        {
            return renderer is SkinnedMeshRenderer
                ? renderer
                : renderer.GetComponent<MeshFilter>();
        }
    }
}
