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

        private TexturePaintTarget Target()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(source, _scene);
            var target = TexturePaintSetup.CreateAutomatic(source, resolution: 256, directory: _assets.AssetPath + "/Paints");
            Selection.activeGameObject = target.gameObject; return target;
        }

        private TextureWeatheringSession Session() { var session = new TextureWeatheringSession(); _sessions.Add(session); return session; }
        private static TextureWeatheringSettings Settings() => new TextureWeatheringSettings { Coverage = 1, Strength = 0.5f };
    }
}
