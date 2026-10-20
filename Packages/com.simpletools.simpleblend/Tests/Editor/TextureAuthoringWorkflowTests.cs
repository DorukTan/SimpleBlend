using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureAuthoringWorkflowTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private GameObject _source;
        private string _settings;
        private Object[] _selection;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope();
            _scene = EditorSceneManager.NewPreviewScene();
            _source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(_source, _scene);
            _source.name = "Cube";
            _settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            _selection = Selection.objects;
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop();
            EditorJsonUtility.FromJsonOverwrite(_settings, SimpleBlendBrushSettings.instance);
            Selection.objects = _selection;
            TexturePaintSaveQueue.Flush();
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets.Dispose();
        }

        private TexturePaintTarget Automatic() => TexturePaintSetup.CreateAutomatic(_source, resolution: 256, directory: _assets.AssetPath + "/Paints");

        [Test]
        public void AutomaticSetupNeedsNoPaletteAndCreatesUniqueAssetsWithoutOverwriting()
        {
            Assert.That(TexturePaintSetup.Problem(_source, null, 0, false), Is.Empty);
            var first = Automatic(); var second = Automatic();
            Assert.That(first.Paint.SurfaceCount, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(first.Paint), Is.Not.EqualTo(AssetDatabase.GetAssetPath(second.Paint)));
            Assert.That(AssetDatabase.GetAssetPath(first.Paint), Does.StartWith(_assets.AssetPath + "/Paints/"));
            Assert.That(TexturePaintStroke.Problem(first), Is.Empty);
            Assert.That(TexturePaintStroke.Problem(second), Is.Empty);
            Assert.That(_source.GetComponent<TexturePaintTarget>(), Is.Null);
        }

        [Test]
        public void AutomaticSetupCopiesCommonBaseInputsWithoutChangingSourceMaterial()
        {
            var sourceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(sourceMaterial, _assets.AssetPath + "/Source.mat");
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, _assets.AssetPath + "/Albedo.asset");
            sourceMaterial.SetTexture("_BaseMap", texture);
            sourceMaterial.SetColor("_BaseColor", Color.magenta);
            sourceMaterial.SetTextureScale("_BaseMap", new Vector2(3, 2));
            sourceMaterial.SetTextureOffset("_BaseMap", new Vector2(0.2f, 0.3f));
            _source.GetComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
            var before = EditorJsonUtility.ToJson(sourceMaterial);
            var target = Automatic();
            Assert.That(target.Paint.Material.GetTexture("_Layer0Albedo"), Is.EqualTo(texture));
            Assert.That(target.Paint.Material.GetColor("_Layer0Color"), Is.EqualTo(Color.magenta));
            Assert.That(target.Paint.Material.GetTextureScale("_Layer0Albedo"), Is.EqualTo(new Vector2(3, 2)));
            Assert.That(target.Paint.Material.GetTextureOffset("_Layer0Albedo"), Is.EqualTo(new Vector2(0.2f, 0.3f)));
            Assert.That(EditorJsonUtility.ToJson(sourceMaterial), Is.EqualTo(before));
        }

        [Test]
        public void AutomaticSetupHonorsOptionalPaletteAndKeepsAllItsSurfaceNames()
        {
            var palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Palette.asset", new[]
            {
                new SurfaceDefinition { Name = "Stone" }, new SurfaceDefinition { Name = "Moss" }, new SurfaceDefinition { Name = "Dirt" }
            });
            var target = TexturePaintSetup.CreateAutomatic(_source, resolution: 256, palette: palette, directory: _assets.AssetPath + "/Paints");
            Assert.That(target.Paint.SurfaceCount, Is.EqualTo(3));
            Assert.That(target.Paint.SurfaceName(2), Is.EqualTo("Dirt"));
            Assert.That(target.Paint.Material, Is.Not.EqualTo(palette.Material));
        }

        [Test]
        public void AutomaticSetupRejectsInvalidSourceBeforeCreatingDirectories()
        {
            _source.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => Automatic());
            Assert.That(AssetDatabase.IsValidFolder(_assets.AssetPath + "/Paints"), Is.False);
        }

        [Test]
        public void SurfaceSwitchActionChangeAndFillKeepTheToolActive()
        {
            var target = Automatic();
            Selection.activeGameObject = target.gameObject;
            SimpleBlendTexturePaintTool.Start(target, 1, false);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            SimpleBlendTexturePaintTool.Configure(target, 0, true);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            Assert.That(SimpleBlendTexturePaintTool.Surface, Is.Zero);
            Assert.That(SimpleBlendTexturePaintTool.Erase, Is.True);
            SimpleBlendTexturePaintTool.Fill(target, 1);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            SimpleBlendBrushPreset.ApplyBuiltIn(1, SimpleBlendBrushSettings.instance);
            SimpleBlendTexturePaintTool.FinishCurrentStroke();
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            Assert.That(ToolManager.activeToolType, Is.EqualTo(typeof(SimpleBlendTexturePaintTool)));
        }

        [UnityTest]
        public IEnumerator ProjectAssetSelectionKeepsBoundTargetButAnotherSceneObjectStopsTheTool()
        {
            var target = Automatic();
            Selection.activeGameObject = target.gameObject;
            SimpleBlendTexturePaintTool.Start(target, 1, false);
            Selection.activeObject = target.Paint;
            yield return null;
            Assert.That(SimpleBlendTexturePaintTool.KeepTargetForSelection(target, Selection.objects), Is.True);
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            Assert.That(SimpleBlendTexturePaintTool.KeepTargetForSelection(target, new Object[] { _source }), Is.False);
            Assert.That(SimpleBlendTexturePaintTool.KeepTargetForSelection(target, Array.Empty<Object>()), Is.False);
            Selection.activeGameObject = _source;
            yield return null;
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.False);
        }

        [Test]
        public void AddedSurfacesHaveUniqueNamesAndNewChannelsStartClear()
        {
            var target = Automatic(); var paint = target.Paint;
            var originalMesh = paint.Mesh; var originalMask = paint.Mask;
            for (var i = 0; i < paint.Pixels.Length; i++) paint.Pixels[i] = new Color32(63, 0, 91, 19);
            paint.CommitPixels();
            Assert.That(TextureSurfaceAuthoring.Add(paint), Is.EqualTo(2));
            Assert.That(TextureSurfaceAuthoring.Add(paint), Is.EqualTo(3));
            Assert.That(paint.SurfaceCount, Is.EqualTo(4));
            Assert.That(paint.Pixels.All(color => color.r == 63 && color.g == 0 && color.b == 0 && color.a == 19), Is.True);
            Assert.That(Enumerable.Range(0, 4).Select(paint.SurfaceName).Distinct().Count(), Is.EqualTo(4));
            Assert.That(paint.Material.shader.name, Is.EqualTo(SimpleBlendLayerBlendContract.LayerBlend4ShaderName));
            Assert.That(paint.Mesh, Is.EqualTo(originalMesh)); Assert.That(paint.Mask, Is.EqualTo(originalMask));
            Assert.Throws<InvalidOperationException>(() => TextureSurfaceAuthoring.Add(paint));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RemovingOverlayCompactsWeightsAndPreservesOtherSurfacesWithExactUndo(int surface)
        {
            var target = Automatic(); var paint = target.Paint;
            TextureSurfaceAuthoring.Add(paint); TextureSurfaceAuthoring.Add(paint);
            for (var i = 0; i < paint.Pixels.Length; i++) paint.Pixels[i] = new Color32(51, 77, 102, 23);
            paint.CommitPixels();
            var names = Enumerable.Range(0, 4).Select(paint.SurfaceName).ToArray();
            var edited = TextureSurfaceAuthoring.Read(paint, 3); edited.Tint = Color.cyan;
            TextureSurfaceAuthoring.Edit(paint, 3, edited);
            var before = (Color32[])paint.Pixels.Clone();
            Undo.ClearAll();
            TextureSurfaceAuthoring.Remove(paint, surface);
            var expected = surface == 1 ? new Color32(77, 102, 0, 23) : surface == 2 ? new Color32(51, 102, 0, 23) : new Color32(51, 77, 0, 23);
            Assert.That(paint.Pixels.All(pixel => pixel.Equals(expected)), Is.True);
            Assert.That(paint.SurfaceCount, Is.EqualTo(3));
            Assert.That(paint.Material.GetFloat("_LayerCount"), Is.EqualTo(3));
            if (surface != 3) Assert.That(paint.Material.GetColor("_Layer2Color"), Is.EqualTo(Color.cyan));
            var after = (Color32[])paint.Pixels.Clone();
            Undo.PerformUndo();
            Assert.That(paint.Pixels, Is.EqualTo(before));
            Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(before));
            Assert.That(Enumerable.Range(0, 4).Select(paint.SurfaceName), Is.EqualTo(names));
            Assert.That(paint.Material.shader.name, Is.EqualTo(SimpleBlendLayerBlendContract.LayerBlend4ShaderName));
            Undo.PerformRedo();
            Assert.That(paint.Pixels, Is.EqualTo(after));
            Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(after));
        }

        [Test]
        public void BaseCannotBeRemovedAndLastOverlayCanBeRemovedAndAddedAgain()
        {
            var target = Automatic();
            Assert.Throws<InvalidOperationException>(() => TextureSurfaceAuthoring.Remove(target.Paint, 0));
            TextureSurfaceAuthoring.Remove(target.Paint, 1);
            Assert.That(target.Paint.SurfaceCount, Is.EqualTo(1));
            Assert.That(TexturePaintStroke.Problem(target), Is.Empty);
            SimpleBlendTexturePaintTool.Fill(target, 0);
            TextureSurfaceAuthoring.Add(target.Paint);
            Assert.That(TexturePaintStroke.Problem(target), Is.Empty);
        }

        [Test]
        public void SurfaceEditingUpdatesOwnedTexturesNameAndTilingWithoutChangingPaint()
        {
            var target = Automatic(); var paint = target.Paint;
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, _assets.AssetPath + "/Texture.asset");
            var pixels = (Color32[])paint.Pixels.Clone();
            var settings = TextureSurfaceAuthoring.Read(paint, 1);
            settings.Name = "Grass"; settings.Tint = Color.blue;
            settings.Albedo = texture; settings.Normal = texture; settings.Mask = texture; settings.Height = texture;
            settings.Tiling = new Vector2(2, 3); settings.Offset = new Vector2(0.1f, 0.2f);
            Undo.ClearAll();
            TextureSurfaceAuthoring.Edit(paint, 1, settings);
            Assert.That(paint.SurfaceName(1), Is.EqualTo("Grass"));
            foreach (var suffix in new[] { "Albedo", "Normal", "Mask", "Height" }) Assert.That(paint.Material.GetTexture("_Layer1" + suffix), Is.EqualTo(texture));
            Assert.That(paint.Material.GetTextureScale("_Layer1Albedo"), Is.EqualTo(settings.Tiling));
            Assert.That(paint.Pixels, Is.EqualTo(pixels));
            Undo.PerformUndo();
            Assert.That(paint.SurfaceName(1), Is.EqualTo("Surface 1"));
            Assert.That(paint.Material.GetTexture("_Layer1Albedo"), Is.Null);
            Undo.PerformRedo();
            Assert.That(paint.Material.GetTexture("_Layer1Albedo"), Is.EqualTo(texture));
        }

        [TestCase("")]
        [TestCase("Base")]
        public void InvalidSurfaceNameIsRejectedWithoutWrites(string name)
        {
            var target = Automatic();
            var before = EditorJsonUtility.ToJson(target.Paint.Material);
            var surface = TextureSurfaceAuthoring.Read(target.Paint, 1); surface.Name = name;
            Assert.Throws<ArgumentException>(() => TextureSurfaceAuthoring.Edit(target.Paint, 1, surface));
            Assert.That(EditorJsonUtility.ToJson(target.Paint.Material), Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BuiltInPresetsPreserveActionChannelsAndMasksAndOwnTheirCurves(int index)
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.Mode = VertexPaintBrushMode.Erase; settings.Channel = VertexPaintChannel.Alpha;
            settings.NormalMaskEnabled = true; settings.SurfaceWeightChannels = 3;
            SimpleBlendBrushPreset.ApplyBuiltIn(index, settings);
            Assert.That(settings.Mode, Is.EqualTo(VertexPaintBrushMode.Erase));
            Assert.That(settings.Channel, Is.EqualTo(VertexPaintChannel.Alpha));
            Assert.That(settings.NormalMaskEnabled, Is.True);
            Assert.That(settings.SurfaceWeightChannels, Is.EqualTo(3));
            Assert.That(settings.Radius, Is.EqualTo(index == 3 ? 0.5f : 0.15f));
            var curve = settings.Falloff;
            SimpleBlendBrushPreset.ApplyBuiltIn(index, settings);
            Assert.That(settings.Falloff, Is.Not.SameAs(curve));
        }

        [Test]
        public void CustomPresetSurvivesReloadAndNeverSharesItsMutableCurve()
        {
            var settings = SimpleBlendBrushSettings.instance;
            settings.Radius = 0.42f; settings.Strength = 0.38f; settings.Flow = 0.61f; settings.Accumulate = false;
            settings.Falloff = AnimationCurve.Linear(0, 0.7f, 1, 0.1f);
            var path = _assets.AssetPath + "/Brush.asset";
            SimpleBlendBrushPreset.SaveNew(path, settings);
            Assert.Throws<ArgumentException>(() => SimpleBlendBrushPreset.SaveNew(path, settings));
            settings.Falloff.MoveKey(0, new Keyframe(0, 0));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var loaded = AssetDatabase.LoadAssetAtPath<SimpleBlendBrushPreset>(path);
            loaded.Apply(settings);
            Assert.That(settings.Radius, Is.EqualTo(0.42f));
            Assert.That(settings.Strength, Is.EqualTo(0.38f));
            Assert.That(settings.Flow, Is.EqualTo(0.61f));
            Assert.That(settings.Falloff.Evaluate(0), Is.EqualTo(0.7f));
            settings.Falloff.MoveKey(0, new Keyframe(0, 0));
            loaded.Apply(settings);
            Assert.That(settings.Falloff.Evaluate(0), Is.EqualTo(0.7f));
        }

        [Test]
        public void AutoSaveWritesOnlyQueuedPaintAssetAndPreservesUnrelatedDirtyMaterial()
        {
            var target = Automatic();
            var unrelated = new Material(target.Paint.Material);
            AssetDatabase.CreateAsset(unrelated, _assets.AssetPath + "/Unrelated.mat");
            AssetDatabase.SaveAssetIfDirty(unrelated);
            unrelated.SetColor("_Layer0Color", Color.red); EditorUtility.SetDirty(unrelated);
            var surface = TextureSurfaceAuthoring.Read(target.Paint, 1); surface.Name = "Saved surface";
            TextureSurfaceAuthoring.Edit(target.Paint, 1, surface);
            TexturePaintSaveQueue.Flush();
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(target.Paint), ImportAssetOptions.ForceSynchronousImport);
            Assert.That(target.Paint.SurfaceName(1), Is.EqualTo("Saved surface"));
        }
    }
}
