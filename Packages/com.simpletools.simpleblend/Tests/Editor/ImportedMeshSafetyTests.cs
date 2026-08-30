using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using SimpleBlend.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace SimpleBlend.Editor.Tests
{
    public sealed class ImportedMeshSafetyTests
    {
        [Test]
        public void SharedImportedFbxRemainsByteIdenticalAndDoesNotBleed()
        {
            using (var assets = new TestAssetScope())
            {
                var urp = PackageManagerInfo.FindForPackageName("com.unity.render-pipelines.universal");
                Assert.That(urp, Is.Not.Null);
                var sourceFbx = Path.Combine(
                    urp.resolvedPath,
                    "Samples~",
                    "URPPackageSamples",
                    "Shaders",
                    "Lit",
                    "Models",
                    "UVSphere.fbx");
                Assert.That(File.Exists(sourceFbx), Is.True, sourceFbx);

                var importedPath = $"{assets.AssetPath}/SharedSphere.fbx";
                File.Copy(sourceFbx, Path.GetFullPath(importedPath), true);
                AssetDatabase.ImportAsset(importedPath, ImportAssetOptions.ForceSynchronousImport);
                var importedMesh = AssetDatabase.LoadAllAssetsAtPath(importedPath).OfType<Mesh>().First();
                var beforeHash = ComputeSha256(importedPath);

                var first = CreateRenderer("Red", importedMesh, out var firstRenderer, out var firstData);
                var second = CreateRenderer("Blue", importedMesh, out var secondRenderer, out var secondData);

                try
                {
                    Assert.That(VertexPaintDataAuthoring.Initialize(firstData, firstRenderer, false).IsValid, Is.True);
                    Assert.That(VertexPaintDataAuthoring.Initialize(secondData, secondRenderer, false).IsValid, Is.True);
                    firstData.ReplaceColors(TestMeshFactory.SolidColors(importedMesh.vertexCount, new Color32(255, 0, 0, 255)));
                    secondData.ReplaceColors(TestMeshFactory.SolidColors(importedMesh.vertexCount, new Color32(0, 0, 255, 255)));
                    Assert.That(VertexPaintDataAuthoring.Apply(firstData).IsValid, Is.True);
                    Assert.That(VertexPaintDataAuthoring.Apply(secondData).IsValid, Is.True);
                    AssetDatabase.SaveAssets();

                    var afterHash = ComputeSha256(importedPath);
                    TestContext.WriteLine($"Imported FBX SHA-256 before/after: {beforeHash} / {afterHash}");
                    Assert.That(afterHash, Is.EqualTo(beforeHash));
                    Assert.That(firstRenderer.additionalVertexStreams, Is.Not.SameAs(secondRenderer.additionalVertexStreams));
                    Assert.That(firstRenderer.additionalVertexStreams.colors32[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
                    Assert.That(secondRenderer.additionalVertexStreams.colors32[0], Is.EqualTo(new Color32(0, 0, 255, 255)));
                }
                finally
                {
                    Object.DestroyImmediate(first);
                    Object.DestroyImmediate(second);
                }
            }
        }

        [Test]
        public void ReimportWithChangedVertexCountIsRejectedAndDetachesStream()
        {
            using (var assets = new TestAssetScope())
            {
                var objectPath = $"{assets.AssetPath}/Changing.obj";
                File.WriteAllText(Path.GetFullPath(objectPath), TriangleObj);
                AssetDatabase.ImportAsset(objectPath, ImportAssetOptions.ForceSynchronousImport);
                var importedMesh = AssetDatabase.LoadAllAssetsAtPath(objectPath).OfType<Mesh>().First();
                var gameObject = CreateRenderer("Changing", importedMesh, out var renderer, out var data);

                try
                {
                    var initialize = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    Assert.That(initialize.IsValid, Is.True, initialize.Message);
                    Assert.That(renderer.additionalVertexStreams, Is.Not.Null);

                    File.WriteAllText(Path.GetFullPath(objectPath), QuadObj);
                    AssetDatabase.ImportAsset(
                        objectPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                    var validation = VertexPaintDataAuthoring.Apply(data);
                    Assert.That(validation.Failure, Is.EqualTo(VertexPaintValidationFailure.VertexCountMismatch));
                    Assert.That(data.ValidationMessage, Does.Contain("vertex count changed"));
                    Assert.That(renderer.additionalVertexStreams, Is.Null);
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static GameObject CreateRenderer(
            string name,
            Mesh mesh,
            out MeshRenderer renderer,
            out VertexPaintData data)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = gameObject.AddComponent<MeshRenderer>();
            data = gameObject.AddComponent<VertexPaintData>();
            return gameObject;
        }

        private static string ComputeSha256(string assetPath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(Path.GetFullPath(assetPath)))
            {
                return string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
            }
        }

        private const string TriangleObj = @"o Triangle
v 0 0 0
v 1 0 0
v 0 1 0
vn 0 0 1
f 1//1 2//1 3//1
";

        private const string QuadObj = @"o Quad
v 0 0 0
v 1 0 0
v 1 1 0
v 0 1 0
vn 0 0 1
f 1//1 2//1 3//1
f 1//1 3//1 4//1
";
    }
}
