using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class TerrainBindingWindow : EditorWindow
    {
        [SerializeField] private Terrain _terrain;
        [SerializeField] private Material _material;
        private string _lastMessage = string.Empty;
        private bool _lastSucceeded;

        [MenuItem("Window/SimpleBlend/Terrain Binding")]
        private static void Open()
        {
            GetWindow<TerrainBindingWindow>("SimpleBlend Terrain");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Terrain Material Binding", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A material stores one terrain binding. Duplicate it for meshes matching different terrains. "
                + "Bind again after changing terrain layers, tiling, parameters, or position.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _terrain, typeof(Terrain), true);
            _material = (Material)EditorGUILayout.ObjectField("Destination Material", _material, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
                _lastMessage = string.Empty;

            var ready = TerrainMaterialBinder.Validate(_terrain, _material, out var validation);
            EditorGUILayout.HelpBox(validation, ready ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("Bind / Refresh Material"))
                {
                    _lastSucceeded = TerrainMaterialBinder.TryBind(_terrain, _material, out _lastMessage);
                }
            }
            if (!string.IsNullOrEmpty(_lastMessage))
                EditorGUILayout.HelpBox(_lastMessage, _lastSucceeded ? MessageType.Info : MessageType.Error);
        }
    }
}
