using System.IO;
using NUnit.Framework;
using SimpleBlend.Editor;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public sealed class VertexPaintDataTests
    {
        [Test]
        public void ExistingColorsAreCopiedAndMissingColorsInitializeClear()
        {
            var colored = TestMeshFactory.CreateQuad(includeColors: true);
            var clear = TestMeshFactory.CreateQuad();

            try
            {
                var coloredResult = VertexColorInitializer.TryCreate(colored, out var copied);
                var clearResult = VertexColorInitializer.TryCreate(clear, out var initialized);

                Assert.That(coloredResult.IsValid, Is.True, coloredResult.Message);
                Assert.That(clearResult.IsValid, Is.True, clearResult.Message);
                Assert.That(copied, Is.EqualTo(colored.colors32));
                Assert.That(initialized, Has.Length.EqualTo(clear.vertexCount));
                Assert.That(initialized, Is.All.EqualTo(new Color32(0, 0, 0, 0)));
            }
            finally
            {
                Object.DestroyImmediate(colored);
                Object.DestroyImmediate(clear);
            }
        }

        [Test]
        public void ReadOnlyMeshDataSupportsNonReadableSource()
        {
            var mesh = TestMeshFactory.CreateQuad(includeColors: true);
            mesh.UploadMeshData(true);

            try
            {
                Assert.That(mesh.isReadable, Is.False);
                var signatureResult = MeshSignatureFactory.TryCreate(mesh, string.Empty, out var signature);
                var colorResult = VertexColorInitializer.TryCreate(mesh, out var colors);

                Assert.That(signatureResult.IsValid, Is.True, signatureResult.Message);
                Assert.That(colorResult.IsValid, Is.True, colorResult.Message);
                Assert.That(signature.VertexCount, Is.EqualTo(4));
                Assert.That(colors, Has.Length.EqualTo(4));
                Assert.That(colors[0], Is.EqualTo(new Color32(10, 20, 30, 40)));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ValidationReturnsExplicitWrongLengthAndMissingSourceFailures()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = new GameObject("Validation Test");
            var filter = gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            var data = gameObject.AddComponent<VertexPaintData>();
            filter.sharedMesh = mesh;

            try
            {
                MeshSignatureFactory.TryCreate(mesh, string.Empty, out var signature);
                data.Configure(
                    mesh,
                    signature,
                    new Color32[mesh.vertexCount - 1],
                    VertexPaintStorageMode.AdditionalVertexStream,
                    VertexPaintStorageReason.OrdinaryMeshRenderer);

                var wrongLength = VertexPaintValidator.Validate(data);
                Assert.That(wrongLength.Failure, Is.EqualTo(VertexPaintValidationFailure.ColorCountMismatch));

                data.ResetSerializedState();
                var missingSource = VertexPaintValidator.Validate(data);
                Assert.That(missingSource.Failure, Is.EqualTo(VertexPaintValidationFailure.MissingSourceMesh));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ComponentRoundTripsThroughPrefabYaml()
        {
            using (var assets = new TestAssetScope())
            {
                var meshPath = $"{assets.AssetPath}/Source.asset";
                var prefabPath = $"{assets.AssetPath}/Painted.prefab";
                var mesh = TestMeshFactory.CreateQuad(includeColors: true);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var gameObject = new GameObject("Serialized Paint");
                var filter = gameObject.AddComponent<MeshFilter>();
                var renderer = gameObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                var data = gameObject.AddComponent<VertexPaintData>();

                try
                {
                    var result = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    Assert.That(result.IsValid, Is.True, result.Message);
                    PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }

                AssetDatabase.SaveAssets();
                var yaml = File.ReadAllText(prefabPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var restored = prefab.GetComponent<VertexPaintData>();

                Assert.That(yaml, Does.Contain("_schemaVersion: 2"));
                Assert.That(yaml, Does.Contain("_sourceSignature:"));
                Assert.That(yaml, Does.Contain("_colors:"));
                Assert.That(restored.SchemaVersion, Is.EqualTo(VertexPaintData.CurrentSchemaVersion));
                Assert.That(restored.SourceMesh, Is.EqualTo(mesh));
                Assert.That(restored.StorageMode, Is.EqualTo(VertexPaintStorageMode.AdditionalVertexStream));
                Assert.That(restored.ColorCount, Is.EqualTo(mesh.vertexCount));
                Assert.That(restored.GetColor(2), Is.EqualTo(new Color32(90, 100, 110, 120)));

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var instanceRenderer = instance.GetComponent<MeshRenderer>();
                    Assert.That(instanceRenderer.additionalVertexStreams, Is.Not.Null);
                    Assert.That(instanceRenderer.additionalVertexStreams.colors32[2],
                        Is.EqualTo(new Color32(90, 100, 110, 120)));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void CloneModeRoundTripsAndReconstructsFromPrefab()
        {
            using (var assets = new TestAssetScope())
            {
                var meshPath = $"{assets.AssetPath}/SkinnedSource.asset";
                var prefabPath = $"{assets.AssetPath}/Skinned.prefab";
                var mesh = TestMeshFactory.CreateQuad(includeSkinning: true);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var gameObject = new GameObject("Serialized Clone");
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                var data = gameObject.AddComponent<VertexPaintData>();

                try
                {
                    var initialize = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    Assert.That(initialize.IsValid, Is.True, initialize.Message);
                    Assert.That(data.StorageMode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                    PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }

                AssetDatabase.SaveAssets();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var restored = instance.GetComponent<VertexPaintData>();
                    var restoredRenderer = instance.GetComponent<SkinnedMeshRenderer>();
                    Assert.That(restored.FallbackMesh, Is.Not.Null);
                    Assert.That(restored.FallbackAssetPath, Does.StartWith(assets.AssetPath + "/"));
                    Assert.That(restoredRenderer.sharedMesh, Is.EqualTo(restored.FallbackMesh));
                    Assert.That(restored.ValidationFailure, Is.EqualTo(VertexPaintValidationFailure.None));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void TeachingMessagesCoverEverySerializedReason()
        {
            foreach (VertexPaintStorageReason reason in System.Enum.GetValues(typeof(VertexPaintStorageReason)))
            {
                Assert.That(VertexPaintText.For(reason), Is.Not.Empty);
            }

            foreach (VertexPaintValidationFailure failure in System.Enum.GetValues(typeof(VertexPaintValidationFailure)))
            {
                Assert.That(VertexPaintText.For(failure), Is.Not.Empty);
            }
        }
    }
}
