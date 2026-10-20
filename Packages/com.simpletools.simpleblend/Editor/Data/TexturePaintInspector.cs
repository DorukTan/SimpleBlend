using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [CustomEditor(typeof(TexturePaintTarget))]
    internal sealed class TexturePaintTargetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var paintTarget = (TexturePaintTarget)target;
            EditorGUILayout.LabelField(SimpleBlendEditorIcons.Content("Paint", "Texture paint", "Saved surface blend mask"), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Paint asset", paintTarget.Paint, typeof(TexturePaintAsset), false);
            var problem = TexturePaintStroke.Problem(paintTarget);
            if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            EditorGUILayout.HelpBox("Paint surfaces in the SimpleBlend workspace. This object's mesh, material and mask belong to its paint asset. Duplicates share that asset.", MessageType.Info);
            if (GUILayout.Button("Open painting workspace")) SimpleBlendWorkspace.Open();
        }
    }

    [CustomEditor(typeof(TexturePaintAsset))]
    internal sealed class TexturePaintAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var paint = (TexturePaintAsset)target;
            EditorGUILayout.LabelField(SimpleBlendEditorIcons.Content("Materials", "Painted surfaces", "Persistent texture painting data"), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Owned mesh", paint.Mesh, typeof(Mesh), false);
                EditorGUILayout.ObjectField("Owned material", paint.Material, typeof(Material), false);
                EditorGUILayout.ObjectField("Blend mask", paint.Mask, typeof(Texture2D), false);
            }
            EditorGUILayout.HelpBox("Select the painted scene object to edit its mask in the workspace. Texture weights use the generated UV4 atlas; surface textures keep their original UVs.", MessageType.Info);
            if (GUILayout.Button("Open painting workspace")) SimpleBlendWorkspace.Open();
        }
    }
}
