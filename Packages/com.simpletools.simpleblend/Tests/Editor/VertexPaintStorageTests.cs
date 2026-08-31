using System.Collections.Generic;
using NUnit.Framework;
using SimpleBlend.Editor;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public sealed class VertexPaintStorageTests
    {
        [Test]
        public void PolicySelectsStreamAndEveryRequiredCloneFallback()
        {
            var ordinaryObject = CreateMeshRendererObject("Ordinary", TestMeshFactory.CreateQuad(), out var ordinaryRenderer);
            var staticObject = CreateMeshRendererObject("Static", TestMeshFactory.CreateQuad(), out var staticRenderer);
            var skinnedObject = new GameObject("Skinned");
            var skinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();

            try
            {
                GameObjectUtility.SetStaticEditorFlags(staticObject, StaticEditorFlags.BatchingStatic);

                var ordinary = VertexPaintStoragePolicy.Resolve(ordinaryRenderer, false);
                var gpu = VertexPaintStoragePolicy.Resolve(ordinaryRenderer, true);
                var batching = VertexPaintStoragePolicy.Resolve(staticRenderer, false);
                var skinned = VertexPaintStoragePolicy.Resolve(skinnedRenderer, false);

                Assert.That(ordinary.Mode, Is.EqualTo(VertexPaintStorageMode.AdditionalVertexStream));
                Assert.That(ordinary.Reason, Is.EqualTo(VertexPaintStorageReason.OrdinaryMeshRenderer));
                Assert.That(gpu.Mode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                Assert.That(gpu.Reason, Is.EqualTo(VertexPaintStorageReason.GpuResidentDrawer));
                Assert.That(batching.Mode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                Assert.That(batching.Reason, Is.EqualTo(VertexPaintStorageReason.StaticBatching));
                Assert.That(skinned.Mode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                Assert.That(skinned.Reason, Is.EqualTo(VertexPaintStorageReason.SkinnedMeshRenderer));
            }
            finally
            {
                var ordinaryMesh = ordinaryObject.GetComponent<MeshFilter>().sharedMesh;
                var staticMesh = staticObject.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(ordinaryObject);
                Object.DestroyImmediate(staticObject);
                Object.DestroyImmediate(skinnedObject);
                Object.DestroyImmediate(ordinaryMesh);
                Object.DestroyImmediate(staticMesh);
            }
        }

        [Test]
        public void SharedSourceReceivesIndependentTransientStreamsAndSurvivesDisable()
        {
            var source = TestMeshFactory.CreateQuad();
            var first = CreateMeshRendererObject("First", source, out var firstRenderer);
            var second = CreateMeshRendererObject("Second", source, out var secondRenderer);
            var firstData = first.AddComponent<VertexPaintData>();
            var secondData = second.AddComponent<VertexPaintData>();

            try
            {
                Assert.That(VertexPaintDataAuthoring.Initialize(firstData, firstRenderer, false).IsValid, Is.True);
                Assert.That(VertexPaintDataAuthoring.Initialize(secondData, secondRenderer, false).IsValid, Is.True);

                firstData.ReplaceColors(TestMeshFactory.SolidColors(source.vertexCount, new Color32(255, 0, 0, 255)));
                secondData.ReplaceColors(TestMeshFactory.SolidColors(source.vertexCount, new Color32(0, 0, 255, 255)));
                Assert.That(VertexPaintDataAuthoring.Apply(firstData).IsValid, Is.True);
                Assert.That(VertexPaintDataAuthoring.Apply(secondData).IsValid, Is.True);

                Assert.That(firstRenderer.additionalVertexStreams, Is.Not.Null);
                Assert.That(secondRenderer.additionalVertexStreams, Is.Not.Null);
                Assert.That(firstRenderer.additionalVertexStreams, Is.Not.SameAs(secondRenderer.additionalVertexStreams));
                Assert.That(firstRenderer.additionalVertexStreams.colors32[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
                Assert.That(secondRenderer.additionalVertexStreams.colors32[0], Is.EqualTo(new Color32(0, 0, 255, 255)));
                Assert.That(source.colors32, Is.Empty);

                firstData.enabled = false;
                Assert.That(firstRenderer.additionalVertexStreams, Is.Null);
                Assert.That(firstData.ColorCount, Is.EqualTo(source.vertexCount));

                firstData.enabled = true;
                Assert.That(firstRenderer.additionalVertexStreams, Is.Not.Null);
                Assert.That(firstRenderer.additionalVertexStreams.colors32[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void GeneratedClonePreservesMeshDataAndClearDoesNotDeleteAsset()
        {
            using (var assets = new TestAssetScope())
            {
                var sourcePath = $"{assets.AssetPath}/SkinnedSource.asset";
                var source = TestMeshFactory.CreateQuad(includeColors: true, includeSkinning: true);
                AssetDatabase.CreateAsset(source, sourcePath);
                var paintedColors = TestMeshFactory.SolidColors(source.vertexCount, new Color32(12, 34, 56, 78));

                var cloneResult = GeneratedMeshWriter.TryCreate(
                    source,
                    paintedColors,
                    "Owner",
                    out var clone,
                    out var clonePath);

                Assert.That(cloneResult.IsValid, Is.True, cloneResult.Message);
                Assert.That(clonePath, Does.StartWith(assets.AssetPath + "/"));
                Assert.That(AssetDatabase.Contains(clone), Is.True);
                AssertMeshEquivalent(source, clone);
                Assert.That(clone.colors32, Is.EqualTo(paintedColors));
                Assert.That(source.colors32[0], Is.EqualTo(new Color32(10, 20, 30, 40)));

                var gameObject = new GameObject("Clone Owner");
                var filter = gameObject.AddComponent<MeshFilter>();
                gameObject.AddComponent<MeshRenderer>();
                var data = gameObject.AddComponent<VertexPaintData>();
                filter.sharedMesh = source;
                MeshSignatureFactory.TryCreate(source, MeshAssetIdentity.GetStableIdentity(source), out var signature);
                data.Configure(
                    source,
                    signature,
                    paintedColors,
                    VertexPaintStorageMode.ClonedMesh,
                    VertexPaintStorageReason.StaticBatching);
                data.SetFallbackMesh(clone, clonePath, "test-owner");
                Assert.That(VertexPaintDataAuthoring.Apply(data).IsValid, Is.True);
                Assert.That(filter.sharedMesh, Is.EqualTo(clone));

                VertexPaintDataAuthoring.Clear(data);
                Assert.That(filter.sharedMesh, Is.EqualTo(source));
                Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(clonePath), Is.Not.Null);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NonReadableOrdinaryMeshCanBeClonedThroughReadOnlyMeshData()
        {
            using (var assets = new TestAssetScope())
            {
                var source = TestMeshFactory.CreateQuad();
                source.UploadMeshData(true);
                AssetDatabase.CreateAsset(source, $"{assets.AssetPath}/NonReadable.asset");

                var result = GeneratedMeshWriter.TryCreate(
                    source,
                    TestMeshFactory.SolidColors(source.vertexCount, new Color32(1, 2, 3, 4)),
                    "NonReadable",
                    out var clone,
                    out _);

                Assert.That(source.isReadable, Is.False);
                Assert.That(result.IsValid, Is.True, result.Message);
                Assert.That(clone.isReadable, Is.True);
                Assert.That(clone.vertexCount, Is.EqualTo(source.vertexCount));
                Assert.That(clone.subMeshCount, Is.EqualTo(source.subMeshCount));
                Assert.That(clone.colors32[0], Is.EqualTo(new Color32(1, 2, 3, 4)));
            }
        }

        [Test]
        public void ReinitializingCloneOwnerRestoresOriginalSourceBeforeCreatingReplacement()
        {
            using (var assets = new TestAssetScope())
            {
                var source = TestMeshFactory.CreateQuad(includeSkinning: true);
                AssetDatabase.CreateAsset(source, $"{assets.AssetPath}/Reinitialize.asset");
                var gameObject = new GameObject("Reinitialize");
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source;
                var data = gameObject.AddComponent<VertexPaintData>();

                try
                {
                    var first = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    var firstClone = data.FallbackMesh;
                    Assert.That(first.IsValid, Is.True, first.Message);
                    Assert.That(renderer.sharedMesh, Is.EqualTo(firstClone));

                    var second = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    Assert.That(second.IsValid, Is.True, second.Message);
                    Assert.That(data.SourceMesh, Is.EqualTo(source));
                    Assert.That(data.FallbackMesh, Is.Not.EqualTo(firstClone));
                    Assert.That(renderer.sharedMesh, Is.EqualTo(data.FallbackMesh));
                    Assert.That(AssetDatabase.Contains(firstClone), Is.True, "Reinitialization must not delete an old generated asset implicitly.");
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        [Test]
        public void Storage_policy_migrates_without_losing_authoritative_colors_or_deleting_old_assets()
        {
            using (var assets = new TestAssetScope())
            {
                var source = TestMeshFactory.CreateQuad();
                AssetDatabase.CreateAsset(source, $"{assets.AssetPath}/MigratingSource.asset");
                var gameObject = CreateMeshRendererObject("Migrating", source, out var renderer);
                var data = gameObject.AddComponent<VertexPaintData>();

                try
                {
                    Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);
                    var painted = TestMeshFactory.SolidColors(
                        source.vertexCount,
                        new Color32(21, 22, 23, 24));
                    data.ReplaceColors(painted);
                    Assert.That(VertexPaintDataAuthoring.Apply(data).IsValid, Is.True);

                    GameObjectUtility.SetStaticEditorFlags(gameObject, StaticEditorFlags.BatchingStatic);
                    var toStatic = VertexPaintDataAuthoring.EnsureCurrentStorageForPaint(
                        data,
                        false,
                        out var changedToStatic);
                    var staticAssetPath = data.FallbackAssetPath;

                    Assert.That(toStatic.IsValid, Is.True, toStatic.Message);
                    Assert.That(changedToStatic, Is.True);
                    Assert.That(data.StorageMode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                    Assert.That(data.StorageReason, Is.EqualTo(VertexPaintStorageReason.StaticBatching));
                    Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(data.FallbackMesh));
                    Assert.That(data.FallbackMesh.colors32, Is.EqualTo(painted));

                    GameObjectUtility.SetStaticEditorFlags(gameObject, 0);
                    var toStream = VertexPaintDataAuthoring.EnsureCurrentStorageForPaint(
                        data,
                        false,
                        out var changedToStream);

                    Assert.That(toStream.IsValid, Is.True, toStream.Message);
                    Assert.That(changedToStream, Is.True);
                    Assert.That(data.StorageMode, Is.EqualTo(VertexPaintStorageMode.AdditionalVertexStream));
                    Assert.That(data.StorageReason, Is.EqualTo(VertexPaintStorageReason.OrdinaryMeshRenderer));
                    Assert.That(data.FallbackMesh, Is.Null);
                    Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(source));
                    Assert.That(renderer.additionalVertexStreams.colors32, Is.EqualTo(painted));
                    Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(staticAssetPath), Is.Not.Null,
                        "Changing policy must not silently delete the old generated mesh.");

                    var toGpu = VertexPaintDataAuthoring.EnsureCurrentStorageForPaint(
                        data,
                        true,
                        out var changedToGpu);
                    Assert.That(toGpu.IsValid, Is.True, toGpu.Message);
                    Assert.That(changedToGpu, Is.True);
                    Assert.That(data.StorageMode, Is.EqualTo(VertexPaintStorageMode.ClonedMesh));
                    Assert.That(data.StorageReason, Is.EqualTo(VertexPaintStorageReason.GpuResidentDrawer));
                    Assert.That(data.FallbackMesh.colors32, Is.EqualTo(painted));
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static GameObject CreateMeshRendererObject(string name, Mesh mesh, out MeshRenderer renderer)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = gameObject.AddComponent<MeshRenderer>();
            return gameObject;
        }

        private static void AssertMeshEquivalent(Mesh source, Mesh clone)
        {
            Assert.That(clone.vertexCount, Is.EqualTo(source.vertexCount));
            Assert.That(clone.subMeshCount, Is.EqualTo(source.subMeshCount));
            Assert.That(clone.indexFormat, Is.EqualTo(source.indexFormat));
            Assert.That(clone.vertices, Is.EqualTo(source.vertices));
            Assert.That(clone.normals, Is.EqualTo(source.normals));
            Assert.That(clone.tangents, Is.EqualTo(source.tangents));
            Assert.That(clone.bindposes, Is.EqualTo(source.bindposes));
            Assert.That(clone.blendShapeCount, Is.EqualTo(source.blendShapeCount));
            Assert.That(clone.GetBlendShapeName(0), Is.EqualTo(source.GetBlendShapeName(0)));

            for (var subMeshIndex = 0; subMeshIndex < source.subMeshCount; subMeshIndex++)
            {
                Assert.That(clone.GetIndices(subMeshIndex), Is.EqualTo(source.GetIndices(subMeshIndex)));
            }

            var sourceUv = new List<Vector2>();
            var cloneUv = new List<Vector2>();
            source.GetUVs(0, sourceUv);
            clone.GetUVs(0, cloneUv);
            Assert.That(cloneUv, Is.EqualTo(sourceUv));

            var sourceDelta = new Vector3[source.vertexCount];
            var cloneDelta = new Vector3[clone.vertexCount];
            source.GetBlendShapeFrameVertices(0, 0, sourceDelta, null, null);
            clone.GetBlendShapeFrameVertices(0, 0, cloneDelta, null, null);
            Assert.That(cloneDelta, Is.EqualTo(sourceDelta));
        }
    }
}
