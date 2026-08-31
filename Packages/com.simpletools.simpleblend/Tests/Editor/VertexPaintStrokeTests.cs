using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class VertexPaintStrokeTests
    {
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test]
        public void One_stroke_is_one_exact_undo_and_redo_for_authoritative_colors()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = CreateRenderer("Painted", mesh, out var data);
            try
            {
                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var before = CopyColors(data);
                var session = new VertexPaintStrokeSession();
                var command = SolidRedCommand(Vector3.zero);

                Assert.That(session.Begin(data), Is.True);
                var undoGroup = session.UndoGroup;
                Assert.That(session.Paint(cache, command), Is.EqualTo(4));
                Assert.That(session.Paint(cache, command), Is.Zero);
                Assert.That(session.UndoGroup, Is.EqualTo(undoGroup));
                session.End();
                Undo.FlushUndoRecordObjects();

                Assert.That(AllRed(data, byte.MaxValue), Is.True);
                Undo.PerformUndo();
                Assert.That(CopyColors(data), Is.EqualTo(before));
                Undo.PerformRedo();
                Assert.That(AllRed(data, byte.MaxValue), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Real_stroke_on_shared_mesh_does_not_change_sibling_instance()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var paintedObject = CreateRenderer("Painted", mesh, out var paintedData);
            var siblingObject = CreateRenderer("Sibling", mesh, out var siblingData);
            try
            {
                var siblingBefore = CopyColors(siblingData);
                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(paintedData).IsValid, Is.True);
                var session = new VertexPaintStrokeSession();

                Assert.That(session.Begin(paintedData), Is.True);
                Assert.That(session.Paint(cache, SolidRedCommand(Vector3.zero)), Is.EqualTo(4));
                session.End();

                Assert.That(AllRed(paintedData, byte.MaxValue), Is.True);
                Assert.That(CopyColors(siblingData), Is.EqualTo(siblingBefore));
                Assert.That(paintedObject.GetComponent<MeshRenderer>().additionalVertexStreams,
                    Is.Not.SameAs(siblingObject.GetComponent<MeshRenderer>().additionalVertexStreams));
                Assert.That(mesh.colors32, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(paintedObject);
                Object.DestroyImmediate(siblingObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Paint_tool_supports_initialized_mesh_and_skinned_renderers()
        {
            using (var assets = new TestAssetScope())
            {
                var mesh = TestMeshFactory.CreateQuad(includeSkinning: true);
                AssetDatabase.CreateAsset(mesh, $"{assets.AssetPath}/Source.asset");
                var ordinary = CreateRenderer("Ordinary", mesh, out var ordinaryData);
                var skinned = new GameObject("Skinned");
                try
                {
                    var skinnedRenderer = skinned.AddComponent<SkinnedMeshRenderer>();
                    skinnedRenderer.sharedMesh = mesh;
                    var skinnedData = skinned.AddComponent<VertexPaintData>();
                    Assert.That(VertexPaintDataAuthoring.Initialize(skinnedData, skinnedRenderer, false).IsValid, Is.True);

                    Assert.That(SimpleBlendPaintTool.IsSupportedTarget(ordinaryData), Is.True);
                    Assert.That(SimpleBlendPaintTool.IsSupportedTarget(skinnedData), Is.True);
                    Assert.That(Shader.Find("Hidden/SimpleBlend/Red Channel Preview"), Is.Not.Null);
                }
                finally
                {
                    Object.DestroyImmediate(ordinary);
                    Object.DestroyImmediate(skinned);
                }
            }
        }

        [Test]
        public void Paint_tool_toolbar_content_is_safe_during_scriptable_object_construction()
        {
            var tool = ScriptableObject.CreateInstance<SimpleBlendPaintTool>();
            try
            {
                Assert.That(tool.toolbarIcon, Is.Not.Null);
                Assert.That(tool.toolbarIcon.text, Is.EqualTo("SB"));
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }
        }

        private static GameObject CreateRenderer(string name, Mesh mesh, out VertexPaintData data)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            data = gameObject.AddComponent<VertexPaintData>();
            var result = VertexPaintDataAuthoring.Initialize(data, renderer, false);
            Assert.That(result.IsValid, Is.True, result.Message);
            return gameObject;
        }

        private static RedBrushCommand SolidRedCommand(Vector3 center)
        {
            return new RedBrushCommand(
                center,
                3f,
                1f,
                new Color32(byte.MaxValue, 0, 0, byte.MaxValue),
                AnimationCurve.Constant(0f, 1f, 1f));
        }

        private static Color32[] CopyColors(VertexPaintData data)
        {
            var colors = new Color32[data.ColorCount];
            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = data.GetColor(index);
            }

            return colors;
        }

        private static bool AllRed(VertexPaintData data, byte value)
        {
            for (var index = 0; index < data.ColorCount; index++)
            {
                if (data.GetColor(index).r != value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
