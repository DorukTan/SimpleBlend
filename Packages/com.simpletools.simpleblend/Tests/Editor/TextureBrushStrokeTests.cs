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

        [TestCase(TextureBrushMaskMode.DarkPixels, false)]
        [TestCase(TextureBrushMaskMode.Alpha, false)]
        [TestCase(TextureBrushMaskMode.DarkPixels, true)]
        [TestCase(TextureBrushMaskMode.Alpha, true)]
        public void CustomTipGpuSnapshotReadsMaskWithoutChangingTheSavedTexture(TextureBrushMaskMode mode, bool invert)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.SetPixels(new[] { Color.black, Color.white, Color.clear, Color.black }); texture.Apply();
            var path = _assets.AssetPath + "/Tip.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture.isReadable, Is.False);
            var before = System.IO.File.ReadAllBytes(path);
            var metadata = System.IO.File.ReadAllBytes(path + ".meta");
            var shape = new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Texture = texture, MaskMode = mode, Hardness = 1, Invert = invert };
            var cache = new TextureBrushTipCache();
            Assert.That(cache.TryGet(shape, out var tip, out var error), Is.True, error);
            Assert.That(tip.Evaluate(new Vector2(-0.99f, -0.99f), Linear), Is.EqualTo(invert ? 0 : 1).Within(0.03f));
            Assert.That(tip.Evaluate(new Vector2(0.99f, -0.99f), Linear), Is.EqualTo((mode == TextureBrushMaskMode.Alpha) != invert ? 1 : 0).Within(0.03f));
            Assert.That(tip.Evaluate(new Vector2(-0.99f, 0.99f), Linear), Is.EqualTo(invert && mode == TextureBrushMaskMode.Alpha ? 1 : 0).Within(0.03f));
            shape.Invert = !invert;
            Assert.That(cache.TryGet(shape, out var reframed, out error), Is.True, error);
            Assert.That(reframed.Evaluate(new Vector2(-0.99f, 0.99f), Linear), Is.EqualTo(!invert && mode == TextureBrushMaskMode.Alpha ? 1 : 0).Within(0.03f));
            Assert.That(tip.Evaluate(new Vector2(-0.99f, -0.99f), Linear), Is.EqualTo(invert ? 0 : 1).Within(0.03f), "Changing cached settings must not mutate an earlier stroke snapshot.");
            Assert.That(texture.isReadable, Is.False);
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(System.IO.File.ReadAllBytes(path + ".meta"), Is.EqualTo(metadata));
        }

        [TestCase(TextureBrushMaskMode.DarkPixels, 0.15f)]
        [TestCase(TextureBrushMaskMode.Alpha, 0.4f)]
        public void InversionPrecedesFalloffAndRetainsTheBoundedFootprint(TextureBrushMaskMode mode, float expected)
        {
            var shape = new TextureBrushShape { Tip = TextureBrushTipKind.Custom, MaskMode = mode, Invert = true };
            var tip = new TextureBrushTip(shape, new[] { 0.2f }, 1, 1, new[] { 0.5f });
            Assert.That(tip.Evaluate(new Vector2(0.5f, 0), Linear), Is.EqualTo(expected).Within(0.0001f));
            Assert.That(tip.Evaluate(new Vector2(1.01f, 0), Linear), Is.Zero);
            Assert.That(tip.Evaluate(new Vector2(0, -1.01f), Linear), Is.Zero);
        }

        [Test]
        public void ReframedInversionOwnsItsMaskAndAlphaSnapshots()
        {
            var shape = new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Hardness = 1 };
            var mask = new[] { 0.1f, 0.2f }; var alpha = new[] { 0.4f, 0.8f };
            var original = new TextureBrushTip(shape, mask, 2, 1, alpha);
            shape.Invert = true; shape.Angle = 90;
            var inverted = original.Reframe(shape);
            mask[0] = 1; alpha[0] = 0; shape.Invert = false;
            Assert.That(original.Evaluate(new Vector2(-1, 0), Linear), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(inverted.Evaluate(new Vector2(0, -1), Linear), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(inverted.Evaluate(new Vector2(0.6f, 0), Linear), Is.Zero, "Rotation must retain the image aspect ratio.");
        }

        [TestCase(TextureBrushTipKind.Round)]
        [TestCase(TextureBrushTipKind.Flat)]
        [TestCase(TextureBrushTipKind.Chalk)]
        public void InversionDoesNotChangeBuiltInTipCoverage(TextureBrushTipKind kind)
        {
            var shape = new TextureBrushShape { Tip = kind };
            var original = new TextureBrushTip(shape); shape.Invert = true;
            var inverted = new TextureBrushTip(shape);
            foreach (var point in new[] { Vector2.zero, new Vector2(0.3f, -0.5f), Vector2.one })
                Assert.That(inverted.Evaluate(point, Linear), Is.EqualTo(original.Evaluate(point, Linear)));
        }

        [TestCase(0f, false)]
        [TestCase(90f, false)]
        [TestCase(0f, true)]
        [TestCase(90f, true)]
        public void InvertedCustomDabMatchesEvaluatorOnTransformedMeshAndUndo(float angle, bool erase)
        {
            var target = Target();
            target.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(20, 35, 15));
            target.transform.localScale = new Vector3(1.3f, 0.8f, 1.1f);
            var atlas = Atlas(target);
            foreach (var pixel in atlas.Pixels) target.Paint.Pixels[pixel] = new Color32((byte)(erase ? 255 : 0), 0, 0, 37);
            target.Paint.CommitPixels();
            var shape = new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Invert = true, Angle = angle, Roundness = 0.6f };
            var tip = new TextureBrushTip(shape, new[] { 0f, 1f, 0f, 1f }, 2, 2);
            var before = (Color32[])target.Paint.Pixels.Clone(); Undo.ClearAll();
            var command = Command(0.6f, 0.5f);
            var center = target.transform.TransformPoint(new Vector3(0, 0, -0.5f));
            var normal = target.transform.TransformDirection(Vector3.back);
            var preferred = target.transform.right;
            TextureBrushTip.Frame(normal, preferred, out var right, out var up);
            var stroke = new TexturePaintStroke(); stroke.Begin(target, atlas, 1, erase, command, tip);
            stroke.Dab(center, normal, preferredRight: preferred); Assert.That(stroke.End(), Is.GreaterThan(0));
            var tested = 0;
            foreach (var pixel in atlas.Pixels)
            {
                var delta = target.transform.TransformPoint(atlas.Positions[pixel]) - center;
                if (Mathf.Abs(Vector3.Dot(delta, normal)) > 0.0001f) continue;
                var point = new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, up)) / command.Radius;
                var expected = Mathf.RoundToInt(255 * tip.Evaluate(point, command.Falloff) * command.Strength * command.Flow);
                Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(erase ? 255 - expected : expected).Within(1));
                Assert.That(target.Paint.Pixels[pixel].a, Is.EqualTo(37)); tested++;
            }
            Assert.That(tested, Is.GreaterThan(20));
            var after = (Color32[])target.Paint.Pixels.Clone();
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
        }

        [Test]
        public void CustomPresetReloadRetainsInversionAndBuiltInsResetIt()
        {
            var texture = new Texture2D(1, 1);
            AssetDatabase.CreateAsset(texture, _assets.AssetPath + "/PresetTip.asset");
            var settings = SimpleBlendBrushSettings.instance;
            settings.TextureBrush = new TextureBrushShape { Tip = TextureBrushTipKind.Custom, Texture = texture, Invert = true, MaskMode = TextureBrushMaskMode.Alpha };
            var path = _assets.AssetPath + "/InvertedPreset.asset";
            var preset = SimpleBlendBrushPreset.SaveNew(path, settings);
            Resources.UnloadAsset(preset);
            preset = AssetDatabase.LoadAssetAtPath<SimpleBlendBrushPreset>(path);
            settings.TextureBrush.Invert = false; preset.Apply(settings);
            Assert.That(settings.TextureBrush.Invert, Is.True);
            Assert.That(settings.TextureBrush.MaskMode, Is.EqualTo(TextureBrushMaskMode.Alpha));
            Assert.That(settings.TextureBrush.Texture, Is.EqualTo(texture));
            settings.TextureBrush.Invert = false; preset.Apply(settings);
            Assert.That(settings.TextureBrush.Invert, Is.True, "Preset settings must be copied, not shared.");
            for (var i = 1; i <= 5; i++)
            {
                settings.TextureBrush.Invert = true; SimpleBlendBrushPreset.ApplyBuiltIn(i, settings);
                Assert.That(settings.TextureBrush.Invert, Is.False);
            }
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
                Assert.That(shape.Invert, Is.False);
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

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void LowFlowBuildUpConvergesWithoutByteRoundingStalls(bool erase, bool limitOpacity)
        {
            var target = Target(); TextureSurfaceAuthoring.Add(target.Paint); var atlas = Atlas(target);
            var seed = erase ? new Color32(255, 0, 0, 37) : new Color32(0, 255, 0, 37);
            foreach (var pixel in atlas.Pixels) target.Paint.Pixels[pixel] = seed;
            target.Paint.CommitPixels(); Undo.ClearAll();
            var before = (Color32[])target.Paint.Pixels.Clone();
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, SmallCommand(1, 0.01f, true),
                new TextureBrushTip(new TextureBrushShape { Hardness = 1, LimitOpacity = limitOpacity }));
            for (var i = 0; i < 600; i++) stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            Assert.That(stroke.End(), Is.GreaterThan(0));
            var pixelIndex = atlas.Pixels.First(pixel => target.Paint.Pixels[pixel].r != seed.r);
            var expected = Mathf.RoundToInt(255 * (1 - Mathf.Pow(0.99f, 600)));
            Assert.That(target.Paint.Pixels[pixelIndex].r, Is.EqualTo(erase ? 255 - expected : expected).Within(1));
            Assert.That(target.Paint.Pixels[pixelIndex].g, Is.EqualTo(erase ? 0 : 255 - expected).Within(1));
            Assert.That(target.Paint.Pixels[pixelIndex].a, Is.EqualTo(37));
            var after = (Color32[])target.Paint.Pixels.Clone();
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
            Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(after));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SubByteDabsEventuallyChangePixelsWithinOneStroke(bool erase)
        {
            var target = Target(); var atlas = Atlas(target);
            if (erase)
            {
                foreach (var pixel in atlas.Pixels) target.Paint.Pixels[pixel] = new Color32(255, 0, 0, 0);
                target.Paint.CommitPixels();
            }
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, SmallCommand(0.001f, 1, true),
                new TextureBrushTip(new TextureBrushShape { Hardness = 1 }));
            for (var i = 0; i < 100; i++) stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            Assert.That(stroke.End(), Is.GreaterThan(0));
            var expected = Mathf.RoundToInt(255 * (1 - Mathf.Pow(0.999f, 100)));
            Assert.That(atlas.Pixels.Any(pixel => Mathf.Abs(target.Paint.Pixels[pixel].r - (erase ? 255 - expected : expected)) <= 1), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedCappedDabsDoNotBuildUp(bool limitOpacity)
        {
            var target = Target(); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, SmallCommand(0.4f, 0.2f, false),
                new TextureBrushTip(new TextureBrushShape { Hardness = 1, LimitOpacity = limitOpacity }));
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            var first = (Color32[])target.Paint.Pixels.Clone();
            for (var i = 0; i < 20; i++) stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back, upload: false);
            stroke.End(); Assert.That(target.Paint.Pixels, Is.EqualTo(first));
        }

        [Test]
        public void NewSurfaceStrokeUsesItsOwnBaselineAndUndoRestoresEachStroke()
        {
            var target = Target(); TextureSurfaceAuthoring.Add(target.Paint); var atlas = Atlas(target);
            Undo.ClearAll(); var baseline = (Color32[])target.Paint.Pixels.Clone();
            var stroke = new TexturePaintStroke();
            var tip = new TextureBrushTip(new TextureBrushShape { Hardness = 1, LimitOpacity = true });
            stroke.Begin(target, atlas, 1, false, SmallCommand(0.5f, 1, true), tip);
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back); stroke.End();
            var first = (Color32[])target.Paint.Pixels.Clone();
            stroke.Begin(target, atlas, 2, false, SmallCommand(0.5f, 1, true), tip);
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back); stroke.End();
            var second = (Color32[])target.Paint.Pixels.Clone();
            var pixelIndex = atlas.Pixels.First(pixel => second[pixel].g > 0);
            Assert.That(second[pixelIndex].r, Is.EqualTo(64).Within(1));
            Assert.That(second[pixelIndex].g, Is.EqualTo(128).Within(1));
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(first));
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
            Undo.PerformRedo(); Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(second));
        }

        private static VertexBrushCommand SmallCommand(float opacity, float flow, bool accumulate)
            => new VertexBrushCommand(Vector3.zero, 0.04f, opacity, flow, new Color32(), AnimationCurve.Constant(0, 1, 1),
                VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, accumulate, VertexBrushMask.None);

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
