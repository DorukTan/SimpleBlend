using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public static class LayerBlendPlayerBuildFixture
    {
        private const string TemporaryAssetRoot = "Assets/__SimpleBlendM7Build";
        private const string TemporaryResources = TemporaryAssetRoot + "/Resources";

        public static void Build()
        {
            try
            {
                CreateTemporaryMaterials();
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var outputPath = Path.Combine(
                    projectRoot,
                    "Builds",
                    "SimpleBlendM7",
                    "SimpleBlend.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException("The project has no enabled build scenes.");
                }

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"SimpleBlend M7 player build failed: {report.summary.result}.");
                }

                Debug.Log(
                    $"SimpleBlend M7 player build succeeded with forced Layer Blend 3/4 materials: {outputPath}");
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryAssetRoot);
                AssetDatabase.Refresh();
            }
        }

        private static void CreateTemporaryMaterials()
        {
            AssetDatabase.DeleteAsset(TemporaryAssetRoot);
            AssetDatabase.CreateFolder("Assets", "__SimpleBlendM7Build");
            AssetDatabase.CreateFolder(TemporaryAssetRoot, "Resources");
            CreateMaterial(
                "Packages/com.simpletools.simpleblend/Shaders/LayerBlend3.shader",
                TemporaryResources + "/LayerBlend3.mat");
            CreateMaterial(
                "Packages/com.simpletools.simpleblend/Shaders/LayerBlend4.shader",
                TemporaryResources + "/LayerBlend4.mat");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateMaterial(string shaderPath, string materialPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null || string.IsNullOrEmpty(shader.name))
            {
                throw new InvalidOperationException($"Could not load a valid shader at '{shaderPath}'.");
            }

            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(materialPath),
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }
    }
}
