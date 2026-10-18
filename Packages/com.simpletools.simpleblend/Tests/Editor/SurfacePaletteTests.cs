using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class SurfacePaletteTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private GameObject _object;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private VertexPaintData _data;
        private string _originalBrush;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope();
            _scene = EditorSceneManager.NewPreviewScene();
            _mesh = TestMeshFactory.CreateQuad(includeColors: true);
            AssetDatabase.CreateAsset(_mesh, _assets.AssetPath + "/Source.asset");
            _object = new GameObject("Surface target");
            SceneManager.MoveGameObjectToScene(_object, _scene);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _renderer.sharedMaterials = new Material[2];
            var result = VertexPaintSetupUtility.Enable(_renderer, false, out _data);
            Assert.That(result.IsValid, Is.True, result.Message);
            _originalBrush = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_originalBrush != null) EditorJsonUtility.FromJsonOverwrite(_originalBrush, SimpleBlendBrushSettings.instance);
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets?.Dispose();
        }

        [TestCase(3)]
        [TestCase(4)]
        public void CreatedPalettePersistsNamesMaterialAndTextureReferences(int count)
        {
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, _assets.AssetPath + "/Albedo.asset");
            AssetDatabase.SaveAssetIfDirty(texture);
            var textureBytes = File.ReadAllBytes(_assets.AssetPath + "/Albedo.asset");
            var definitions = Definitions(count);
            definitions[1].Albedo = texture;
            var materialBefore = (Material[])_renderer.sharedMaterials.Clone();
            var paintBefore = EditorJsonUtility.ToJson(_data);
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Surfaces.asset", definitions);
            AssetDatabase.ImportAsset(_assets.AssetPath + "/Surfaces.asset", ImportAssetOptions.ForceSynchronousImport);
            palette = AssetDatabase.LoadAssetAtPath<SurfacePalette>(_assets.AssetPath + "/Surfaces.asset");
            Assert.That(palette.SurfaceCount, Is.EqualTo(count));
            Assert.That(palette.SurfaceName(1), Is.EqualTo("Surface 1"));
            Assert.That(palette.Problem, Is.Empty);
            Assert.That(AssetDatabase.GetAssetPath(palette.Material), Is.EqualTo(_assets.AssetPath + "/Surfaces.mat"));
            Assert.That(palette.Material.GetFloat("_LayerCount"), Is.EqualTo(count));
            Assert.That(AssetDatabase.GetAssetPath(palette.Material.GetTexture("_Layer1Albedo")), Is.EqualTo(_assets.AssetPath + "/Albedo.asset"));
            Assert.That(palette.Material.GetColor("_Layer1Color"), Is.EqualTo(definitions[1].Tint));
            Assert.That(File.ReadAllBytes(_assets.AssetPath + "/Albedo.asset"), Is.EqualTo(textureBytes));
            Assert.That(_renderer.sharedMaterials, Is.EqualTo(materialBefore));
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(paintBefore));
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(pipeline));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("../Surfaces.asset")]
        [TestCase("Packages/Surfaces.asset")]
        [TestCase("Assets/../Surfaces.asset")]
        [TestCase("Assets/MissingFolder/Surfaces.asset")]
        [TestCase("Assets/Surfaces.mat")]
        public void CancelledOrInvalidDestinationsCreateNothing(string path)
        {
            var before = Directory.GetFiles(_assets.AssetPath, "*", SearchOption.AllDirectories);
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(path, Definitions(3)));
            Assert.That(Directory.GetFiles(_assets.AssetPath, "*", SearchOption.AllDirectories), Is.EquivalentTo(before));
            Assert.That(_renderer.sharedMaterials, Is.All.Null);
        }

        [TestCase(2)]
        [TestCase(5)]
        public void UnsupportedSurfaceCountsAreRejected(int count)
        {
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Bad.asset", Definitions(count)));
            Assert.That(File.Exists(_assets.AssetPath + "/Bad.mat"), Is.False);
        }

        [Test]
        public void InvalidNamesAndTransientTexturesAreRejectedBeforeWriting()
        {
            var definitions = Definitions(3);
            definitions[1].Name = definitions[0].Name.ToUpperInvariant();
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Bad.asset", definitions));
            definitions[1].Name = "";
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Bad.asset", definitions));
            definitions[1].Name = "Overlay";
            definitions[1].Tint = new Color(float.NaN, 0, 0);
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Bad.asset", definitions));
            definitions[1].Tint = Color.white;
            var texture = new Texture2D(2, 2);
            try
            {
                definitions[1].Albedo = texture;
                Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Bad.asset", definitions));
                Assert.That(File.Exists(_assets.AssetPath + "/Bad.mat"), Is.False);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ExistingMaterialOrPaletteIsNeverOverwritten()
        {
            var palette = CreatePalette();
            var palettePath = AssetDatabase.GetAssetPath(palette);
            var materialPath = AssetDatabase.GetAssetPath(palette.Material);
            var paletteBytes = File.ReadAllBytes(palettePath);
            var materialBytes = File.ReadAllBytes(materialPath);
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(palettePath, Definitions(4)));
            Assert.That(File.ReadAllBytes(palettePath), Is.EqualTo(paletteBytes));
            Assert.That(File.ReadAllBytes(materialPath), Is.EqualTo(materialBytes));
            var occupiedMaterialPath = _assets.AssetPath + "/Occupied.mat";
            AssetDatabase.CopyAsset(materialPath, occupiedMaterialPath);
            var occupiedBytes = File.ReadAllBytes(occupiedMaterialPath);
            Assert.Throws<ArgumentException>(() => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Occupied.asset", Definitions(3)));
            Assert.That(File.Exists(_assets.AssetPath + "/Occupied.asset"), Is.False);
            Assert.That(File.ReadAllBytes(occupiedMaterialPath), Is.EqualTo(occupiedBytes));
        }

        [Test]
        public void AssignmentIsSlotSpecificUndoableAndPreservesPaintAndOldMaterial()
        {
            var palette = CreatePalette();
            var original = new Material(palette.Material) { name = "Original" };
            AssetDatabase.CreateAsset(original, _assets.AssetPath + "/Original.mat");
            AssetDatabase.SaveAssetIfDirty(original);
            var originalBytes = File.ReadAllBytes(_assets.AssetPath + "/Original.mat");
            _renderer.sharedMaterials = new[] { original, original };
            var paint = EditorJsonUtility.ToJson(_data);
            SurfacePaletteAuthoring.Assign(_renderer, 1, palette);
            Assert.That(_renderer.sharedMaterials, Is.EqualTo(new[] { original, palette.Material }));
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(paint));
            Assert.That(File.ReadAllBytes(_assets.AssetPath + "/Original.mat"), Is.EqualTo(originalBytes));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_renderer.sharedMaterials, Is.EqualTo(new[] { original, original }));
            Undo.PerformRedo();
            Assert.That(AssetDatabase.GetAssetPath(_renderer.sharedMaterials[1]), Is.EqualTo(AssetDatabase.GetAssetPath(palette.Material)));
        }

        [Test]
        public void AssignmentPreflightIsReadOnlyAndRejectsInvalidSlotsAndPalette()
        {
            var palette = CreatePalette();
            var rendererJson = EditorJsonUtility.ToJson(_renderer);
            Assert.That(SurfacePaletteAuthoring.AssignmentProblem(_renderer, 0, palette), Is.Empty);
            Assert.That(SurfacePaletteAuthoring.IsAssigned(_renderer, 0, palette), Is.False);
            Assert.That(EditorJsonUtility.ToJson(_renderer), Is.EqualTo(rendererJson));
            Assert.Throws<InvalidOperationException>(() => SurfacePaletteAuthoring.Assign(_renderer, -1, palette));
            Assert.Throws<InvalidOperationException>(() => SurfacePaletteAuthoring.Assign(_renderer, 2, palette));
            Assert.Throws<InvalidOperationException>(() => SurfacePaletteAuthoring.Assign(_renderer, 0, null));
            palette.Material.SetFloat("_LayerCount", 4);
            Assert.That(palette.Problem, Does.Contain("layer count"));
            Assert.Throws<InvalidOperationException>(() => SurfacePaletteAuthoring.Assign(_renderer, 0, palette));
            Assert.That(EditorJsonUtility.ToJson(_renderer), Is.EqualTo(rendererJson));
        }

        [Test]
        public void AssignmentOnPrefabInstanceIsAnOverrideNotAPrefabAssetEdit()
        {
            var palette = CreatePalette();
            var source = new GameObject("Palette prefab");
            SceneManager.MoveGameObjectToScene(source, _scene);
            source.AddComponent<MeshFilter>().sharedMesh = _mesh;
            source.AddComponent<MeshRenderer>().sharedMaterials = new Material[2];
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, _assets.AssetPath + "/Target.prefab");
            Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            var renderer = instance.GetComponent<MeshRenderer>();
            Assert.That(VertexPaintSetupUtility.Enable(renderer, false, out _).IsValid, Is.True);
            SurfacePaletteAuthoring.Assign(renderer, 1, palette);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterials[1]), Is.EqualTo(AssetDatabase.GetAssetPath(palette.Material)));
            Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterials, Is.All.Null);
            var hasMaterialOverride = false;
            foreach (var modification in PrefabUtility.GetPropertyModifications(instance))
                if (modification.propertyPath == "m_Materials.Array.data[1]") hasMaterialOverride = true;
            Assert.That(hasMaterialOverride, Is.True);
        }

        [TestCase(0, VertexPaintChannel.RGB)]
        [TestCase(1, VertexPaintChannel.Red)]
        [TestCase(2, VertexPaintChannel.Green)]
        [TestCase(3, VertexPaintChannel.Blue)]
        public void NamedChoicesOnlyConfigureTheSharedBrush(int surface, VertexPaintChannel channel)
        {
            var palette = CreatePalette(4);
            var settings = SimpleBlendBrushSettings.instance;
            settings.Strength = 0.37f;
            settings.Radius = 2.5f;
            settings.NormalMaskEnabled = true;
            var paint = EditorJsonUtility.ToJson(_data);
            var renderer = EditorJsonUtility.ToJson(_renderer);
            var material = EditorJsonUtility.ToJson(palette.Material);
            SurfacePaletteAuthoring.SelectSurface(palette, surface, 1, settings);
            Assert.That(settings.Channel, Is.EqualTo(channel));
            Assert.That(settings.Mode, Is.EqualTo(VertexPaintBrushMode.Paint));
            Assert.That(settings.SubmeshMaskEnabled, Is.True);
            Assert.That(settings.SubmeshMaskIndex, Is.EqualTo(1));
            Assert.That(settings.Strength, Is.EqualTo(0.37f));
            Assert.That(settings.Radius, Is.EqualTo(2.5f));
            Assert.That(settings.NormalMaskEnabled, Is.True);
            Assert.That(SurfacePaletteAuthoring.Matches(palette, surface, 1, settings), Is.True);
            Assert.That(EditorJsonUtility.ToJson(_data), Is.EqualTo(paint));
            Assert.That(EditorJsonUtility.ToJson(_renderer), Is.EqualTo(renderer));
            Assert.That(EditorJsonUtility.ToJson(palette.Material), Is.EqualTo(material));
            settings.Channel = VertexPaintChannel.Alpha;
            Assert.That(SurfacePaletteAuthoring.Matches(palette, surface, 1, settings), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RealStrokePreservesAlphaAndHonorsSectionMembership(int surface)
        {
            var palette = CreatePalette();
            var settings = SimpleBlendBrushSettings.instance;
            settings.Strength = 1;
            settings.Flow = 1;
            settings.Radius = 10;
            settings.Falloff = AnimationCurve.Constant(0, 1, 1);
            settings.NormalMaskEnabled = false;
            settings.ThresholdMaskEnabled = false;
            SurfacePaletteAuthoring.SelectSurface(palette, surface, 0, settings);
            var before = (Color32[])_data.SerializedColors.Clone();
            var cache = new MeshPaintCache();
            try
            {
                Assert.That(cache.Ensure(_data).IsValid, Is.True);
                var session = new VertexPaintStrokeSession();
                Assert.That(session.Begin(_data), Is.True);
                session.Paint(cache, settings.CreateCommand(Vector3.zero));
                session.End();
            }
            finally { cache.Dispose(); }
            for (var i = 0; i < 4; i++) Assert.That(_data.GetColor(i).a, Is.EqualTo(before[i].a));
            Assert.That(_data.GetColor(3), Is.EqualTo(before[3]), "Only section 1 references vertex 3.");
            Assert.That(_data.GetColor(0).r, Is.EqualTo(surface == 0 ? 0 : 255));
            if (surface == 0)
            {
                Assert.That(_data.GetColor(0).g, Is.Zero);
                Assert.That(_data.GetColor(0).b, Is.Zero);
            }
            else
            {
                Assert.That(_data.GetColor(0).g, Is.EqualTo(before[0].g));
                Assert.That(_data.GetColor(0).b, Is.EqualTo(before[0].b));
            }
            // Vertex 0 is shared by both sections: a section mask cannot split a shared vertex's paint.
            Assert.That(_mesh.colors32, Is.EqualTo(before), "The source asset must not receive stroke colors.");
        }

        [Test]
        public void ThreeSurfacePaletteRejectsBlueChoiceAndEmptyNamesAreActionable()
        {
            var palette = CreatePalette();
            Assert.Throws<ArgumentOutOfRangeException>(() => SurfacePaletteAuthoring.SelectSurface(palette, 3, 0, SimpleBlendBrushSettings.instance));
            var serialized = new SerializedObject(palette);
            serialized.FindProperty("_surfaceNames").GetArrayElementAtIndex(1).stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(palette.Problem, Does.Contain("distinct name"));
            Assert.That(SurfacePaletteAuthoring.AssignmentProblem(_renderer, 0, palette), Is.Not.Empty);
        }

        private SurfacePalette CreatePalette(int count = 3) => SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Surfaces.asset", Definitions(count));

        private static SurfaceDefinition[] Definitions(int count)
        {
            var result = new SurfaceDefinition[count];
            for (var i = 0; i < count; i++)
                result[i] = new SurfaceDefinition { Name = i == 0 ? "Base" : $"Surface {i}", Tint = new Color(0.2f + i * 0.1f, 0.4f, 0.6f) };
            return result;
        }
    }
}
