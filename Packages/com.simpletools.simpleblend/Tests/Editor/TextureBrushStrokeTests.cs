using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureBrushStrokeTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private string _settings;
        private static AnimationCurve Linear => AnimationCurve.Linear(0, 1, 1, 0);

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope();
            _scene = EditorSceneManager.NewPreviewScene();
            _settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop();
            EditorJsonUtility.FromJsonOverwrite(_settings, SimpleBlendBrushSettings.instance);
            TexturePaintSaveQueue.Flush(); Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets.Dispose();
        }

        [Test]
        public void RoundDefaultMatchesLegacyRadialFalloff()
        {
            var tip = new TextureBrushTip(new TextureBrushShape());
            foreach (var distance in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.1f })
                Assert.That(tip.Evaluate(new Vector2(distance, 0), Linear), Is.EqualTo(Mathf.Clamp01(1 - distance)).Within(0.0001f));
        }

        [TestCase(0f, 0.25f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        public void HardnessControlsTheSolidCenter(float hardness, float expected)
        {
            var tip = new TextureBrushTip(new TextureBrushShape { Hardness = hardness });
            Assert.That(tip.Evaluate(new Vector2(0.75f, 0), Linear), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void FlatRoundnessAndRotationAffectCoverageAndBounds()
        {
            var horizontal = new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Flat, Roundness = 0.2f, Hardness = 1 });
            var vertical = new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Flat, Roundness = 0.2f, Angle = 90, Hardness = 1 });
            Assert.That(horizontal.Evaluate(new Vector2(0.7f, 0), Linear), Is.EqualTo(1));
            Assert.That(horizontal.Evaluate(new Vector2(0, 0.7f), Linear), Is.Zero);
            Assert.That(vertical.Evaluate(new Vector2(0.7f, 0), Linear), Is.Zero);
            Assert.That(vertical.Evaluate(new Vector2(0, 0.7f), Linear), Is.EqualTo(1));
            for (var i = 0; i < 64; i++) Assert.That(vertical.Boundary(i * Mathf.PI / 32).magnitude, Is.LessThanOrEqualTo(vertical.BoundingRadius + 0.00001f));
        }

        [Test]
        public void ChalkIsDeterministicAndHasGrainInsteadOfUniformCoverage()
        {
            var tip = new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Chalk, Hardness = 1 });
            var values = new List<float>();
            for (var i = 0; i < 20; i++)
            {
                var point = new Vector2(i * 0.04f - 0.4f, 0.1f);
                var value = tip.Evaluate(point, Linear);
                Assert.That(tip.Evaluate(point, Linear), Is.EqualTo(value)); values.Add(value);
            }
            Assert.That(values.Distinct().Count(), Is.GreaterThan(5));
            Assert.That(values.Min(), Is.LessThan(0.1f));
        }

        [Test]
        public void TipSnapshotsDoNotChangeWhenSettingsAreEdited()
        {
            var shape = new TextureBrushShape { Hardness = 1 };
            var tip = new TextureBrushTip(shape);
            shape.Roundness = 0.1f; shape.Tip = TextureBrushTipKind.Flat;
            Assert.That(tip.Evaluate(new Vector2(0, 0.8f), Linear), Is.EqualTo(1));
        }

        [TestCase(TextureBrushMaskMode.DarkPixels)]
        [TestCase(TextureBrushMaskMode.Alpha)]
        public void CustomTipGpuSnapshotReadsMaskWithoutChangingTheSavedTexture(TextureBrushMaskMode mode)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.SetPixels(new[] { Color.black, Color.white, Color.clear, Color.black }); texture.Apply();
            var path = _assets.AssetPath + "/Tip.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture.isReadable, Is.False);
            var before = System.IO.File.ReadAllBytes(path);
            var tip = TextureBrushTip.Capture(new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Texture = texture, MaskMode = mode, Hardness = 1 });
            Assert.That(tip.Evaluate(new Vector2(-0.99f, -0.99f), Linear), Is.GreaterThan(0.97f));
            Assert.That(tip.Evaluate(new Vector2(0.99f, -0.99f), Linear), Is.EqualTo(mode == TextureBrushMaskMode.Alpha ? 1 : 0).Within(0.03f));
            Assert.That(tip.Evaluate(new Vector2(-0.99f, 0.99f), Linear), Is.LessThan(0.03f));
            Assert.That(texture.isReadable, Is.False);
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void CustomTipMissingAssetFailsClearlyAndCacheDoesNotReuseRoundAsCustom()
        {
            var cache = new TextureBrushTipCache();
            var shape = new TextureBrushShape();
            Assert.That(cache.TryGet(shape, out _, out _), Is.True);
            shape.Tip = TextureBrushTipKind.Custom;
            Assert.That(cache.TryGet(shape, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("saved brush-tip texture"));
        }

        [Test]
        public void CustomTipKeepsAspectRatioAndBilinearMaskValues()
        {
            var tip = new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Hardness = 1 }, new[] { 0f, 1f }, 2, 1);
            Assert.That(tip.Evaluate(Vector2.zero, Linear), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(tip.Evaluate(new Vector2(0, 0.75f), Linear), Is.Zero);
        }

        [TestCase(0.05f)]
        [TestCase(0.1f)]
        [TestCase(0.5f)]
        public void DiameterSpacingIsIndependentOfStraightLineEventSubdivisions(float spacing)
        {
            var coarse = Samples(spacing, 0, new[] { Vector3.zero, Vector3.right });
            var fine = Samples(spacing, 0, Enumerable.Range(0, 21).Select(i => Vector3.right * i / 20).ToArray());
            Assert.That(fine.Count, Is.EqualTo(coarse.Count));
            for (var i = 0; i < coarse.Count; i++) Assert.That(Vector3.Distance(coarse[i], fine[i]), Is.LessThan(0.0001f));
            Assert.That(coarse[1].x - coarse[0].x, Is.EqualTo(0.2f * spacing).Within(0.0001f));
        }

        [Test]
        public void SmoothingLagsThePointerAndCatchUpReachesTheRemainingRegularDabs()
        {
            var sampler = new TextureStrokeSampler(); var samples = new List<Vector3>();
            sampler.Append(Vector3.zero, 0.1f, 0.1f, 1, samples);
            sampler.Append(Vector3.right, 0.1f, 0.1f, 1, samples);
            Assert.That(samples.Last().x, Is.EqualTo(0.8f).Within(0.0001f));
            sampler.CatchUp(0.1f, 0.1f, samples);
            Assert.That(samples.Last().x, Is.EqualTo(1).Within(0.0001f));
            sampler.Reset(); sampler.CatchUp(0.1f, 0.1f, samples);
            Assert.That(samples, Is.Empty, "Reset on misses/focus changes must not paint a catch-up tail.");
        }

        [Test]
        public void ActualFlatDabUsesTipFrameRatherThanARadialDisc()
        {
            var target = Target(); var atlas = Atlas(target);
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Flat, Hardness = 1, Roundness = 0.2f }));
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back); stroke.End();
            var changed = 0;
            foreach (var pixel in atlas.Pixels)
            {
                if (target.Paint.Pixels[pixel].r == 0) continue;
                Assert.That(Mathf.Abs(atlas.Positions[pixel].y), Is.LessThanOrEqualTo(0.0401f));
                Assert.That(Mathf.Abs(atlas.Positions[pixel].x), Is.LessThanOrEqualTo(0.2001f)); changed++;
            }
            Assert.That(changed, Is.GreaterThan(20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PerStrokeOpacityCapsRepeatedFlowDabsAndUndoRestoresOneStroke(bool erase)
        {
            var target = Target(); var atlas = Atlas(target);
            if (erase)
            { var fill = new TexturePaintStroke(); fill.Begin(target, atlas, 1, false, Command()); fill.Dab(Vector3.zero, Vector3.up, true); fill.End(); }
            var before = (Color32[])target.Paint.Pixels.Clone(); Undo.ClearAll();
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, Command(0.4f, 0.2f), new TextureBrushTip(new TextureBrushShape { Hardness = 1, LimitOpacity = true }));
            for (var i = 0; i < 40; i++) stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            stroke.End();
            var changed = -1;
            for (var i = 0; i < before.Length; i++) if (target.Paint.Pixels[i].r != before[i].r) { changed = i; break; }
            Assert.That(changed, Is.GreaterThanOrEqualTo(0));
            var after = (Color32[])target.Paint.Pixels.Clone();
            var delta = Math.Abs(after[changed].r - before[changed].r);
            Assert.That(delta, Is.EqualTo(102).Within(1));
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
        }

        [Test]
        public void BatchedDabsUploadOnlyWhenRequestedAndEndFlushesPendingPixels()
        {
            var target = Target(); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            var before = target.Paint.Mask.GetPixels32();
            stroke.Begin(target, atlas, 1, false, Command(), new TextureBrushTip(new TextureBrushShape()));
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            Assert.That(target.Paint.Pixels, Is.Not.EqualTo(before)); Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(before));
            stroke.End(); Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(target.Paint.Pixels));
        }

        [Test]
        public void FillIgnoresTipShapeAndOpacityLimit()
        {
            var target = Target(); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(0.4f, 0.2f), new TextureBrushTip(new TextureBrushShape { Tip = TextureBrushTipKind.Custom, LimitOpacity = true }));
            stroke.Dab(Vector3.zero, Vector3.up, true); stroke.End();
            foreach (var pixel in atlas.Pixels) Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(20).Within(1));
        }

        [Test]
        public void PresetReloadRetainsTipAndStrokeSettingsWithoutAliasing()
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.TextureBrush = new TextureBrushShape { Tip = TextureBrushTipKind.Flat, Roundness = 0.3f, Angle = 47, Hardness = 0.8f, Spacing = 0.25f, Smoothing = 0.6f, LimitOpacity = true };
            var path = _assets.AssetPath + "/Preset.asset";
            SimpleBlendBrushPreset.SaveNew(path, settings);
            settings.TextureBrush.Angle = 0;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var preset = AssetDatabase.LoadAssetAtPath<SimpleBlendBrushPreset>(path); preset.Apply(settings);
            Assert.That(settings.TextureBrush.Tip, Is.EqualTo(TextureBrushTipKind.Flat));
            Assert.That(settings.TextureBrush.Angle, Is.EqualTo(47)); Assert.That(settings.TextureBrush.Roundness, Is.EqualTo(0.3f));
            Assert.That(settings.TextureBrush.Spacing, Is.EqualTo(0.25f)); Assert.That(settings.TextureBrush.Smoothing, Is.EqualTo(0.6f));
            Assert.That(settings.TextureBrush.LimitOpacity, Is.True);
            settings.TextureBrush.Angle = 0; preset.Apply(settings); Assert.That(settings.TextureBrush.Angle, Is.EqualTo(47));
        }

        [Test]
        public void LegacyPresetWithoutShapeFieldsUsesCompatibleRoundDefaults()
        {
            var preset = ScriptableObject.CreateInstance<SimpleBlendBrushPreset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite("{\"_radius\":0.2,\"_strength\":0.5,\"_flow\":1}", preset);
                preset.Apply(SimpleBlendBrushSettings.instance);
                var shape = SimpleBlendBrushSettings.instance.TextureBrush;
                Assert.That(shape.Tip, Is.EqualTo(TextureBrushTipKind.Round)); Assert.That(shape.Spacing, Is.EqualTo(0.1f));
                Assert.That(shape.Smoothing, Is.Zero); Assert.That(shape.Hardness, Is.Zero); Assert.That(shape.LimitOpacity, Is.False);
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void SavingCustomPresetWithoutSavedTipIsRejectedBeforeAssetCreation()
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.TextureBrush = new TextureBrushShape { Tip = TextureBrushTipKind.Custom };
            var path = _assets.AssetPath + "/Invalid.asset";
            Assert.Throws<ArgumentException>(() => SimpleBlendBrushPreset.SaveNew(path, settings));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null);
        }

        private TexturePaintTarget Target()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(source, _scene);
            return TexturePaintSetup.CreateAutomatic(source, resolution: 256, directory: _assets.AssetPath + "/Paints");
        }
        private static TexturePaintAtlas Atlas(TexturePaintTarget target) => new TexturePaintAtlas(target.Paint.Mesh, 0, 256);
        private static VertexBrushCommand Command(float opacity = 1, float flow = 1) => new VertexBrushCommand(Vector3.zero, 0.2f, opacity, flow,
            new Color32(), AnimationCurve.Constant(0, 1, 1), VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, true, VertexBrushMask.None);
        private static List<Vector3> Samples(float spacing, float smoothing, Vector3[] points)
        {
            var sampler = new TextureStrokeSampler(); var result = new List<Vector3>(); var samples = new List<Vector3>();
            foreach (var point in points) { sampler.Append(point, 0.1f, spacing, smoothing, samples); result.AddRange(samples); }
            return result;
        }
    }
}
