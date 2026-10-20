using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class BrushFootprintPreviewTests
    {
        private Scene _scene;
        private Mesh _mesh;
        private VertexPaintData _data;
        private MeshPaintCache _cache;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _mesh = TestMeshFactory.CreateQuad(includeColors: true);
            var target = new GameObject("Footprint test");
            SceneManager.MoveGameObjectToScene(target, _scene);
            target.AddComponent<MeshFilter>().sharedMesh = _mesh;
            Assert.That(VertexPaintSetupUtility.Enable(target.AddComponent<MeshRenderer>(), false, out _data).IsValid, Is.True);
            _cache = new MeshPaintCache();
            Assert.That(_cache.Ensure(_data).IsValid, Is.True);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            _cache.Dispose();
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_mesh);
        }

        private static VertexBrushCommand Command(VertexPaintBrushMode mode = VertexPaintBrushMode.Paint,
            float strength = 0.5f, float flow = 0.4f, VertexBrushMask? mask = null, float radius = 3)
            => new VertexBrushCommand(Vector3.zero, radius, strength, flow, new Color32(255, 0, 0, 0),
                AnimationCurve.Constant(0, 1, 1), mode, VertexPaintChannel.Red, true, mask ?? VertexBrushMask.None);

        [TestCase(0.5f, 0.4f, 4)]
        [TestCase(0, 1, 0)]
        [TestCase(1, 0, 0)]
        public void MarkersUseExactInputInfluenceAndNeverModifyColorsOrUndo(float strength, float flow, int count)
        {
            var before = EditorJsonUtility.ToJson(_data);
            var source = _mesh.colors32;
            var undo = Undo.GetCurrentGroup();
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, Command(strength: strength, flow: flow));
            Assert.That(preview.Points.Count, Is.EqualTo(count));
            foreach (var point in preview.Points) Assert.That(point.Weight, Is.EqualTo(strength * flow).Within(0.00001f));
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(before));
            Assert.That(_mesh.colors32, Is.EqualTo(source));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo));
        }

        [Test]
        public void FalloffAndPositionsMatchTheBrushKernelInputs()
        {
            var command = new VertexBrushCommand(_cache.WorldVertices[0], 4, 0.8f, 0.5f,
                new Color32(255, 0, 0, 0), AnimationCurve.Linear(0, 1, 1, 0),
                VertexPaintBrushMode.Paint, VertexPaintChannel.Red, true, VertexBrushMask.None);
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, command);
            Assert.That(preview.Points.Count, Is.EqualTo(4));
            foreach (var point in preview.Points)
            {
                Assert.That(_cache.WorldVertices, Does.Contain(point.Position));
                Assert.That(point.Weight, Is.EqualTo(command.EvaluateWeight(Vector3.Distance(point.Position, command.Center))));
            }
        }

        [Test]
        public void ThresholdMasksCanUseTheCappedStrokeBaselineWithoutChangingIt()
        {
            var mask = new VertexBrushMask(false, Vector3.up, 180, false, 0,
                true, VertexPaintChannel.Green, 0, 0.1f, true);
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, Command(mask: mask));
            Assert.That(preview.Points.Count, Is.EqualTo(1));
            var baseline = new Color32[4];
            preview.Evaluate(_cache, _data, Command(mask: mask), baseline);
            Assert.That(preview.Points.Count, Is.EqualTo(4));
            Assert.That(baseline, Is.EqualTo(new Color32[4]));
        }

        [TestCase(VertexPaintBrushMode.Fill)]
        [TestCase(VertexPaintBrushMode.Sample)]
        public void NonRadiusOperationsDoNotShowAMisleadingFootprint(VertexPaintBrushMode mode)
        {
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, Command(mode));
            Assert.That(preview.Points, Is.Empty);
        }

        [Test]
        public void MarkerAndQueryBudgetsDiscloseIncompletePreviews()
        {
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, Command(), markerBudget: 1);
            Assert.That(preview.Points.Count, Is.EqualTo(1));
            Assert.That(preview.IsComplete, Is.False);
            preview.Evaluate(_cache, _data, Command(radius: 10000));
            Assert.That(preview.IsComplete, Is.False);
            var grid = new UniformVertexGrid();
            grid.Rebuild(_cache.WorldVertices, 1);
            var indices = new List<int>();
            Assert.That(grid.QuerySphereLimited(Vector3.zero, 10000, indices, 8, 2), Is.False);
            Assert.That(indices.Count, Is.LessThanOrEqualTo(2));
            grid.QuerySphere(Vector3.zero, 3, indices);
            Assert.That(indices.Count, Is.EqualTo(4), "Painting queries must retain their complete result.");
        }

        [Test]
        public void PreviewCommandsApplyGestureOverridesWithoutNormalizingOrSavingPreferences()
        {
            var settings = SimpleBlendBrushSettings.instance;
            var backup = EditorJsonUtility.ToJson(settings);
            try
            {
                settings.Radius = 0.12f;
                settings.Strength = 0.5f;
                settings.NormalDirection = Vector3.up * 2;
                var before = EditorJsonUtility.ToJson(settings);
                var command = settings.CreatePreviewCommand(Vector3.zero, radiusOverride: 2, strengthOverride: 0.25f);
                Assert.That(command.Radius, Is.EqualTo(2));
                Assert.That(command.Strength, Is.EqualTo(0.25f));
                Assert.That(EditorJsonUtility.ToJson(settings), Is.EqualTo(before));
            }
            finally { EditorJsonUtility.FromJsonOverwrite(backup, settings); }
        }

        [Test]
        public void DenseCellsWithNoMatchesStillRespectTheWorkBudget()
        {
            var grid = new UniformVertexGrid();
            var vertices = new Vector3[1000];
            for (var index = 0; index < vertices.Length; index++) vertices[index] = Vector3.one * 0.9f;
            grid.Rebuild(vertices, 1);
            var results = new List<int>();
            Assert.That(grid.QuerySphereLimited(Vector3.one * 0.05f, 0.001f, results, 8, 2), Is.False);
            Assert.That(results, Is.Empty);
        }

        [Test]
        public void InvalidRadiusCannotProduceUnsafeMarkerValues()
        {
            var preview = new BrushFootprintPreview();
            preview.Evaluate(_cache, _data, Command(radius: float.NaN));
            Assert.That(preview.Points, Is.Empty);
            Assert.That(preview.IsComplete, Is.False);
        }
    }
}
