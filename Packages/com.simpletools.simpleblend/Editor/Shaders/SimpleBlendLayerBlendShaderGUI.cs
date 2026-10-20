using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    public sealed class SimpleBlendLayerBlendShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null)
            {
                return;
            }

            var layerCount = SimpleBlendLayerBlendContract.GetLayerCount(material.shader.name);
            SynchronizeLayerCount(materialEditor, layerCount);

            if (material.GetFloat("_UseTextureWeights") > 0.5f)
                EditorGUILayout.HelpBox("Weights come from this object's painted blend mask in UV4, not vertex colors. Edit the mask in the SimpleBlend workspace.", MessageType.Info);
            EditorGUILayout.HelpBox(
                layerCount == 3
                    ? "Unpainted = Base, Red = Layer 1, Green = Layer 2. Alpha remains free."
                    : "Unpainted = Base, Red = Layer 1, Green = Layer 2, Blue = Layer 3. Alpha remains free.",
                MessageType.Info);
            materialEditor.ShaderProperty(
                FindProperty("_HeightBlendStrength", properties),
                new GUIContent("Height contrast", "0 uses smooth normalized weights; 1 uses Height Transition. Existing materials retain the legacy value 1."));
            materialEditor.ShaderProperty(
                FindProperty("_BlendTransition", properties),
                "Height Transition");

            for (var layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                DrawLayer(materialEditor, properties, layerIndex);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Cull");
            materialEditor.ShaderProperty(
                FindProperty("_ReceiveShadows", properties),
                "Receive Shadows");
            materialEditor.ShaderProperty(
                FindProperty("_SpecularHighlights", properties),
                "Specular Highlights");
            materialEditor.ShaderProperty(
                FindProperty("_EnvironmentReflections", properties),
                "Environment Reflections");
            materialEditor.EnableInstancingField();
            materialEditor.RenderQueueField();
        }

        internal static void DrawLayer(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            int layerIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                SimpleBlendLayerBlendContract.GetLayerLabel(layerIndex),
                EditorStyles.boldLabel);
            var prefix = $"_Layer{layerIndex}";
            var albedo = FindProperty($"{prefix}Albedo", properties);
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Albedo"),
                albedo,
                FindProperty($"{prefix}Color", properties));
            materialEditor.TextureScaleOffsetProperty(albedo);
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Normal"),
                FindProperty($"{prefix}Normal", properties),
                FindProperty($"{prefix}NormalScale", properties));
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Mask", SimpleBlendLayerBlendContract.MaskConvention),
                FindProperty($"{prefix}Mask", properties));
            materialEditor.ShaderProperty(
                FindProperty($"{prefix}Metallic", properties),
                "Metallic Multiplier");
            materialEditor.ShaderProperty(
                FindProperty($"{prefix}Smoothness", properties),
                "Smoothness Multiplier");
            materialEditor.ShaderProperty(
                FindProperty($"{prefix}OcclusionStrength", properties),
                "AO Strength");
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Height"),
                FindProperty($"{prefix}Height", properties));
        }

        private static void SynchronizeLayerCount(MaterialEditor materialEditor, int layerCount)
        {
            foreach (var target in materialEditor.targets)
            {
                if (target is Material material
                    && !Mathf.Approximately(material.GetFloat("_LayerCount"), layerCount))
                {
                    material.SetFloat("_LayerCount", layerCount);
                    EditorUtility.SetDirty(material);
                }
            }
        }
    }
}
