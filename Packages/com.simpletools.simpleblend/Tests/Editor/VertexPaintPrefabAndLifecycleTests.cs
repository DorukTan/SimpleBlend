using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SimpleBlend.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class VertexPaintPrefabAndLifecycleTests
    {
        private const string LifecycleFolder = "Assets/__SimpleBlendLifecycleTest";
        private const string LifecycleScenePath = LifecycleFolder + "/Lifecycle.unity";
        private const string LifecycleObjectName = "SimpleBlend Lifecycle Target";

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            CleanupLifecycleFixture();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            CleanupLifecycleFixture();
        }

        [Test]
        public void Setup_adds_initialized_component_with_one_undoable_operation()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = CreateMeshObject("Setup", mesh, out var renderer);
            try
            {
                var result = VertexPaintSetupUtility.Enable(renderer, false, out var data);
                Undo.FlushUndoRecordObjects();

                Assert.That(result.IsValid, Is.True, result.Message);
                Assert.That(data, Is.Not.Null);
                Assert.That(data.SourceMesh, Is.EqualTo(mesh));
                Assert.That(renderer.additionalVertexStreams, Is.Not.Null);

                Undo.PerformUndo();
                Assert.That(gameObject.GetComponent<VertexPaintData>(), Is.Null);
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(mesh));

                Undo.PerformRedo();
                var restored = gameObject.GetComponent<VertexPaintData>();
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.SourceMesh, Is.EqualTo(mesh));
                Assert.That(VertexPaintDataAuthoring.Apply(restored).IsValid, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Prefab_instance_stroke_is_a_property_override_and_reverts_cleanly()
        {
            using (var assets = new TestAssetScope())
            {
                var mesh = TestMeshFactory.CreateQuad();
                AssetDatabase.CreateAsset(mesh, $"{assets.AssetPath}/Source.asset");
                var prefabPath = CreateInitializedPrefab(assets, mesh, false, "StreamPrefab");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                try
                {
                    var data = instance.GetComponent<VertexPaintData>();
                    PaintAll(data);

                    Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.True);
                    Assert.That(
                        PrefabUtility.GetPropertyModifications(data)
                            .Any(modification => modification.propertyPath.StartsWith("_colors", StringComparison.Ordinal)),
                        Is.True,
                        "The authoritative Color32 array should appear as a normal prefab property override.");
                    Assert.That(AllRed(data), Is.True);

                    PrefabUtility.RevertObjectOverride(data, InteractionMode.AutomatedAction);
                    data = instance.GetComponent<VertexPaintData>();
                    Assert.That(VertexPaintDataAuthoring.Apply(data).IsValid, Is.True);
                    Assert.That(AllClear(data), Is.True);
                    Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.False);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void Saving_prefab_contents_propagates_painted_colors_to_instances()
        {
            using (var assets = new TestAssetScope())
            {
                var mesh = TestMeshFactory.CreateQuad();
                AssetDatabase.CreateAsset(mesh, $"{assets.AssetPath}/Source.asset");
                var prefabPath = CreateInitializedPrefab(assets, mesh, false, "PrefabMode");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);

                try
                {
                    PaintAll(contents.GetComponent<VertexPaintData>());
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                try
                {
                    AssetDatabase.SaveAssets();
                    var assetData = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                        .GetComponent<VertexPaintData>();
                    var instanceData = instance.GetComponent<VertexPaintData>();
                    Assert.That(AllRed(assetData), Is.True);
                    Assert.That(AllRed(instanceData), Is.True);
                    var colorModifications = PrefabUtility.GetPropertyModifications(instanceData)
                        .Where(modification => modification.propertyPath.StartsWith("_colors", StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(colorModifications, Is.Empty,
                        "Saving the prefab asset must not leave a local authoritative color override on an instance.");
                    Assert.That(VertexPaintDataAuthoring.Apply(instanceData).IsValid, Is.True);
                    Assert.That(instance.GetComponent<MeshRenderer>().additionalVertexStreams.colors32[0].r,
                        Is.EqualTo(byte.MaxValue));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void Static_prefab_instance_forks_inherited_clone_before_local_paint()
        {
            using (var assets = new TestAssetScope())
            {
                var mesh = TestMeshFactory.CreateQuad();
                AssetDatabase.CreateAsset(mesh, $"{assets.AssetPath}/StaticSource.asset");
                var prefabPath = CreateInitializedPrefab(assets, mesh, true, "StaticPrefab");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var prefabData = prefab.GetComponent<VertexPaintData>();
                var inheritedFallback = prefabData.FallbackMesh;
                var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                try
                {
                    var firstData = first.GetComponent<VertexPaintData>();
                    var secondData = second.GetComponent<VertexPaintData>();
                    PaintAll(firstData);
                    var forkPath = firstData.FallbackAssetPath;

                    Assert.That(firstData.FallbackMesh, Is.Not.EqualTo(inheritedFallback));
                    Assert.That(firstData.FallbackOwnerId, Is.Not.EqualTo(prefabData.FallbackOwnerId));
                    Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(firstData.FallbackMesh));
                    Assert.That(secondData.FallbackMesh, Is.EqualTo(inheritedFallback));
                    Assert.That(AllClear(secondData), Is.True);
                    Assert.That(inheritedFallback.colors32, Is.All.EqualTo(new Color32(0, 0, 0, 0)));
                    Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(forkPath), Is.Not.Null);

                    PrefabUtility.RevertPrefabInstance(first, InteractionMode.AutomatedAction);
                    firstData = first.GetComponent<VertexPaintData>();
                    Assert.That(firstData.FallbackMesh, Is.EqualTo(inheritedFallback));
                    Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(inheritedFallback));
                    Assert.That(AllClear(firstData), Is.True);
                    Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(first, false), Is.False);
                    Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(forkPath), Is.Not.Null,
                        "Revert must not silently delete generated assets.");
                }
                finally
                {
                    Object.DestroyImmediate(first);
                    Object.DestroyImmediate(second);
                }
            }
        }

        [Test]
        public void Skinned_fallback_paints_the_current_baked_pose_and_preserves_source_data()
        {
            using (var assets = new TestAssetScope())
            {
                var source = TestMeshFactory.CreateQuad(includeSkinning: true);
                AssetDatabase.CreateAsset(source, $"{assets.AssetPath}/SkinnedSource.asset");
                var gameObject = new GameObject("Skinned Paint");
                var bone = new GameObject("Bone").transform;
                bone.SetParent(gameObject.transform, false);
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source;
                renderer.rootBone = bone;
                renderer.bones = new[] { bone };
                var data = gameObject.AddComponent<VertexPaintData>();

                try
                {
                    var initialize = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                    Assert.That(initialize.IsValid, Is.True, initialize.Message);
                    var cache = new MeshPaintCache();
                    try
                    {
                        var cacheResult = cache.Ensure(data);
                        Assert.That(cacheResult.IsValid, Is.True, cacheResult.Message);
                        Assert.That(cache.WorldVertices.Count, Is.EqualTo(source.vertexCount));
                        PaintAll(data, cache);
                    }
                    finally
                    {
                        cache.Dispose();
                    }

                    Assert.That(AllRed(data), Is.True);
                    Assert.That(renderer.sharedMesh, Is.EqualTo(data.FallbackMesh));
                    Assert.That(data.FallbackMesh.bindposes, Is.EqualTo(source.bindposes));
                    Assert.That(data.FallbackMesh.blendShapeCount, Is.EqualTo(source.blendShapeCount));
                    Assert.That(source.colors32, Is.Empty);
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator Additional_stream_reconstructs_through_play_mode_and_enable_cycles()
        {
            AssetDatabase.CreateFolder("Assets", "__SimpleBlendLifecycleTest");
            var source = TestMeshFactory.CreateQuad();
            AssetDatabase.CreateAsset(source, $"{LifecycleFolder}/LifecycleSource.asset");
            var lifecycleScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameObject = CreateMeshObject(LifecycleObjectName, source, out var renderer);
            var data = gameObject.AddComponent<VertexPaintData>();
            Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);
            data.ReplaceColors(TestMeshFactory.SolidColors(source.vertexCount, new Color32(255, 0, 0, 255)));
            Assert.That(VertexPaintDataAuthoring.Apply(data).IsValid, Is.True);
            Assert.That(EditorSceneManager.SaveScene(lifecycleScene, LifecycleScenePath), Is.True);

            yield return new EnterPlayMode();

            gameObject = GameObject.Find(LifecycleObjectName);
            Assert.That(gameObject, Is.Not.Null);
            data = gameObject.GetComponent<VertexPaintData>();
            renderer = gameObject.GetComponent<MeshRenderer>();
            Assert.That(renderer.additionalVertexStreams, Is.Not.Null);
            Assert.That(renderer.additionalVertexStreams.colors32[0].r, Is.EqualTo(byte.MaxValue));
            data.enabled = false;
            Assert.That(renderer.additionalVertexStreams, Is.Null);
            data.enabled = true;
            Assert.That(renderer.additionalVertexStreams.colors32[0].r, Is.EqualTo(byte.MaxValue));

            yield return new ExitPlayMode();

            gameObject = GameObject.Find(LifecycleObjectName);
            Assert.That(gameObject, Is.Not.Null);
            data = gameObject.GetComponent<VertexPaintData>();
            renderer = gameObject.GetComponent<MeshRenderer>();
            Assert.That(AllRed(data), Is.True);
            Assert.That(renderer.additionalVertexStreams, Is.Not.Null);
        }

        private static string CreateInitializedPrefab(
            TestAssetScope assets,
            Mesh mesh,
            bool batchingStatic,
            string name)
        {
            var gameObject = CreateMeshObject(name, mesh, out var renderer);
            var data = gameObject.AddComponent<VertexPaintData>();
            if (batchingStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(gameObject, StaticEditorFlags.BatchingStatic);
            }

            try
            {
                var initialize = VertexPaintDataAuthoring.Initialize(data, renderer, false);
                Assert.That(initialize.IsValid, Is.True, initialize.Message);
                var path = $"{assets.AssetPath}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
                AssetDatabase.SaveAssets();
                return path;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static GameObject CreateMeshObject(string name, Mesh mesh, out MeshRenderer renderer)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = gameObject.AddComponent<MeshRenderer>();
            return gameObject;
        }

        private static void PaintAll(VertexPaintData data, MeshPaintCache cache = null)
        {
            var ownsCache = cache == null;
            cache = cache ?? new MeshPaintCache();
            try
            {
                var cacheResult = cache.Ensure(data);
                Assert.That(cacheResult.IsValid, Is.True, cacheResult.Message);
                var session = new VertexPaintStrokeSession();
                Assert.That(session.Begin(data), Is.True);
                var command = new VertexBrushCommand(
                    Average(cache.WorldVertices),
                    100f,
                    1f,
                    1f,
                    new Color32(byte.MaxValue, 0, 0, byte.MaxValue),
                    AnimationCurve.Constant(0f, 1f, 1f),
                    VertexPaintBrushMode.Paint,
                    VertexPaintChannel.Red,
                    true,
                    VertexBrushMask.None);
                Assert.That(session.Paint(cache, command), Is.EqualTo(data.ColorCount));
                session.End();
                Undo.FlushUndoRecordObjects();
            }
            finally
            {
                if (ownsCache)
                {
                    cache.Dispose();
                }
            }
        }

        private static Vector3 Average(System.Collections.Generic.IReadOnlyList<Vector3> vertices)
        {
            var sum = Vector3.zero;
            for (var index = 0; index < vertices.Count; index++)
            {
                sum += vertices[index];
            }

            return vertices.Count == 0 ? Vector3.zero : sum / vertices.Count;
        }

        private static bool AllRed(VertexPaintData data)
        {
            for (var index = 0; index < data.ColorCount; index++)
            {
                if (data.GetColor(index).r != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AllClear(VertexPaintData data)
        {
            for (var index = 0; index < data.ColorCount; index++)
            {
                if (!data.GetColor(index).Equals(new Color32(0, 0, 0, 0)))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CleanupLifecycleFixture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (string.Equals(scene.path, LifecycleScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                    else
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            if (AssetDatabase.IsValidFolder(LifecycleFolder))
            {
                AssetDatabase.DeleteAsset(LifecycleFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
