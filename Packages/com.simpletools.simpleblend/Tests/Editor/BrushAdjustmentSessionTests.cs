using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class BrushAdjustmentSessionTests
    {
        private SimpleBlendBrushSettings _settings;
        private string _backup;

        [SetUp]
        public void SetUp()
        {
            _settings = SimpleBlendBrushSettings.instance;
            _backup = EditorJsonUtility.ToJson(_settings);
            _settings.Radius = 2;
            _settings.Strength = 0.4f;
            _settings.AdjustmentSensitivity = 1;
        }

        [TearDown]
        public void TearDown() => EditorJsonUtility.FromJsonOverwrite(_backup, _settings);

        [TestCase(BrushAdjustment.Radius, 100, 4)]
        [TestCase(BrushAdjustment.Radius, -100, 1)]
        [TestCase(BrushAdjustment.Strength, 100, 0.9f)]
        [TestCase(BrushAdjustment.Strength, -100, 0)]
        public void PreviewIsBaselineRelativeAndLeavesSettingsAndUndoUntouched(BrushAdjustment kind, float delta, float expected)
        {
            var session = new BrushAdjustmentSession();
            var before = EditorJsonUtility.ToJson(_settings);
            var undo = Undo.GetCurrentGroup();
            Assert.That(session.Begin(kind, 10, _settings), Is.True);
            session.Update(35);
            session.Update(10 + delta);
            Assert.That(session.Value, Is.EqualTo(expected).Within(0.00001f));
            Assert.That(EditorJsonUtility.ToJson(_settings), Is.EqualTo(before));
            session.Cancel();
            Assert.That(session.IsActive, Is.False);
            Assert.That(session.Commit(_settings), Is.False);
            Assert.That(EditorJsonUtility.ToJson(_settings), Is.EqualTo(before));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo));
        }

        [TestCase(BrushAdjustment.Radius)]
        [TestCase(BrushAdjustment.Strength)]
        public void CommitChangesOnlyChosenFieldAndCanOnlyHappenOnce(BrushAdjustment kind)
        {
            var session = new BrushAdjustmentSession();
            session.Begin(kind, 0, _settings);
            session.Update(50);
            Assert.That(session.Commit(_settings), Is.True);
            Assert.That(kind == BrushAdjustment.Radius ? _settings.Strength : _settings.Radius,
                Is.EqualTo(kind == BrushAdjustment.Radius ? 0.4f : 2));
            Assert.That(session.Commit(_settings), Is.False);
        }

        [Test]
        public void NoMovementDoesNotRequestPersistenceAndCannotRestartAnActiveSession()
        {
            var session = new BrushAdjustmentSession();
            Assert.That(session.Begin(BrushAdjustment.None, 0, _settings), Is.False);
            Assert.That(session.Begin(BrushAdjustment.Radius, float.NaN, _settings), Is.False);
            Assert.That(session.Begin(BrushAdjustment.Radius, 0, _settings), Is.True);
            Assert.That(session.Begin(BrushAdjustment.Strength, 0, _settings), Is.False);
            Assert.That(session.Commit(_settings), Is.False);
        }

        [TestCase(float.NaN, 1)]
        [TestCase(float.PositiveInfinity, 1)]
        [TestCase(-10, 0.25f)]
        [TestCase(10, 4)]
        [TestCase(2, 2)]
        public void SensitivityIsFiniteAndBounded(float input, float expected)
        {
            _settings.AdjustmentSensitivity = input;
            _settings.Normalize();
            Assert.That(_settings.AdjustmentSensitivity, Is.EqualTo(expected));
        }

        [TestCase(BrushAdjustment.Radius, 10000)]
        [TestCase(BrushAdjustment.Strength, 1)]
        public void ExtremePointerMovementIsBoundedAndNonFiniteUpdatesAreIgnored(BrushAdjustment kind, float maximum)
        {
            var session = new BrushAdjustmentSession();
            session.Begin(kind, 0, _settings);
            session.Update(float.MaxValue);
            Assert.That(session.Value, Is.EqualTo(maximum));
            session.Update(float.NaN);
            Assert.That(session.Value, Is.EqualTo(maximum));
            session.Update(float.NegativeInfinity);
            Assert.That(session.Value, Is.EqualTo(maximum));
        }

        [Test]
        public void SensitivityIsLatchedAndReturningToOriginRestoresThePreview()
        {
            var session = new BrushAdjustmentSession();
            _settings.AdjustmentSensitivity = 2;
            session.Begin(BrushAdjustment.Radius, 50, _settings);
            _settings.AdjustmentSensitivity = 4;
            session.Update(100);
            Assert.That(session.Value, Is.EqualTo(4));
            session.Update(50);
            Assert.That(session.Value, Is.EqualTo(2));
            Assert.That(session.Commit(_settings), Is.False);
        }

        [TestCase(BrushAdjustment.Radius, float.NaN, 1.25f)]
        [TestCase(BrushAdjustment.Radius, -2, 0.0001f)]
        [TestCase(BrushAdjustment.Strength, float.PositiveInfinity, 1)]
        [TestCase(BrushAdjustment.Strength, -2, 0)]
        public void InvalidStartingValuesHaveFinitePreviewsWithoutMutatingPreferences(BrushAdjustment kind, float invalid, float expected)
        {
            if (kind == BrushAdjustment.Radius) _settings.Radius = invalid;
            else _settings.Strength = invalid;
            var before = EditorJsonUtility.ToJson(_settings);
            var session = new BrushAdjustmentSession();
            session.Begin(kind, 0, _settings);
            Assert.That(session.Value, Is.EqualTo(expected));
            session.Cancel();
            Assert.That(EditorJsonUtility.ToJson(_settings), Is.EqualTo(before));
        }

        [TestCase("ReleaseInput")]
        [TestCase("OnTargetLockChanged")]
        public void ToolCleanupDiscardsPreviewWithoutSavingOrAddingUndo(string callback)
        {
            var tool = ScriptableObject.CreateInstance<SimpleBlendPaintTool>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var session = (BrushAdjustmentSession)typeof(SimpleBlendPaintTool).GetField("_adjustment", flags).GetValue(tool);
                var before = EditorJsonUtility.ToJson(_settings);
                var undo = Undo.GetCurrentGroup();
                session.Begin(BrushAdjustment.Radius, 0, _settings);
                session.Update(100);
                typeof(SimpleBlendPaintTool).GetMethod(callback, flags).Invoke(tool, null);
                Assert.That(session.IsActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(_settings), Is.EqualTo(before));
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo));
            }
            finally { Object.DestroyImmediate(tool); }
        }
    }
}
