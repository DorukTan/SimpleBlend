using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureSmoothingTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private string _settings;
        private Object[] _selection;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope(); _scene = EditorSceneManager.NewPreviewScene();
            _settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance); _selection = Selection.objects;
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop(); TexturePaintSaveQueue.Flush(); Undo.ClearAll();
            EditorJsonUtility.FromJsonOverwrite(_settings, SimpleBlendBrushSettings.instance); Selection.objects = _selection;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); _assets.Dispose();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void SmoothBlendsAllWeightsAcrossUvChartsAndPersistsWithOneUndo(bool four, bool includeBase)
        {
            var target = Target(four: four); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? (includeBase ? new Color32() : new Color32(255, 0, 0, 0))
                : four ? new Color32(0, 0, 255, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 0, false, Command(), smooth: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.GreaterThan(0)); Assert.That(stroke.End(), Is.GreaterThan(0));
            var after = (Color32[])target.Paint.Pixels.Clone(); var seam = 0; var gutters = 0;
            foreach (var pixel in atlas.Pixels)
            {
                Assert.That(after[pixel].a, Is.EqualTo(before[pixel].a));
                if (!four) Assert.That(after[pixel].b, Is.EqualTo(before[pixel].b));
                Assert.That(after[pixel].r + after[pixel].g + (four ? after[pixel].b : 0), Is.LessThanOrEqualTo(255));
                var point = atlas.Positions[pixel];
                if (Mathf.Abs(point.x) < .02f && Mathf.Abs(point.y) < .1f)
                { Assert.That(four ? after[pixel].b : after[pixel].g, Is.InRange(40, 215)); seam++; }
                if (point.sqrMagnitude > .31f * .31f) Assert.That(after[pixel], Is.EqualTo(before[pixel]));
                if (!after[pixel].Equals(before[pixel]) && atlas.Coverage[pixel] != pixel) gutters++;
            }
            // A second boundary dab reaches chart-edge padding; the first dab verifies the seam itself.
            Assert.That(seam, Is.GreaterThan(10));
            for (var i = 0; i < after.Length; i++) if (atlas.Coverage[i] < 0) Assert.That(after[i], Is.EqualTo(before[i]));
            Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(after));
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
            TexturePaintSaveQueue.Flush(); var path = AssetDatabase.GetAssetPath(target.Paint);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(path).Pixels, Is.EqualTo(after));
            stroke.Begin(target, atlas, 1, false, Command(), smooth: true);
            stroke.Dab(new Vector3(0, .48f, 0), Vector3.back); stroke.End();
            foreach (var pixel in atlas.Pixels)
                if (atlas.Coverage[pixel] != pixel && !target.Paint.Pixels[pixel].Equals(after[pixel]))
                { Assert.That(target.Paint.Pixels[pixel].a, Is.EqualTo(after[pixel].a)); gutters++; }
            Assert.That(gutters, Is.GreaterThan(0));
        }

        [TestCase("shell", false)]
        [TestCase("fold", true)]
        public void SeparatePartsOrOppositeNormalGroupsCannotContaminateEachOther(string geometry, bool opposite)
        {
            var target = Target(geometry, opposite: opposite); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.z < .01f ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), throughPaint: true, facing: TexturePaintFacing.EitherDirection, smooth: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.Zero); Assert.That(stroke.End(), Is.Zero);
            Assert.That(target.Paint.Pixels, Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HiddenSourcesRequireThroughReachEvenOnTheSameConnectedFold(bool through)
        {
            var target = Target("fold"); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.z < .01f ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var cameraObject = new GameObject("View"); SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -2); camera.enabled = false;
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: new TexturePaintVisibility(atlas, new TexturePaintView(camera)),
                throughPaint: through, smooth: true);
            var changed = stroke.Dab(Vector3.zero, Vector3.back); stroke.End();
            Assert.That(changed > 0, Is.EqualTo(through));
        }

        [TestCase("uniform")]
        [TestCase("zero strength")]
        [TestCase("zero flow")]
        public void NoOpSmoothDoesNotAddUndoDirtyFlagsOrFileWrites(string reason)
        {
            var target = Target(); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => reason == "uniform" ? new Color32(80, 40, 0, 0)
                : point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var path = AssetDatabase.GetAssetPath(target.Paint);
            var bytes = System.IO.File.ReadAllBytes(path); var undo = Undo.GetCurrentGroup();
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(strength: reason == "zero strength" ? 0 : 1,
                flow: reason == "zero flow" ? 0 : 1), smooth: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.Zero); Assert.That(stroke.End(), Is.Zero);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo)); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(target.Paint), Is.False); Assert.That(EditorUtility.IsDirty(target.Paint.Mask), Is.False);
            TexturePaintSaveQueue.Flush(); Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [Test]
        public void SourceTexelsAreAveragedOnceRegardlessOfGridAndGutterTraversalOrder()
        {
            var target = Target(); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var stroke = new TexturePaintStroke(); stroke.Begin(target, atlas, 1, false, Command(), smooth: true);
            stroke.Dab(new Vector3(0, .48f, 0), Vector3.back); stroke.End(); var expected = (Color32[])target.Paint.Pixels.Clone();
            Undo.PerformUndo();
            var grid = typeof(TexturePaintAtlas).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(atlas);
            var cells = (IDictionary)typeof(UniformVertexGrid).GetField("_cells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(grid);
            foreach (var values in cells.Values) ((List<int>)values).Reverse();
            stroke.Begin(target, atlas, 2, false, Command(), smooth: true);
            stroke.Dab(new Vector3(0, .48f, 0), Vector3.back); stroke.End();
            Assert.That(target.Paint.Pixels, Is.EqualTo(expected), "Selected surface and candidate order must not change all-weight smoothing.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LowFlowBuildUpRetainsSubByteCoverageWhileCappedStrokesStayCapped(bool accumulate)
        {
            var target = Target(); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            var tip = TextureBrushTip.Capture(new TextureBrushShape { Hardness = 1, LimitOpacity = true });
            stroke.Begin(target, atlas, 1, false, Command(.4f, .001f, accumulate), tip: tip, smooth: true);
            stroke.Dab(Vector3.zero, Vector3.back); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            for (var i = 0; i < 100; i++) stroke.Dab(Vector3.zero, Vector3.back);
            Assert.That(stroke.End() > 0, Is.EqualTo(accumulate));
        }

        [Test]
        public void ShapedTipAndFalloffLimitSmoothWrites()
        {
            var target = Target(); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            var tip = TextureBrushTip.Capture(new TextureBrushShape { Tip = TextureBrushTipKind.Flat, Roundness = .2f, Hardness = .5f });
            var falloff = AnimationCurve.Linear(0, 1, 1, 0);
            stroke.Begin(target, atlas, 1, false, Command(falloff: falloff), tip: tip, smooth: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.GreaterThan(0)); stroke.End();
            foreach (var pixel in atlas.Pixels)
                if (tip.Evaluate(new Vector2(atlas.Positions[pixel].x, -atlas.Positions[pixel].y) / .3f, falloff) <= 0)
                    Assert.That(target.Paint.Pixels[pixel], Is.EqualTo(before[pixel]));
        }

        [Test]
        public void SecondarySmoothingAndAutomaticRoutingDoNotRequireMatchingSurfaceNames()
        {
            var target = Target("section");
            var ray = new Ray(new Vector3(0, 0, 2), Vector3.back);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, true, 0, 1, null, out _, out _, out _, smooth: true), Is.False);
            var second = TexturePaintSetup.PrepareSection(target, Palette(true, "Other"), 1, _assets.AssetPath + "/Second.asset");
            var primary = (Color32[])target.Paint.Pixels.Clone(); var atlas = Atlas(target, 1);
            Seed(second, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 0, 255, 0));
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, true, 0, 1, null, out _, out _, out _), Is.False);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, true, 0, 1, null, out var hit, out _, out _, smooth: true), Is.True);
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, false, 0, 1, null, out _, out _, out _, smooth: true), Is.False);
            var stroke = new TexturePaintStroke(); stroke.Begin(target, atlas, 3, false, Command(), materialSection: 1, smooth: true);
            Assert.That(stroke.Dab(new Vector3(0, 0, .02f), Vector3.back), Is.GreaterThan(0)); stroke.End();
            Assert.That(target.Paint.Pixels, Is.EqualTo(primary)); Assert.That(second.Pixels.Any(color => color.r > 0 && color.b > 0), Is.True);
        }

        [Test]
        public void ActiveToolKeepsSmoothAcrossSurfaceSectionAndPresetChangesAndRejectsFill()
        {
            var target = Target("section"); TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            SimpleBlendTexturePaintTool.Start(target, 1, false, smooth: true);
            SimpleBlendTexturePaintTool.Configure(target, 2, false);
            SimpleBlendTexturePaintTool.SelectSection(target, 1); SimpleBlendTexturePaintTool.SelectSection(target, 0);
            SimpleBlendBrushPreset.ApplyBuiltIn(1, SimpleBlendBrushSettings.instance);
            Assert.That(SimpleBlendTexturePaintTool.Smooth, Is.True); Assert.That(SimpleBlendTexturePaintTool.Surface, Is.EqualTo(2));
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            var tool = typeof(SimpleBlendTexturePaintTool).GetField("_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var stroke = (TexturePaintStroke)typeof(SimpleBlendTexturePaintTool).GetField("_stroke", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tool);
            stroke.Begin(target, Atlas(target), 2, false, Command(), smooth: true); stroke.Dab(Vector3.zero, Vector3.back);
            SimpleBlendTexturePaintTool.FinishCurrentStroke();
            Assert.That(SimpleBlendTexturePaintTool.LastStroke, Does.Contain("uniform"));
            Assert.That(SimpleBlendTexturePaintTool.LastStroke, Does.Not.Contain("selected surface"));
            Assert.Throws<ArgumentException>(() => SimpleBlendTexturePaintTool.Start(target, 1, true, smooth: true));
            Assert.Throws<ArgumentException>(() => SimpleBlendTexturePaintTool.Configure(target, 1, true, smooth: true));
            Assert.That(SimpleBlendTexturePaintTool.Smooth, Is.True);
            Assert.Throws<InvalidOperationException>(() => SimpleBlendTexturePaintTool.Fill(target, 1));
            SimpleBlendTexturePaintTool.Configure(target, 1, true);
            Assert.That(SimpleBlendTexturePaintTool.Smooth, Is.False); Assert.That(SimpleBlendTexturePaintTool.Erase, Is.True);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
        }

        [Test]
        public void SmoothFillAndConflictingEraseRejectBeforeChangingPaintOrUndo()
        {
            var target = Target(); var atlas = Atlas(target); var before = (Color32[])target.Paint.Pixels.Clone(); var undo = Undo.GetCurrentGroup();
            var stroke = new TexturePaintStroke();
            Assert.Throws<ArgumentException>(() => stroke.Begin(target, atlas, 1, true, Command(), smooth: true));
            Assert.That(stroke.Active, Is.False);
            stroke.Begin(target, atlas, 1, false, Command(), smooth: true);
            Assert.Throws<InvalidOperationException>(() => stroke.Dab(Vector3.zero, Vector3.back, fill: true)); stroke.End();
            Assert.That(target.Paint.Pixels, Is.EqualTo(before)); Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmoothingUsesWorldSpaceFootprintsUnderNonuniformOrMirroredTransforms(bool mirrored)
        {
            var target = Target(); target.transform.localScale = new Vector3(mirrored ? -2 : 2, .5f, 1.5f);
            target.transform.rotation = Quaternion.Euler(0, 20, 0); target.transform.position = new Vector3(2, 0, 1);
            var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke(); var center = target.transform.position;
            stroke.Begin(target, atlas, 1, false, Command(), smooth: true);
            Assert.That(stroke.Dab(center, target.transform.TransformDirection(Vector3.back)), Is.GreaterThan(0)); stroke.End();
            for (var i = 0; i < atlas.Pixels.Count; i++)
                if (Vector3.Distance(atlas.WorldPosition(i), center) > .31f)
                    Assert.That(target.Paint.Pixels[atlas.Pixel(i)], Is.EqualTo(before[atlas.Pixel(i)]));
        }

        [Test]
        public void OutOfDepthSourcesCannotContaminateAConnectedVisiblePart()
        {
            var target = Target("fold", gap: .2f); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.z < .1f ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), throughPaint: true, smooth: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.Zero); stroke.End();
            Assert.That(target.Paint.Pixels, Is.EqualTo(before));
        }

        [Test]
        public void WeatheringFinishesSmoothStrokePausesInputAndPreservesTheActiveAction()
        {
            var target = Target(); var atlas = Atlas(target);
            Seed(target.Paint, atlas, point => point.x < 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 255, 0, 0));
            SimpleBlendTexturePaintTool.Start(target, 1, false, smooth: true);
            var tool = typeof(SimpleBlendTexturePaintTool).GetField("_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var stroke = (TexturePaintStroke)typeof(SimpleBlendTexturePaintTool).GetField("_stroke", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tool);
            stroke.Begin(target, atlas, 1, false, Command(), smooth: true); stroke.Dab(Vector3.zero, Vector3.back);
            var beforePreview = (Color32[])target.Paint.Pixels.Clone();
            using (var session = new TextureWeatheringSession())
            {
                var settings = new TextureWeatheringSettings { Coverage = 1, Strength = .5f };
                session.Begin(target, 1, false, settings);
                Assert.That(stroke.Active, Is.False); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.True);
                Assert.That(SimpleBlendTexturePaintTool.Smooth, Is.True); Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
                session.Cancel(); Assert.That(target.Paint.Pixels, Is.EqualTo(beforePreview));
                Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
                session.Begin(target, 1, false, settings);
                SimpleBlendTexturePaintTool.Configure(target, 1, false, smooth: false);
                Assert.That(session.Active, Is.False); Assert.That(TextureWeatheringSession.BlocksPaintInput, Is.False);
                Assert.That(SimpleBlendTexturePaintTool.Smooth, Is.False); Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            }
        }

        private TexturePaintTarget Target(string geometry = "seam", bool four = false, bool opposite = false, float gap = .02f)
        {
            var positions = new List<Vector3> { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0),
                new Vector3(-.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
            var normals = Enumerable.Repeat(Vector3.back, 6).ToList(); var triangles = Enumerable.Range(0, 6).ToList();
            if (geometry != "seam")
            {
                positions.AddRange(positions.Take(6).Select(point => point + Vector3.forward * gap).ToArray());
                normals.AddRange(Enumerable.Repeat(opposite ? Vector3.forward : Vector3.back, 6)); triangles.AddRange(Enumerable.Range(6, 6));
                if (geometry == "fold")
                {
                    positions.AddRange(new[] { new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,.5f,gap), new Vector3(-.5f,.5f,gap) });
                    normals.AddRange(Enumerable.Repeat(Vector3.up, 4)); triangles.AddRange(new[] { 12, 13, 14, 12, 14, 15 });
                }
            }
            var mesh = new Mesh { name = "Seamed surface", vertices = positions.ToArray(), normals = normals.ToArray() };
            mesh.uv = positions.Select((point, i) => new Vector2(i % 3 == 1 ? 0 : i % 3 == 2 ? 1 : 0, i % 3 == 0 ? 0 : 1)).ToArray();
            if (geometry == "section") { mesh.subMeshCount = 2; mesh.SetTriangles(triangles.Take(6).ToArray(), 0); mesh.SetTriangles(triangles.Skip(6).ToArray(), 1); }
            else mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/Source.asset");
            var source = new GameObject("Surface", typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(source, _scene);
            source.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, _assets.AssetPath + "/Source.mat");
            source.GetComponent<MeshRenderer>().sharedMaterials = geometry == "section" ? new[] { material, material } : new[] { material };
            var target = TexturePaintSetup.CreateAutomatic(source, resolution: 256, palette: Palette(four, "Main"), directory: _assets.AssetPath + "/Paints");
            Selection.activeGameObject = target.gameObject; return target;
        }

        private SurfacePalette Palette(bool four, string prefix) => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/" + prefix + ".asset",
            Enumerable.Range(0, four ? 4 : 3).Select(i => new SurfaceDefinition { Name = prefix + i }).ToArray());

        private static TexturePaintAtlas Atlas(TexturePaintTarget target, int section = 0)
        {
            var paint = target.PaintForSection(section); var atlas = new TexturePaintAtlas(paint.Mesh, section, paint.Mask.width);
            atlas.SetTransform(target.transform.localToWorldMatrix); return atlas;
        }

        private static VertexBrushCommand Command(float strength = 1, float flow = 1, bool accumulate = false, AnimationCurve falloff = null)
            => new VertexBrushCommand(Vector3.zero, .3f, strength, flow, new Color32(), falloff ?? AnimationCurve.Constant(0, 1, 1),
                VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, accumulate, VertexBrushMask.None);

        private static void Seed(TexturePaintAsset paint, TexturePaintAtlas atlas, Func<Vector3, Color32> value)
        {
            for (var i = 0; i < paint.Pixels.Length; i++)
            {
                var color = atlas.Coverage[i] < 0 ? new Color32(13, 19, 29, 43) : value(atlas.Positions[i]);
                color.a = (byte)(37 + i % 17); if (paint.WeightChannels == 2) color.b = (byte)(71 + i % 11);
                paint.Pixels[i] = color;
            }
            paint.CommitPixels(); EditorUtility.SetDirty(paint); EditorUtility.SetDirty(paint.Mask); AssetDatabase.SaveAssetIfDirty(paint);
        }
    }
}
