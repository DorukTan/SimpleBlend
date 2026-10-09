using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    public sealed class SimpleBlendTerrainShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "Unpainted = bound terrain, Red/Green/Blue = mesh overlays. Alpha remains free. "
                + "Bind and refresh through Window > SimpleBlend > Terrain Binding. "
                + "Only linear terrain layer blending is supported.",
                MessageType.Info);
            materialEditor.ShaderProperty(FindProperty("_BlendTransition", properties), "Overlay Height Transition");
            materialEditor.ShaderProperty(FindProperty("_TerrainHeightScale", properties), "Terrain Base Height Influence");
            for (var index = 1; index <= 3; index++)
                SimpleBlendLayerBlendShaderGUI.DrawLayer(materialEditor, properties, index);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            foreach (var name in new[] { "_Cull", "_ReceiveShadows", "_SpecularHighlights", "_EnvironmentReflections" })
                materialEditor.ShaderProperty(FindProperty(name, properties), name.Substring(1));
            materialEditor.EnableInstancingField();
            materialEditor.RenderQueueField();
        }
    }
}
