using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleBlend.Editor.Tests
{
    public static class TerrainBlendPlayerBuildFixture
    {
        public static void Build()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run the terrain build fixture in a dedicated batch process.");
            var folderName = "__SimpleBlendTerrainBuild_" + Guid.NewGuid().ToString("N");
            var assetRoot = "Assets/" + folderName;
            if (AssetDatabase.IsValidFolder(assetRoot))
                throw new InvalidOperationException("The temporary build folder already exists.");
            AssetDatabase.CreateFolder("Assets", folderName);
            try
            {
                var startup = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(startup.path))
                {
                    if (startup.isDirty || startup.rootCount != 0 || SceneManager.sceneCount != 1)
                        throw new InvalidOperationException("The dedicated build process must start with a saved scene or a clean empty scene.");
                    if (!EditorSceneManager.SaveScene(startup, assetRoot + "/BatchBootstrap.unity"))
                        throw new InvalidOperationException("Could not save the empty batch bootstrap scene.");
                }
                AssetDatabase.CreateFolder(assetRoot, "Reference");
                var referenceRoot = assetRoot + "/Reference";
                var scene = TerrainBlendReferenceFixture.Create(referenceRoot);
                AssetDatabase.CreateFolder(assetRoot, "Resources");
                var bound = AssetDatabase.LoadAssetAtPath<Material>(referenceRoot + "/TerrainBlend.mat");
                AssetDatabase.CreateAsset(new Material(bound), assetRoot + "/Resources/TerrainBlend.mat");
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds",
                    "SimpleBlendTerrain", "SimpleBlend.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Terrain shader player build failed: " + report.summary.result);
                Debug.Log("SimpleBlend terrain player build succeeded with retained bound material: " + output);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetRoot);
                AssetDatabase.Refresh();
            }
        }
    }
}
