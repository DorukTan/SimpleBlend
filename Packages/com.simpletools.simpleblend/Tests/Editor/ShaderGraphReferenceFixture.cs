using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public static class ShaderGraphReferenceFixture
    {
        private const string Root = "Packages/com.simpletools.simpleblend/ShaderGraph/Examples";

        public static void CreateAssets()
        {
            EnsureFolder(Root + "/Textures");
            EnsureFolder(Root + "/Materials");
            var albedos = new Texture2D[4];
            var heights = new Texture2D[4];
            var colors = new[]
            {
                new Color(0.35f, 0.24f, 0.15f),
                new Color(0.15f, 0.55f, 0.2f),
                new Color(0.7f, 0.75f, 0.85f),
                new Color(0.25f, 0.3f, 0.65f)
            };
            for (var layer = 0; layer < 4; layer++)
            {
                var layerIndex = layer;
                albedos[layer] = WriteTexture("Albedo" + layer, false,
                    (x, y) => colors[layerIndex] * (0.75f + 0.05f * (x + y)));
                heights[layer] = WriteTexture("Height" + layer, true,
                    (x, y) => Color.white * ((x + y + layerIndex) / 9f));
            }
            var normal = WriteTexture("Normal", true,
                (x, y) => new Color(0.45f + 0.03f * x, 0.45f + 0.03f * y, 1f, 1f));
            var mask = WriteTexture("Mask", true,
                (x, y) => new Color(0.2f + 0.15f * x, 0.7f + 0.05f * y, 0f, 0.3f + 0.1f * y));

            foreach (var layerCount in new[] { 3, 4 })
            {
                foreach (var graph in new[] { false, true })
                {
                    var shaderPath = graph
                        ? Root + "/LayerBlend" + layerCount + "Parity.shadergraph"
                        : "Packages/com.simpletools.simpleblend/Shaders/LayerBlend" + layerCount + ".shader";
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                    if (shader == null || ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("Cannot create reference material for " + shaderPath);
                    var name = (graph ? "Graph" : "Hlsl") + layerCount;
                    var materialPath = Root + "/Materials/" + name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(shader) { name = name };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    else
                    {
                        material.shader = shader;
                    }
                    material.SetFloat("_BlendTransition", 0.2f);
                    for (var layer = 0; layer < layerCount; layer++)
                    {
                        var prefix = "_Layer" + layer;
                        material.SetTexture(prefix + "Albedo", albedos[layer]);
                        material.SetTexture(prefix + "Height", heights[layer]);
                        material.SetTexture(prefix + "Normal", normal);
                        material.SetTexture(prefix + "Mask", mask);
                        material.SetColor(prefix + "Color", Color.white);
                        material.SetFloat(prefix + "NormalScale", 0.8f);
                        material.SetFloat(prefix + "Metallic", 0.4f);
                        material.SetFloat(prefix + "Smoothness", 0.6f);
                        material.SetFloat(prefix + "OcclusionStrength", 0.75f);
                        material.SetTextureScale(prefix + "Albedo", new Vector2(1f + layer * 0.25f, 1f));
                        material.SetTextureOffset(prefix + "Albedo", new Vector2(layer * 0.1f, 0f));
                    }
                    EditorUtility.SetDirty(material);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SimpleBlend Shader Graph reference assets created.");
        }

        private static Texture2D WriteTexture(string name, bool linear, Func<int, int, Color> pixel)
        {
            var path = Root + "/Textures/" + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, linear)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, path);
            }
            for (var y = 0; y < 4; y++)
                for (var x = 0; x < 4; x++)
                    texture.SetPixel(x, y, pixel(x, y));
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
