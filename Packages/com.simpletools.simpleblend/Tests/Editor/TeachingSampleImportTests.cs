using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TeachingSampleImportTests
    {
        [TestCase("Rock and Moss", "RockMoss")]
        [TestCase("Roof and Snow", "RoofSnow")]
        [TestCase("Wall and Grime", "WallGrime")]
        public void Package_manager_imports_self_contained_painted_scene(string displayName, string sceneName)
        {
            var sample = Sample.FindByPackage("com.simpletools.simpleblend", "0.4.0-beta.1")
                .Single(s => s.displayName == displayName);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            var absoluteDestination = Path.GetFullPath(sample.importPath).Replace('\\', '/');
            Assert.That(absoluteDestination, Does.StartWith(projectRoot + "/Assets/Samples/SimpleBlend/0.4.0-beta.1/"));
            var destination = absoluteDestination.Substring(projectRoot.Length + 1);
            Assert.That(destination, Does.StartWith("Assets/Samples/SimpleBlend/0.4.0-beta.1/"));
            Assert.That(Directory.Exists(destination), Is.False,
                "Use a clean test project; existing imported samples must not be overwritten.");
            Assert.That(sample.isImported, Is.False);
            var owned = false;
            try
            {
                owned = true;
                Assert.That(sample.Import(Sample.ImportOptions.None), Is.True);
                AssetDatabase.Refresh();
                var path = destination + "/" + sceneName + ".unity";
                Assert.That(File.Exists(path), Is.True);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var paint = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<VertexPaintData>(true)).Single();
                    Assert.That(paint.ValidationFailure, Is.EqualTo(VertexPaintValidationFailure.None));
                    Assert.That(paint.ColorCount, Is.EqualTo(paint.SourceMesh.vertexCount));
                    Assert.That(paint.SourceMesh.colors32, Is.Empty, "Paint must not mutate the source mesh.");
                    var renderer = paint.GetComponent<MeshRenderer>();
                    Assert.That(renderer.additionalVertexStreams, Is.Not.Null);
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("SimpleBlend/Layer Blend 3"));
                    for (var layer = 0; layer < 3; layer++)
                    {
                        foreach (var suffix in new[] { "Albedo", "Height" })
                        {
                            var texture = renderer.sharedMaterial.GetTexture("_Layer" + layer + suffix);
                            Assert.That(texture, Is.Not.Null, suffix);
                            Assert.That(AssetDatabase.GetAssetPath(texture), Does.StartWith(destination + "/"));
                        }
                    }
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var child in root.GetComponentsInChildren<Transform>(true))
                            Assert.That(child.GetComponents<Component>(), Has.All.Not.Null,
                                "Missing component on " + child.name);
                    var captions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TextMesh>())
                        .Select(text => text.text).ToArray();
                    Assert.That(captions.Any(text => text.Contains("Red:") && text.Contains("Green:")
                        && text.Contains("strength 0.35")), Is.True);
                    for (var index = 0; index < paint.SourceMesh.vertexCount; index++)
                    {
                        Assert.That(paint.GetColor(index).b, Is.Zero);
                        Assert.That(paint.GetColor(index).a, Is.Zero);
                        Assert.That(paint.SourceMesh.normals[index].sqrMagnitude, Is.GreaterThan(0.5f));
                        if (sceneName == "RockMoss")
                            Assert.That(Vector3.Dot(paint.SourceMesh.normals[index],
                                (paint.SourceMesh.vertices[index] - Vector3.up).normalized), Is.GreaterThan(0.2f));
                        Assert.That(new Vector3(paint.SourceMesh.tangents[index].x,
                            paint.SourceMesh.tangents[index].y, paint.SourceMesh.tangents[index].z).sqrMagnitude,
                            Is.GreaterThan(0.5f));
                    }
                    foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                    {
                        Assert.That(dependency.Contains("/Tests/"), Is.False, dependency);
                        Assert.That(dependency.StartsWith("Assets/__SimpleBlendSamples_"), Is.False, dependency);
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            finally
            {
                if (owned)
                    AssetDatabase.DeleteAsset(destination);
            }
        }
    }
}
