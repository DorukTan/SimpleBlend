using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class ScenePaintInputTests
    {
        private Scene _scene;
        private GameObject _first;
        private GameObject _second;
        private Mesh _mesh;
        private VertexPaintData _data;
        private Object[] _selection;
        private string _settings;

        [SetUp]
        public void SetUp()
        {
            _selection = Selection.objects;
            SimpleBlendTargetLock.Unlock();
            SimpleBlendPaintInput.ClearTemporaryErase();
            _settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            _scene = EditorSceneManager.NewPreviewScene();
            _mesh = TestMeshFactory.CreateQuad();
            _first = CreateTarget("First", out _data);
            _second = CreateTarget("Second", out _);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
            SimpleBlendTargetLock.Unlock();
            SimpleBlendPaintInput.ClearTemporaryErase();
            Selection.objects = _selection;
            if (_settings != null) EditorJsonUtility.FromJsonOverwrite(_settings, SimpleBlendBrushSettings.instance);
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_mesh);
        }

        private GameObject CreateTarget(string name, out VertexPaintData data)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, _scene);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            Assert.That(VertexPaintSetupUtility.Enable(renderer, false, out data).IsValid, Is.True);
            return gameObject;
        }

        [Test]
        public void LockPinsPaintTargetWithoutChangingSelectionOrSavedData()
        {
            Selection.activeGameObject = _second;
            var selection = Selection.objects;
            var data = EditorJsonUtility.ToJson(_data);
            Assert.That(SimpleBlendTargetLock.Lock(_data), Is.True);
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects), Is.SameAs(_data));
            Assert.That(Selection.objects, Is.EqualTo(selection));
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(data));
            SimpleBlendTargetLock.Unlock();
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects), Is.SameAs(_second.GetComponent<VertexPaintData>()));
        }

        [Test]
        public void LostLockedTargetNeverFallsThroughToNewSelection()
        {
            Assert.That(SimpleBlendTargetLock.Lock(_data), Is.True);
            Object.DestroyImmediate(_first);
            Selection.activeGameObject = _second;
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.True);
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects) == null, Is.True);
            Assert.That(PaintWorkspaceTarget.Inspect(new Object[] { SimpleBlendTargetLock.Data }).CanPaint, Is.False);
            SimpleBlendTargetLock.Unlock();
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects), Is.Not.Null);
        }

        [Test]
        public void EditorTransitionReleasesPinEvenBeforeThePaintToolWasActivated()
        {
            Assert.That(SimpleBlendTargetLock.Lock(_data), Is.True);
            Selection.activeGameObject = _second;
            SimpleBlendTargetLock.ReleaseForEditorTransition();
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.False);
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects), Is.SameAs(_second.GetComponent<VertexPaintData>()));
        }

        [Test]
        public void InvalidAndMixedTargetsCannotAcquireAnImplicitTarget()
        {
            Assert.That(SimpleBlendTargetLock.Lock(null), Is.False);
            Assert.That(SimpleBlendTargetLock.Resolve(new Object[] { _first, _second }), Is.Null);
            _data.enabled = false;
            Assert.That(SimpleBlendTargetLock.Lock(_data), Is.False);
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.False);
        }

        [Test]
        public void LostStrokeTargetReleasesSessionStateWithoutRetargeting()
        {
            var stroke = new VertexPaintStrokeSession();
            Assert.That(stroke.Begin(_data), Is.True);
            Object.DestroyImmediate(_first);
            stroke.End();
            Assert.That(stroke.IsActive, Is.False);
            Assert.That(stroke.UndoGroup, Is.EqualTo(-1));
            Assert.That(stroke.HasChanges, Is.False);
            Assert.That(stroke.Begin(_second.GetComponent<VertexPaintData>()), Is.True);
            stroke.End();
        }

        [Test]
        public void ActiveGlobalToolKeepsPinnedTargetAcrossHierarchySelectionAndReleasesOnStop()
        {
            Selection.activeGameObject = _first;
            ToolManager.SetActiveTool<SimpleBlendPaintTool>();
            Assert.That(SimpleBlendTargetLock.Lock(_data), Is.True);
            Selection.activeGameObject = _second;
            Assert.That(ToolManager.activeToolType, Is.EqualTo(typeof(SimpleBlendPaintTool)));
            Assert.That(SimpleBlendTargetLock.Resolve(Selection.objects), Is.SameAs(_data));
            SimpleBlendPaintInput.SetTemporaryErase(true);
            ToolManager.RestorePreviousTool();
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.False);
            Assert.That(SimpleBlendPaintInput.EraseHeld, Is.False);
            Assert.That(Selection.activeGameObject, Is.SameAs(_second));
        }

        [TestCase(true, false, false, true, false, false, true)]
        [TestCase(false, false, false, true, false, false, false)]
        [TestCase(true, true, false, true, false, false, false)]
        [TestCase(true, false, true, true, false, false, false)]
        [TestCase(true, false, false, false, false, false, false)]
        [TestCase(true, false, false, true, true, false, false)]
        [TestCase(true, false, false, true, false, true, false)]
        public void CommandPolicyReservesFocusNavigationAndGestures(bool scene, bool text, bool playing, bool tool, bool gesture, bool alt, bool expected)
        {
            Assert.That(SimpleBlendPaintInput.CanDispatch(scene, text, playing, tool, gesture, alt), Is.EqualTo(expected));
        }

        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(false, false, false)]
        public void OnlyTheGestureOwnerReleasesOnFocusLoss(bool ownsGesture, bool ownerFocused, bool release)
        {
            Assert.That(SimpleBlendPaintTool.ShouldReleaseForFocusLoss(ownsGesture, ownerFocused), Is.EqualTo(release));
        }

        [Test]
        public void CommandsAreRegisteredWithNoDefaultBindingsOrProfileEdits()
        {
            var attributes = typeof(SimpleBlendPaintInput).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => method.GetCustomAttributesData())
                .Where(attribute => typeof(ShortcutAttribute).IsAssignableFrom(attribute.AttributeType)).ToArray();
            Assert.That(attributes.Length, Is.EqualTo(15));
            var ids = attributes.Select(attribute => (string)attribute.ConstructorArguments[0].Value).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            var registered = ShortcutManager.instance.GetAvailableShortcutIds().ToArray();
            foreach (var attribute in attributes)
            {
                Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2), "Only the unbound id/context constructor is permitted.");
                Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(typeof(SimpleBlendShortcutContext)));
                Assert.That(registered, Does.Contain((string)attribute.ConstructorArguments[0].Value));
            }
        }

        [Test]
        public void SizeStrengthStepsAreBoundedAndNeverChangeStoredAlphaOrOperation()
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.Color = new Color(0.2f, 0.3f, 0.4f, 0.7f);
            settings.Mode = VertexPaintBrushMode.Paint;
            settings.Radius = 2;
            settings.Strength = 0.5f;
            SimpleBlendPaintInput.AdjustRadius(settings, true);
            Assert.That(settings.Radius, Is.EqualTo(2.2f).Within(0.0001f));
            SimpleBlendPaintInput.AdjustRadius(settings, false);
            Assert.That(settings.Radius, Is.EqualTo(2).Within(0.0001f));
            SimpleBlendPaintInput.AdjustStrength(settings, true);
            Assert.That(settings.Strength, Is.EqualTo(0.55f).Within(0.0001f));
            settings.Radius = float.PositiveInfinity;
            SimpleBlendPaintInput.AdjustRadius(settings, true);
            Assert.That(float.IsInfinity(settings.Radius) || float.IsNaN(settings.Radius), Is.False);
            settings.Strength = 1;
            SimpleBlendPaintInput.AdjustStrength(settings, true);
            Assert.That(settings.Strength, Is.EqualTo(1));
            settings.Strength = 0;
            SimpleBlendPaintInput.AdjustStrength(settings, false);
            Assert.That(settings.Strength, Is.Zero);
            Assert.That(settings.Color.a, Is.EqualTo(0.7f));
            Assert.That(settings.Mode, Is.EqualTo(VertexPaintBrushMode.Paint));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TemporaryEraseIsLatchedAndKeepsOneUndoAndAuthoritativeAlpha(bool accumulate)
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.Mode = VertexPaintBrushMode.Paint;
            settings.Channel = VertexPaintChannel.Red;
            settings.Radius = 10;
            settings.Strength = 0.5f;
            settings.Flow = 1;
            settings.Falloff = AnimationCurve.Constant(0, 1, 1);
            settings.Accumulate = accumulate;
            settings.NormalMaskEnabled = settings.SubmeshMaskEnabled = settings.ThresholdMaskEnabled = false;
            var before = TestMeshFactory.SolidColors(_mesh.vertexCount, new Color32(100, 110, 120, 77));
            _data.ReplaceColors(before);
            VertexPaintDataAuthoring.Apply(_data);
            var preferences = EditorJsonUtility.ToJson(settings);
            SimpleBlendPaintInput.SetTemporaryErase(true);
            var latched = SimpleBlendPaintInput.EraseAtStrokeStart(settings.Mode);
            SimpleBlendPaintInput.ClearTemporaryErase();
            Assert.That(latched, Is.True);
            Assert.That(SimpleBlendPaintInput.EraseAtStrokeStart(settings.Mode), Is.False);
            var cache = new MeshPaintCache();
            try
            {
                Assert.That(cache.Ensure(_data).IsValid, Is.True);
                var stroke = new VertexPaintStrokeSession();
                Assert.That(stroke.Begin(_data), Is.True);
                stroke.Paint(cache, settings.CreateCommand(Vector3.zero, latched ? VertexPaintBrushMode.Erase : (VertexPaintBrushMode?)null));
                stroke.End();
                Assert.That(_data.GetColor(0), Is.EqualTo(new Color32(50, 110, 120, 77)));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(_data.SerializedColors, Is.EqualTo(before));
                Undo.PerformRedo();
                Assert.That(_data.GetColor(0), Is.EqualTo(new Color32(50, 110, 120, 77)));
                Assert.That(EditorJsonUtility.ToJson(settings), Is.EqualTo(preferences));
            }
            finally { cache.Dispose(); }
        }

        [TestCase(VertexPaintBrushMode.Erase)]
        [TestCase(VertexPaintBrushMode.Smooth)]
        [TestCase(VertexPaintBrushMode.Fill)]
        [TestCase(VertexPaintBrushMode.Sample)]
        public void TemporaryEraseOnlyOverridesPaintDrags(VertexPaintBrushMode mode)
        {
            SimpleBlendPaintInput.SetTemporaryErase(true);
            Assert.That(SimpleBlendPaintInput.EraseAtStrokeStart(mode), Is.False);
        }

        [TestCase(VertexPaintPreviewMode.Red, 0)]
        [TestCase(VertexPaintPreviewMode.Green, 1)]
        [TestCase(VertexPaintPreviewMode.Blue, 2)]
        [TestCase(VertexPaintPreviewMode.Alpha, 3)]
        [TestCase(VertexPaintPreviewMode.RGB, 4)]
        [TestCase(VertexPaintPreviewMode.Density, 5)]
        [TestCase(VertexPaintPreviewMode.Material, 6)]
        public void ExistingPreviewPreferenceIndicesRemainStable(VertexPaintPreviewMode mode, int value)
        {
            Assert.That((int)mode, Is.EqualTo(value));
        }

        [Test]
        public void MaterialViewDoesNotCreateOrDrawAReplacementOverlay()
        {
            var renderer = _first.GetComponent<MeshRenderer>();
            var before = EditorJsonUtility.ToJson(renderer);
            var data = EditorJsonUtility.ToJson(_data);
            var preview = new VertexColorPreviewRenderer();
            Assert.That(preview.Draw(null, _data, VertexPaintPreviewMode.Material), Is.False);
            Assert.That(VertexPaintPreviewUtility.Evaluate(new Color32(1, 2, 3, 4), 1, VertexPaintPreviewMode.Material), Is.EqualTo(Color.clear));
            Assert.That(EditorJsonUtility.ToJson(renderer), Is.EqualTo(before));
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(data));
            preview.Dispose();
        }

        [Test]
        public void DiagnosticMeshPreservesCapturedSectionsWithoutTouchingSource()
        {
            Assert.That(MeshPaintSnapshot.TryCreate(_mesh, out var snapshot).IsValid, Is.True);
            var first = _mesh.GetTriangles(0);
            var second = _mesh.GetTriangles(1);
            var preview = VertexColorPreviewRenderer.CreatePreviewMesh(snapshot);
            try
            {
                Assert.That(preview.subMeshCount, Is.EqualTo(2));
                Assert.That(preview.GetTriangles(0), Is.EqualTo(first));
                Assert.That(preview.GetTriangles(1), Is.EqualTo(second));
                Assert.That(preview.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                Assert.That(_mesh.GetTriangles(0), Is.EqualTo(first));
                Assert.That(_mesh.GetTriangles(1), Is.EqualTo(second));
            }
            finally { Object.DestroyImmediate(preview); }
        }
    }
}
