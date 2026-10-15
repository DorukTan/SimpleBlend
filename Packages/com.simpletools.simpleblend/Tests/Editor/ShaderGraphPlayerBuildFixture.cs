using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public static class ShaderGraphPlayerBuildFixture
    {
        private const string Root = "Assets/__SimpleBlendShaderGraphBuild";

        public static void Build()
        {
            if (AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("The temporary graph build folder already exists: " + Root);
            AssetDatabase.CreateFolder("Assets", "__SimpleBlendShaderGraphBuild");
            try
            {
                AssetDatabase.CreateFolder(Root, "Resources");
                foreach (var count in new[] { 3, 4 })
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                        "Packages/com.simpletools.simpleblend/ShaderGraph/Examples/LayerBlend"
                        + count + "Parity.shadergraph");
                    if (shader == null || ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("Cannot retain graph shader for " + count + " layers.");
                    AssetDatabase.CreateAsset(new Material(shader),
                        Root + "/Resources/Graph" + count + ".mat");
                }
                AssetDatabase.SaveAssets();
                LayerBlendPlayerBuildFixture.Build();
                Debug.Log("SimpleBlend player build retained both Shader Graph reference shaders.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(Root);
                AssetDatabase.Refresh();
            }
        }
    }
}
