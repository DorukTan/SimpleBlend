using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class PaintFeedbackTests
    {
        private Scene _scene;
        private Mesh _mesh;
        private GameObject _object;
        private VertexPaintData _data;
        private MeshPaintCache _cache;
        private string _settings;

        [SetUp]
        public void SetUp()
        {
            SimpleBlendPaintFeedback.Clear();
            _settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            _scene = EditorSceneManager.NewPreviewScene();
            _mesh = TestMeshFactory.CreateQuad(includeColors: true);
            _object = new GameObject("Paint feedback target");
            SceneManager.MoveGameObjectToScene(_object, _scene);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            Assert.That(VertexPaintSetupUtility.Enable(renderer, false, out _data).IsValid, Is.True);
            _cache = new MeshPaintCache();
            Assert.That(_cache.Ensure(_data).IsValid, Is.True);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendPaintFeedback.Clear();
            _cache?.Dispose();
            EditorJsonUtility.FromJsonOverwrite(_settings, SimpleBlendBrushSettings.instance);
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_mesh);
        }

        private static VertexBrushCommand Command(VertexPaintBrushMode mode = VertexPaintBrushMode.Paint,
            bool accumulate = true, float strength = 1, VertexBrushMask? mask = null, Vector3? center = null,
            float radius = 3, byte red = 255)
        {
            return new VertexBrushCommand(center ?? Vector3.zero, radius, strength, 1,
                new Color32(red, 0, 0, 0), AnimationCurve.Constant(0, 1, 1), mode,
                VertexPaintChannel.Red, accumulate, mask ?? VertexBrushMask.None);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RepeatedDabsReportDistinctNetVerticesAndPreserveExactUndo(bool accumulate)
        {
            var before = (Color32[])_data.SerializedColors.Clone();
            var sourceBefore = _mesh.colors32;
            var session = new VertexPaintStrokeSession();
            Assert.That(session.Begin(_data), Is.True);
            session.Paint(_cache, Command(accumulate: accumulate, strength: 0.5f));
            session.Paint(_cache, Command(accumulate: accumulate, strength: 0.5f, center: new Vector3(0.7f, 0, 0)));
            var after = (Color32[])_data.SerializedColors.Clone();
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.EqualTo(4));
            Assert.That(result.Sections, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(session.End(), Is.Null);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_data.SerializedColors, Is.EqualTo(before));
            Undo.PerformRedo();
            Assert.That(_data.SerializedColors, Is.EqualTo(after));
            Assert.That(_mesh.colors32, Is.EqualTo(sourceBefore));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FillAndSectionMasksIncludeNeighboringSectionsThroughSharedVertices(bool useMask)
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            var mask = new VertexBrushMask(false, Vector3.up, 180, useMask, 0,
                false, VertexPaintChannel.Red, 0, 1, true);
            session.Fill(_cache, Command(VertexPaintBrushMode.Fill, mask: mask), useMask ? -1 : 0);
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.EqualTo(3));
            Assert.That(result.Sections, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void NonSharedVertexReportsOnlyItsOwnSection()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command(center: _cache.WorldVertices[1], radius: 0.1f));
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.EqualTo(1));
            Assert.That(result.Sections, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void VerticesRestoredWithinTheStrokeAreNotNetChanges()
        {
            for (var index = 0; index < _data.ColorCount; index++)
            {
                var color = _data.SerializedColors[index];
                color.r = 50;
                _data.SerializedColors[index] = color;
            }
            VertexPaintDataAuthoring.Apply(_data);
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Fill(_cache, Command(VertexPaintBrushMode.Fill), -1);
            session.Fill(_cache, Command(VertexPaintBrushMode.Fill, red: 50), -1);
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.Zero);
            Assert.That(result.Sections, Is.Empty);
            Assert.That(SimpleBlendPaintFeedback.DescribeResult(result), Does.Contain("no vertex colors changed"));
        }

        [TestCase(0, 3)]
        [TestCase(1, 0.1f)]
        public void ZeroInfluenceOrSparseReachProducesExplicitNoChangeFeedback(float strength, float radius)
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command(strength: strength, radius: radius));
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.Zero);
            Assert.That(result.Sections, Is.Empty);
            Assert.That(result.NoRadiusCoverage, Is.EqualTo(radius < 0.2f));
            Assert.That(SimpleBlendPaintFeedback.DescribeResult(result), Does.Contain(radius < 0.2f ? "no vertices were inside" : "mesh detail"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RejectedNormalOrThresholdMasksDoNotReportChangedVertices(bool normalMask)
        {
            var mask = new VertexBrushMask(normalMask, Vector3.down, 0, false, 0,
                !normalMask, VertexPaintChannel.Green, 0.9f, 1, true);
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command(mask: mask));
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.Zero);
            Assert.That(result.Sections, Is.Empty);
            Assert.That(result.NoRadiusCoverage, Is.False);
        }

        [Test]
        public void SmoothReportsItsActualChangesRatherThanEveryCandidate()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command(VertexPaintBrushMode.Smooth));
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.EqualTo(3));
            Assert.That(result.Sections, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void FillAndReusedSessionsCannotInheritAnEarlierNoReachDiagnostic()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command(radius: 0.1f));
            Assert.That(session.End().NoRadiusCoverage, Is.True);
            session.Begin(_data);
            session.Paint(_cache, Command(strength: 0));
            Assert.That(session.End().NoRadiusCoverage, Is.False);
            session.Begin(_data);
            session.Fill(_cache, Command(VertexPaintBrushMode.Fill), 99);
            Assert.That(session.End().NoRadiusCoverage, Is.False, "An empty Fill section is not radius-based.");
        }

        [Test]
        public void PaintingTheAlreadyFilledValueIsNotMissingGeometryCoverage()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Fill(_cache, Command(VertexPaintBrushMode.Fill), -1);
            session.End();
            session.Begin(_data);
            session.Paint(_cache, Command());
            var result = session.End();
            Assert.That(result.ChangedVertexCount, Is.Zero);
            Assert.That(result.NoRadiusCoverage, Is.False);
        }

        [Test]
        public void NoAttemptOrLostTargetCannotManufactureASuccessReport()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            Assert.That(session.End(), Is.Null);
            session.Begin(_data);
            session.Paint(_cache, Command());
            Object.DestroyImmediate(_object);
            Assert.That(session.End(), Is.Null);
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void MismatchedResultSnapshotsAreDiscarded()
        {
            Assert.That(VertexPaintStrokeResult.Create(new Color32[1], _data.SerializedColors,
                new HashSet<int> { 0 }, _cache.Snapshot.Topology), Is.Null);
        }

        [Test]
        public void HoverReadsAssignedSlotNamesWithoutChangingMaterialsDataOrPreferences()
        {
            var shader = Shader.Find(VertexColorPreviewRenderer.PreviewShaderName);
            var first = new Material(shader) { name = "Stone" };
            var second = new Material(shader) { name = "Moss" };
            try
            {
                var renderer = _object.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { first, second };
                var data = EditorJsonUtility.ToJson(_data);
                var material = EditorJsonUtility.ToJson(second);
                var settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
                var undo = Undo.GetCurrentGroup();
                SimpleBlendPaintFeedback.SetHover(_data, 1);
                Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance),
                    Does.Contain("Under pointer: section 1 · Moss"));
                Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { first, second }));
                Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(data));
                Assert.That(EditorJsonUtility.ToJson(second), Is.EqualTo(material));
                Assert.That(EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance), Is.EqualTo(settings));
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo));
                second.name = "Renamed Moss";
                SimpleBlendPaintFeedback.SetHover(_data, 1);
                Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance),
                    Does.Contain("Renamed Moss"));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test]
        public void MissingSlotsAndInvalidSectionsDoNotClaimAnAssignedMaterial()
        {
            _object.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { null };
            Assert.That(SimpleBlendPaintFeedback.DescribeSection(_data, 0), Does.Contain("unassigned material slot"));
            Assert.That(SimpleBlendPaintFeedback.DescribeSection(_data, 1), Does.Contain("no matching material slot"));
            Assert.That(SimpleBlendPaintFeedback.DescribeSection(_data, 2), Is.EqualTo("Section unavailable."));
        }

        [Test]
        public void BrushDescriptionSeparatesAlphaFromStrengthAndNamesFillScopeAndMasks()
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.Mode = VertexPaintBrushMode.Fill;
            settings.Channel = VertexPaintChannel.Alpha;
            settings.FillScope = VertexPaintFillScope.HitSubmesh;
            settings.SubmeshMaskEnabled = true;
            settings.SubmeshMaskIndex = 1;
            var message = SimpleBlendPaintFeedback.DescribeBrush(settings);
            Assert.That(message, Does.Contain("Alpha (stored vertex channel)"));
            Assert.That(message, Does.Contain("hit section"));
            Assert.That(message, Does.Contain("section mask 1"));
            Assert.That(message, Does.Contain("Shared vertices"));
            settings.Mode = VertexPaintBrushMode.Sample;
            message = SimpleBlendPaintFeedback.DescribeBrush(settings);
            Assert.That(message, Does.Contain("RGBA"));
            Assert.That(message, Does.Not.Contain("section mask"));
        }

        [Test]
        public void IdenticalHoverDoesNotCreateARepaintLoopAndClearIsIdempotent()
        {
            var notifications = 0;
            System.Action observer = () => notifications++;
            SimpleBlendPaintFeedback.Changed += observer;
            try
            {
                SimpleBlendPaintFeedback.SetHover(_data, 0);
                SimpleBlendPaintFeedback.SetHover(_data, 0);
                Assert.That(notifications, Is.EqualTo(1));
                SimpleBlendPaintFeedback.Clear();
                SimpleBlendPaintFeedback.Clear();
                Assert.That(notifications, Is.EqualTo(2));
            }
            finally { SimpleBlendPaintFeedback.Changed -= observer; }
        }

        [Test]
        public void UndoAndTargetChangesClearHistoricalResults()
        {
            var session = new VertexPaintStrokeSession();
            session.Begin(_data);
            session.Paint(_cache, Command());
            var result = session.End();
            SimpleBlendPaintFeedback.SetResult(_data, result);
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance), Does.Contain("Last stroke:"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance), Does.Not.Contain("Last stroke:"));
            var other = new GameObject("Other");
            SceneManager.MoveGameObjectToScene(other, _scene);
            var otherData = other.AddComponent<VertexPaintData>();
            SimpleBlendPaintFeedback.SetResult(_data, result);
            SimpleBlendPaintFeedback.SetHover(_data, 0);
            SimpleBlendPaintFeedback.SetHover(otherData, 0);
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance), Does.Not.Contain("Under pointer:"));
            Assert.That(SimpleBlendPaintFeedback.GetMessage(otherData, SimpleBlendBrushSettings.instance), Does.Not.Contain("Last stroke:"));
        }

        [Test]
        public void ManyAffectedSectionsAreAllRetainedButSummaryIsBounded()
        {
            var triangles = new List<int>();
            var ids = new List<int>();
            for (var index = 0; index < 12; index++)
            {
                triangles.AddRange(new[] { 0, 1, 2 });
                ids.Add(index);
            }
            var topology = VertexPaintTopology.Create(3, triangles, ids, 12);
            var before = new Color32[3];
            var after = new[] { new Color32(255, 0, 0, 0), new Color32(), new Color32() };
            var result = VertexPaintStrokeResult.Create(before, after, new HashSet<int> { 0 }, topology);
            Assert.That(result.ChangedVertexCount, Is.EqualTo(1));
            Assert.That(result.Sections.Count, Is.EqualTo(12));
            Assert.That(SimpleBlendPaintFeedback.DescribeResult(result), Does.Contain("(+4 more)"));
        }

        [Test]
        public void CurrentStrokeDescribesActualEraseAndDoesNotLeakToAnotherTarget()
        {
            SimpleBlendBrushSettings.instance.Mode = VertexPaintBrushMode.Paint;
            SimpleBlendPaintFeedback.SetActive(_data, Command(VertexPaintBrushMode.Erase));
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance),
                Does.Contain("Current stroke: Erase · Red"));
            SimpleBlendPaintFeedback.SetResult(_data, null);
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance),
                Does.Not.Contain("Current stroke:"));
            SimpleBlendPaintFeedback.SetActive(_data, Command(VertexPaintBrushMode.Erase));
            SimpleBlendPaintFeedback.SetHover(null, -1);
            Assert.That(SimpleBlendPaintFeedback.GetMessage(_data, SimpleBlendBrushSettings.instance),
                Does.Not.Contain("Current stroke:"));
        }
    }
}
