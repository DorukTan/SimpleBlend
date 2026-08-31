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

        [Test]
        public void Submesh_fill_is_one_undoable_operation_through_the_stroke_session()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = CreateRenderer("Fill", mesh, out var data);
            try
            {
                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var before = CopyColors(data);
                var command = new VertexBrushCommand(
                    Vector3.zero,
                    1f,
                    1f,
                    1f,
                    new Color32(0, 255, 0, 0),
                    AnimationCurve.Constant(0f, 1f, 1f),
                    VertexPaintBrushMode.Fill,
                    VertexPaintChannel.Green,
                    true,
                    VertexBrushMask.None);
                var session = new VertexPaintStrokeSession();

                Assert.That(session.Begin(data), Is.True);
                Assert.That(session.Fill(cache, command, 1), Is.EqualTo(3));
                session.End();
                Undo.FlushUndoRecordObjects();

                Assert.That(data.GetColor(0).g, Is.EqualTo(255));
                Assert.That(data.GetColor(1).g, Is.Zero);
                Assert.That(data.GetColor(2).g, Is.EqualTo(255));
                Assert.That(data.GetColor(3).g, Is.EqualTo(255));
                Undo.PerformUndo();
                Assert.That(CopyColors(data), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Stroke_results_are_stable_for_sparse_and_dense_events_on_the_same_path()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var sparseObject = CreateRenderer("Sparse", mesh, out var sparseData);
            var denseObject = CreateRenderer("Dense", mesh, out var denseData);
            try
            {
                PaintPath(sparseData, new[] { -1f, 1f });
                PaintPath(denseData, new[] { -1f, -0.5f, 0f, 0.5f, 1f });

                Assert.That(CopyColors(denseData), Is.EqualTo(CopyColors(sparseData)));
            }
            finally
            {
                Object.DestroyImmediate(sparseObject);
                Object.DestroyImmediate(denseObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Brush_overlay_constructs_without_loading_editor_styles()
        {
            var overlay = new SimpleBlendBrushOverlay();

            var content = overlay.CreatePanelContent();

            Assert.That(content, Is.Not.Null);
            Assert.That(content.name, Is.EqualTo("SimpleBlend Brush Settings"));
        }

        [Test]
        public void Brush_settings_normalize_before_creating_a_command()
        {
            var settings = ScriptableObject.CreateInstance<SimpleBlendBrushSettings>();
            try
            {
                settings.Radius = -2f;
                settings.Strength = 4f;
                settings.Flow = -1f;
                settings.NormalDirection = Vector3.zero;
                settings.NormalAngle = 300f;
                settings.SubmeshMaskIndex = -4;
                settings.ThresholdRange = new Vector2(0.9f, 0.2f);
                settings.Falloff = null;

                var command = settings.CreateCommand(Vector3.one);

                Assert.That(command.Radius, Is.GreaterThan(0f));
                Assert.That(command.Strength, Is.EqualTo(1f));
                Assert.That(command.Flow, Is.Zero);
                Assert.That(command.Falloff, Is.Not.Null);
                Assert.That(command.Mask.NormalDirection, Is.EqualTo(Vector3.up));
                Assert.That(command.Mask.NormalAngle, Is.EqualTo(180f));
                Assert.That(command.Mask.SubmeshIndex, Is.Zero);
                Assert.That(command.Mask.ThresholdMinimum, Is.EqualTo(0.2f));
                Assert.That(command.Mask.ThresholdMaximum, Is.EqualTo(0.9f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
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

        private static VertexBrushCommand SolidRedCommand(Vector3 center)
        {
            return new VertexBrushCommand(
                center,
                3f,
                1f,
                1f,
                new Color32(byte.MaxValue, 0, 0, byte.MaxValue),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);
        }

        private static void PaintPath(VertexPaintData data, float[] xCoordinates)
        {
            var cache = new MeshPaintCache();
            Assert.That(cache.Ensure(data).IsValid, Is.True);
            var session = new VertexPaintStrokeSession();
            Assert.That(session.Begin(data), Is.True);
            for (var index = 0; index < xCoordinates.Length; index++)
            {
                var command = new VertexBrushCommand(
                    new Vector3(xCoordinates[index], 0f, -1f),
                    1.1f,
                    1f,
                    0.25f,
                    new Color32(255, 0, 0, 0),
                    AnimationCurve.Constant(0f, 1f, 1f),
                    VertexPaintBrushMode.Paint,
                    VertexPaintChannel.Red,
                    true,
                    VertexBrushMask.None);
                session.Paint(cache, command);
            }

            session.End();
            cache.Dispose();
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
