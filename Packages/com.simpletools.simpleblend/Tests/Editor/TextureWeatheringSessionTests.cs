using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureWeatheringSessionTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private Object[] _selection;
        private readonly List<TextureWeatheringSession> _sessions = new List<TextureWeatheringSession>();

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope(); _scene = EditorSceneManager.NewPreviewScene();
            _selection = Selection.objects; Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var session in _sessions) session.Dispose(); _sessions.Clear();
            if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
            SimpleBlendTargetLock.Unlock(); SimpleBlendPaintInput.ClearTemporaryErase();
            SimpleBlendTexturePaintTool.Stop(); Selection.objects = _selection; TexturePaintSaveQueue.Flush(); Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); _assets.Dispose();
        }

        [Test]
        public void PreviewUsesOnlyTransientResourcesAndCancelLeavesRendererFilesAndUndoUntouched()
        {
            var target = Target(); var paint = target.Paint; var renderer = target.GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_TestProperty", 19); renderer.SetPropertyBlock(properties);
            AssetDatabase.SaveAssetIfDirty(paint); Undo.ClearAll();
            var bytes = System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(paint)); var baseline = (Color32[])paint.Pixels.Clone();
            var materialState = EditorJsonUtility.ToJson(paint.Material); var group = Undo.GetCurrentGroup();
            var dirty = EditorUtility.IsDirty(paint); var maskDirty = EditorUtility.IsDirty(paint.Mask);
            var mesh = target.GetComponent<MeshFilter>().sharedMesh; var material = renderer.sharedMaterial;
            var session = Session(); session.Begin(target, 1, false, Settings());
            var changes = session.Changes; var previewMaterial = session.PreviewMaterial;
            Assert.That(changes.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(previewMaterial.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(AssetDatabase.Contains(changes), Is.False); Assert.That(AssetDatabase.Contains(previewMaterial), Is.False);
            Assert.That(changes.GetPixels32().Any(pixel => pixel.r > 0), Is.True);
            Assert.That(target.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh)); Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_TestProperty"), Is.EqualTo(19));
            session.Cancel(); Assert.That(changes == null, Is.True); Assert.That(previewMaterial == null, Is.True);
            Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
            Assert.That(paint.Pixels, Is.EqualTo(baseline)); Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(baseline));
            Assert.That(EditorJsonUtility.ToJson(paint.Material), Is.EqualTo(materialState));
            Assert.That(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(paint)), Is.EqualTo(bytes));
            Assert.That(EditorUtility.IsDirty(paint), Is.EqualTo(dirty)); Assert.That(EditorUtility.IsDirty(paint.Mask), Is.EqualTo(maskDirty));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ApplyAndCancelResumeTheStillActiveTextureTool(bool apply)
        {
            var target = Target(); SimpleBlendTexturePaintTool.Start(target, 1, false); var baseline = (Color32[])target.Paint.Pixels.Clone();
            var session = Session(); session.Begin(target, 1, false, Settings());
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.True);
            if (apply)
            {
                Assert.That(session.Apply(), Is.GreaterThan(0)); var after = (Color32[])target.Paint.Pixels.Clone();
                Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
                Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
            }
            else { session.Cancel(); Assert.That(target.Paint.Pixels, Is.EqualTo(baseline)); }
            Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
        }

        [Test]
        public void StartingAnotherPreviewDestroysThePreviousSessionResources()
        {
            var target = Target(); var first = Session(); first.Begin(target, 1, false, Settings()); var oldTexture = first.Changes;
            var second = Session(); second.Begin(target, 1, false, Settings());
            Assert.That(first.Active, Is.False); Assert.That(oldTexture == null, Is.True); Assert.That(second.Active, Is.True);
            second.Cancel(); Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
        }

        [Test]
        public void CancelledGenerationLeavesNoResourcesAndNoBrushBlock()
        {
            var target = Target(); var session = Session(); var baseline = (Color32[])target.Paint.Pixels.Clone();
            Assert.Throws<OperationCanceledException>(() => session.Begin(target, 1, false, Settings(), () => true));
            Assert.That(session.Active, Is.False); Assert.That(session.Changes == null, Is.True);
            Assert.That(session.PreviewMaterial == null, Is.True); Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
            Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
        }

        [TestCase("undo")]
        [TestCase("selection")]
        [TestCase("stale")]
        [TestCase("tool")]
        [TestCase("play")]
        public void LifecycleChangesDiscardPreviewAndDestroyResources(string action)
        {
            var target = Target();
            if (action == "tool") SimpleBlendTexturePaintTool.Start(target, 1, false);
            var session = Session(); session.Begin(target, 1, false, Settings()); var texture = session.Changes;
            if (action == "undo")
            {
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(target.Paint, "Test edit");
                target.Paint.Pixels[0] = new Color32(1, 0, 0, 0); target.Paint.CommitPixels(); Undo.PerformUndo();
            }
            else if (action == "selection")
            {
                Selection.activeGameObject = _scene.GetRootGameObjects().First(go => go != target.gameObject);
                typeof(TextureWeatheringSession).GetMethod("CheckSelection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
            }
            else if (action == "stale")
            { target.Paint.Pixels[0] = new Color32(1, 0, 0, 0); target.Paint.CommitPixels(); session.CheckCurrent(); }
            else if (action == "tool") ToolManager.RestorePreviousTool();
            else typeof(TextureWeatheringSession).GetMethod("OnPlayModeChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { PlayModeStateChange.ExitingEditMode });
            Assert.That(session.Active, Is.False); Assert.That(texture == null, Is.True);
            Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
        }

        [Test]
        public void ProjectAssetSelectionKeepsPreviewButWorkspaceCloseDisposesIt()
        {
            var target = Target(); var workspace = ScriptableObject.CreateInstance<SimpleBlendWorkspace>();
            try
            {
                var session = (TextureWeatheringSession)typeof(SimpleBlendWorkspace).GetField("_weathering", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
                session.Begin(target, 1, false, Settings()); var texture = session.Changes;
                Selection.activeObject = target.Paint;
                typeof(TextureWeatheringSession).GetMethod("CheckSelection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Assert.That(session.Active, Is.True);
                Object.DestroyImmediate(workspace); workspace = null;
                Assert.That(session.Active, Is.False); Assert.That(texture == null, Is.True);
            }
            finally { if (workspace != null) Object.DestroyImmediate(workspace); }
        }

        [Test]
        public void GenerationReportsMonotonicProcessedTexelCountsAndCanBeCancelledAfterProgress()
        {
            var target = Target(); var session = Session(); var last = -1; var expectedTotal = -1; var reports = 0;
            session.Begin(target, 1, false, Settings(), progress: (done, total) =>
            {
                Assert.That(done, Is.GreaterThanOrEqualTo(last)); Assert.That(done, Is.InRange(0, total));
                if (expectedTotal < 0) expectedTotal = total; else Assert.That(total, Is.EqualTo(expectedTotal));
                last = done; reports++;
            });
            Assert.That(last, Is.EqualTo(expectedTotal)); Assert.That(reports, Is.GreaterThan(2)); session.Cancel();
            last = 0;
            Assert.Throws<OperationCanceledException>(() => session.Begin(target, 1, false, Settings(), () => last > 0, (done, total) => last = done));
            Assert.That(session.Active, Is.False);
        }

        [Test]
        public void ChangeOverlayShaderUsesUv4AndDiscardsUnchangedTexels()
        {
            var shader = Shader.Find(TextureWeatheringSession.ShaderName); Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
            var mesh = new Mesh(); var material = new Material(shader); var mask = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            var rt = RenderTexture.GetTemporary(32, 32, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false, true); var command = new CommandBuffer(); var previous = RenderTexture.active;
            try
            {
                mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.uv = Enumerable.Repeat(Vector2.one, 4).ToArray();
                mesh.uv4 = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                mask.SetPixels32(new[] { new Color32(0,0,0,255), new Color32(255,0,0,255) }); mask.filterMode = FilterMode.Point;
                mask.wrapMode = TextureWrapMode.Clamp; mask.Apply(); material.SetTexture("_ChangeMask", mask); material.SetColor("_PreviewColor", Color.white);
                command.SetRenderTarget(rt); command.ClearRenderTarget(true, true, Color.clear);
                command.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1));
                command.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0); Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                Assert.That(readback.GetPixel(4, 16).r, Is.LessThan(0.01f));
                Assert.That(readback.GetPixel(27, 16).r, Is.GreaterThan(0.5f));
            }
            finally
            {
                RenderTexture.active = previous; command.Release(); RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(readback); Object.DestroyImmediate(mask); Object.DestroyImmediate(material); Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ToolConfigurationChangesCancelPreviewWithoutDeactivatingTool(bool actionChange)
        {
            var target = Target(); SimpleBlendTexturePaintTool.Start(target, 1, false);
            var session = Session(); session.Begin(target, 1, false, Settings());
            SimpleBlendTexturePaintTool.Configure(target, actionChange ? 1 : 0, actionChange);
            Assert.That(session.Active, Is.False); Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
        }

        [Test]
        public void FillDiscardsPreviewAndNoOpApplyDoesNotAddUndo()
        {
            var target = Target(); var session = Session();
            session.Begin(target, 1, false, new TextureWeatheringSettings { Coverage = 0 });
            var group = Undo.GetCurrentGroup(); Assert.That(session.Apply(), Is.Zero);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group)); Assert.That(session.Active, Is.False);
            session.Begin(target, 1, false, Settings()); SimpleBlendTexturePaintTool.Fill(target, 1);
            Assert.That(session.Active, Is.False); Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationDuringGenerationCannotPublishTheCandidate(bool fromCancellationPoll)
        {
            var target = Target(); var session = Session(); var baseline = (Color32[])target.Paint.Pixels.Clone();
            var cancelled = false;
            void CancelOnce() { if (!cancelled) { cancelled = true; session.Cancel(); } }
            Assert.Throws<OperationCanceledException>(() => session.Begin(target, 1, false, Settings(),
                fromCancellationPoll ? () => { CancelOnce(); return false; } : null,
                fromCancellationPoll ? null : (done, total) => CancelOnce()));
            Assert.That(session.Active, Is.False); Assert.That(session.Changes == null, Is.True);
            Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
        }

        [Test]
        public void ChangingSceneSelectionDuringGenerationCannotPublishForTheOldTarget()
        {
            var target = Target(); var session = Session(); var changed = false;
            var other = _scene.GetRootGameObjects().First(go => go != target.gameObject);
            Assert.Throws<OperationCanceledException>(() => session.Begin(target, 1, false, Settings(), progress: (done, total) =>
            { if (!changed) { changed = true; Selection.activeGameObject = other; } }));
            Assert.That(session.Active, Is.False); Assert.That(session.Changes == null, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedGenerationKeepsOnlyTheNewerPreview(bool sameSession)
        {
            var target = Target(); var first = Session(); var newer = sameSession ? first : Session();
            var started = false; Texture2D newerTexture = null;
            Assert.Throws<OperationCanceledException>(() => first.Begin(target, 1, false, Settings(), progress: (done, total) =>
            {
                if (started) return;
                started = true; newer.Begin(target, 1, false, Settings()); newerTexture = newer.Changes;
            }));
            Assert.That(newer.Active, Is.True); Assert.That(newerTexture != null, Is.True);
            Assert.That(newer.Changes, Is.SameAs(newerTexture));
            if (!sameSession) Assert.That(first.Active, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(texture => texture.name == "SimpleBlend Weathering Changes"), Is.EqualTo(1));
        }

        [Test]
        public void ProjectSelectionDuringGenerationRetainsTheBoundTarget()
        {
            var target = Target(); var session = Session();
            session.Begin(target, 1, false, Settings(), progress: (done, total) => Selection.activeObject = target.Paint);
            Assert.That(session.Active, Is.True); Assert.That(session.Target, Is.SameAs(target));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GeneratingAndDisplayedPreviewsSuspendInputUntilCancellation(bool cancelDuringGeneration)
        {
            var target = Target(); var session = Session(); var observed = false;
            void Generate() => session.Begin(target, 1, false, Settings(), () => cancelDuringGeneration && observed,
                (done, total) =>
                {
                    observed = true;
                    Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.True);
                    Assert.That(TextureWeatheringSession.BlocksBrush(target), Is.True);
                    Assert.That(SimpleBlendPaintInput.CanDispatch(true, false, false, true, false,
                        suspended: TextureWeatheringSession.BlocksPaintInput), Is.False);
                });
            if (cancelDuringGeneration) Assert.Throws<OperationCanceledException>(() => Generate());
            else { Generate(); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.True); session.Cancel(); }
            Assert.That(observed, Is.True); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
            Assert.That(SimpleBlendPaintInput.CanDispatch(true, false, false, true, false,
                suspended: TextureWeatheringSession.BlocksPaintInput), Is.True);
        }

        [Test]
        public void PreviewFinishesLockedVertexGestureWithoutDeactivatingItsToolOrUnlockingTarget()
        {
            var target = Target();
            var vertexObject = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(vertexObject, _scene);
            Assert.That(VertexPaintSetupUtility.Enable(vertexObject.GetComponent<Renderer>(), false, out var data).IsValid, Is.True);
            Selection.activeGameObject = vertexObject; ToolManager.SetActiveTool<SimpleBlendPaintTool>();
            Assert.That(SimpleBlendTargetLock.Lock(data), Is.True); Selection.activeGameObject = target.gameObject;
            var tool = (SimpleBlendPaintTool)typeof(SimpleBlendPaintTool).GetField("_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var stroke = (VertexPaintStrokeSession)typeof(SimpleBlendPaintTool).GetField("_stroke", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tool);
            Assert.That(stroke.Begin(data), Is.True); SimpleBlendPaintInput.SetTemporaryErase(true);
            var vertexBaseline = (Color32[])data.SerializedColors.Clone(); var paintBaseline = (Color32[])target.Paint.Pixels.Clone();
            var session = Session(); session.Begin(target, 1, false, Settings());
            Assert.That(stroke.IsActive, Is.False); Assert.That(SimpleBlendPaintInput.EraseHeld, Is.False);
            Assert.That(ToolManager.activeToolType, Is.EqualTo(typeof(SimpleBlendPaintTool)));
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.True); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.True);
            session.Cancel(); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
            Assert.That(SimpleBlendTargetLock.IsLocked, Is.True);
            Assert.That(data.SerializedColors, Is.EqualTo(vertexBaseline)); Assert.That(target.Paint.Pixels, Is.EqualTo(paintBaseline));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SecondarySessionPreviewApplyAndCancelKeepPrimaryAndToolContext(bool apply)
        {
            var target = Target(true); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            for (var i = 0; i < target.Paint.Pixels.Length; i++) target.Paint.Pixels[i] = new Color32(180, 0, 219, 37);
            target.Paint.CommitPixels();
            var primary = (Color32[])target.Paint.Pixels.Clone(); var before = (Color32[])second.Pixels.Clone();
            SimpleBlendTexturePaintTool.Start(target, 1, false, 1, true);
            var session = Session(); session.Begin(target, 1, false, Settings(), materialSection: 1);
            var changes = session.Changes;
            Assert.That(session.Section, Is.EqualTo(1)); Assert.That(session.Paint, Is.EqualTo(second));
            Assert.That(session.Matches(target, 1, false, 1), Is.True); Assert.That(session.Matches(target, 1, false), Is.False);
            Assert.That(changes.width, Is.EqualTo(second.Mask.width));
            Assert.That(changes.GetPixels32().Any(pixel => pixel.r == 128), Is.True, "Highlight deltas must use the secondary baseline, not primary pixels.");
            Assert.That(second.Pixels, Is.EqualTo(before)); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.True);
            if (apply)
            {
                Assert.That(session.Apply(), Is.GreaterThan(0)); var expected = (Color32[])second.Pixels.Clone();
                Undo.PerformUndo(); Assert.That(second.Pixels, Is.EqualTo(before));
                Undo.PerformRedo(); Assert.That(second.Pixels, Is.EqualTo(expected));
            }
            else { session.Cancel(); Assert.That(second.Pixels, Is.EqualTo(before)); }
            Assert.That(changes == null, Is.True); Assert.That(target.Paint.Pixels, Is.EqualTo(primary));
            Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True); Assert.That(SimpleBlendTexturePaintTool.Section, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SectionSwitchCancelsGeneratingOrDisplayedSecondaryPreview(bool duringGeneration)
        {
            var target = Target(true); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            SimpleBlendTexturePaintTool.Start(target, 1, false, 1, true);
            var primary = (Color32[])target.Paint.Pixels.Clone(); var pixels = (Color32[])second.Pixels.Clone();
            var session = Session();
            if (duringGeneration)
            {
                var switched = false;
                Assert.Throws<OperationCanceledException>(() => session.Begin(target, 1, false, Settings(), progress: (done, total) =>
                { if (!switched) { switched = true; SimpleBlendTexturePaintTool.SelectSection(target, 0); } }, materialSection: 1));
            }
            else
            {
                session.Begin(target, 1, false, Settings(), materialSection: 1); var texture = session.Changes;
                SimpleBlendTexturePaintTool.SelectSection(target, 0); Assert.That(texture == null, Is.True);
            }
            Assert.That(session.Active, Is.False); Assert.That(session.Changes == null, Is.True);
            Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True); Assert.That(SimpleBlendTexturePaintTool.Section, Is.Zero);
            Assert.That(target.Paint.Pixels, Is.EqualTo(primary)); Assert.That(second.Pixels, Is.EqualTo(pixels));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedSectionGenerationRetainsOnlyTheNewerSecondaryPreview(bool sameSession)
        {
            var target = Target(true); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var first = Session(); var newer = sameSession ? first : Session(); var started = false; Texture2D texture = null;
            Assert.Throws<OperationCanceledException>(() => first.Begin(target, 1, false, Settings(), progress: (done, total) =>
            {
                if (started) return;
                started = true; newer.Begin(target, 1, false, Settings(), materialSection: 1); texture = newer.Changes;
            }, materialSection: 0));
            Assert.That(newer.Active, Is.True); Assert.That(newer.Section, Is.EqualTo(1)); Assert.That(newer.Paint, Is.EqualTo(second));
            Assert.That(newer.Changes, Is.EqualTo(texture)); Assert.That(texture != null, Is.True);
            if (!sameSession) Assert.That(first.Active, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(item => item.name == "SimpleBlend Weathering Changes"), Is.EqualTo(1));
        }

        [Test]
        public void SecondaryHighlightUsesItsOwnMaskDimensionsAndBlueWeightChannel()
        {
            var target = Target(true);
            var palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/FourSurfaces.asset", new[]
            {
                new SurfaceDefinition { Name = "Base" }, new SurfaceDefinition { Name = "Moss" },
                new SurfaceDefinition { Name = "Dirt" }, new SurfaceDefinition { Name = "Snow" }
            });
            var second = TexturePaintSetup.PrepareSection(target, palette, 1, _assets.AssetPath + "/Second.asset");
            // A data-level fixture, not a resolution-change workflow: selected dimensions must never come from primary.
            second.Mask.Reinitialize(512, 512);
            second.Configure(second.Mesh, second.Material, second.Mask, new[] { "Base", "Moss", "Dirt", "Snow" });
            var primary = (Color32[])target.Paint.Pixels.Clone();
            var session = Session(); session.Begin(target, 3, false, Settings(), materialSection: 1);
            Assert.That(target.Paint.SurfaceCount, Is.EqualTo(2)); Assert.That(target.Paint.Mask.width, Is.EqualTo(256));
            Assert.That(session.Changes.width, Is.EqualTo(512)); Assert.That(session.Changes.height, Is.EqualTo(512));
            Assert.That(session.Changes.GetPixels32().Any(pixel => pixel.r == 128), Is.True);
            session.Apply(); Assert.That(second.Pixels.Any(pixel => pixel.b > 0), Is.True);
            Assert.That(target.Paint.Pixels, Is.EqualTo(primary));
        }

        [Test]
        public void ReplacedSectionBindingDuringGenerationCannotPublishOrWriteEitherAsset()
        {
            var target = Target(true); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var primary = (Color32[])target.Paint.Pixels.Clone(); var pixels = (Color32[])second.Pixels.Clone(); var edited = false;
            var session = Session();
            Assert.Throws<InvalidOperationException>(() => session.Begin(target, 1, false, Settings(), progress: (done, total) =>
            {
                if (edited) return; edited = true;
                var serialized = new SerializedObject(target);
                serialized.FindProperty("_sectionPaints").GetArrayElementAtIndex(1).objectReferenceValue = target.Paint;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var materials = target.GetComponent<MeshRenderer>().sharedMaterials; materials[1] = target.Paint.Material;
                target.GetComponent<MeshRenderer>().sharedMaterials = materials;
            }, materialSection: 1));
            Assert.That(session.Active, Is.False); Assert.That(session.Changes == null, Is.True);
            Assert.That(session.PreviewMaterial == null, Is.True); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
            Assert.That(target.Paint.Pixels, Is.EqualTo(primary)); Assert.That(second.Pixels, Is.EqualTo(pixels));
        }

        private TexturePaintTarget Target(bool splitSections = false)
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(source, _scene);
            if (splitSections)
            {
                var mesh = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh); var indices = mesh.triangles;
                mesh.subMeshCount = 2; mesh.SetTriangles(indices.Take(indices.Length / 2).ToArray(), 0);
                mesh.SetTriangles(indices.Skip(indices.Length / 2).ToArray(), 1);
                AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/SplitSource.asset");
                source.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = source.GetComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { renderer.sharedMaterial, renderer.sharedMaterial };
            }
            var target = TexturePaintSetup.CreateAutomatic(source, resolution: 256, directory: _assets.AssetPath + "/Paints");
            Selection.activeGameObject = target.gameObject; return target;
        }

        private TextureWeatheringSession Session() { var session = new TextureWeatheringSession(); _sessions.Add(session); return session; }
        private static TextureWeatheringSettings Settings() => new TextureWeatheringSettings { Coverage = 1, Strength = 0.5f };
    }
}
