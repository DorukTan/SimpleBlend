using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TerrainMaterialBinderTests
    {
        private const string ShaderRoot = "Packages/com.simpletools.simpleblend/Tests/Editor/Shaders/";

        [SetUp]
        public void SetUp() => Undo.ClearAll();

        [TearDown]
        public void TearDown() => Undo.ClearAll();

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Binding_copies_public_inputs_and_preserves_unrelated_properties(int count)
        {
            using (var fixture = new Fixture(count))
            {
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message),
                    Is.True, message);
                Assert.That(fixture.Material.GetTexture("_TerrainControl"),
                    Is.SameAs(fixture.Data.GetAlphamapTexture(0)));
                Assert.That(fixture.Material.GetVector("_TerrainOrigin"), Is.EqualTo(new Vector4(14f, 2f, -9f, 0f)));
                Assert.That(fixture.Material.GetVector("_TerrainSize"), Is.EqualTo(new Vector4(80f, 20f, 60f, 0f)));
                Assert.That(fixture.Material.GetFloat("_TerrainLayerCount"), Is.EqualTo(count));
                Assert.That(fixture.Material.GetFloat("_OverlaySentinel"), Is.EqualTo(0.37f));
                for (var index = 0; index < count; index++)
                {
                    var layer = fixture.Layers[index];
                    var prefix = "_TerrainLayer" + index;
                    Assert.That(fixture.Material.GetTexture(prefix + "Diffuse"), Is.SameAs(layer.diffuseTexture));
                    Assert.That(fixture.Material.GetTexture(prefix + "Normal"), Is.SameAs(layer.normalMapTexture));
                    Assert.That(fixture.Material.GetTexture(prefix + "Mask"), Is.SameAs(layer.maskMapTexture));
                    Assert.That(fixture.Material.GetVector(prefix + "TileSizeOffset"),
                        Is.EqualTo(new Vector4(layer.tileSize.x, layer.tileSize.y, layer.tileOffset.x, layer.tileOffset.y)));
                    Assert.That(fixture.Material.GetVector(prefix + "Surface"),
                        Is.EqualTo(new Vector4(layer.normalScale, layer.metallic, layer.smoothness, 0f)));
                    Assert.That(fixture.Material.GetVector(prefix + "Flags"), Is.EqualTo(new Vector4(1f, 1f, 1f, 0f)));
                    Assert.That(fixture.Material.GetVector(prefix + "DiffuseRemapMin"), Is.EqualTo(layer.diffuseRemapMin));
                    Assert.That(fixture.Material.GetVector(prefix + "DiffuseRemapMax"), Is.EqualTo(layer.diffuseRemapMax));
                    Assert.That(fixture.Material.GetVector(prefix + "MaskRemapMin"), Is.EqualTo(layer.maskMapRemapMin));
                    Assert.That(fixture.Material.GetVector(prefix + "MaskRemapMax"), Is.EqualTo(layer.maskMapRemapMax));
                }
            }
        }

        [Test]
        public void Validate_reads_without_writing_or_marking_the_material_dirty()
        {
            using (var fixture = new Fixture(4))
            {
                var before = EditorJsonUtility.ToJson(fixture.Material);
                var dirty = EditorUtility.IsDirty(fixture.Material);
                Assert.That(TerrainMaterialBinder.Validate(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
                Assert.That(EditorUtility.IsDirty(fixture.Material), Is.EqualTo(dirty));
            }
        }

        [Test]
        public void Missing_optional_textures_use_explicit_neutral_inputs()
        {
            using (var fixture = new Fixture(1))
            {
                var layer = fixture.Layers[0];
                layer.diffuseTexture = null;
                layer.normalMapTexture = null;
                layer.maskMapTexture = null;
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                Assert.That(fixture.Material.GetTexture("_TerrainLayer0Diffuse"), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(fixture.Material.GetTexture("_TerrainLayer0Normal"), Is.SameAs(Texture2D.normalTexture));
                Assert.That(fixture.Material.GetTexture("_TerrainLayer0Mask"), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(fixture.Material.GetVector("_TerrainLayer0Flags"), Is.EqualTo(Vector4.zero));
                Assert.That(fixture.Material.GetVector("_TerrainLayer0Surface"),
                    Is.EqualTo(new Vector4(layer.normalScale, layer.metallic, layer.smoothness, 0f)));
            }
        }

        [Test]
        public void Rebinding_fewer_layers_resets_every_unused_slot()
        {
            using (var fixture = new Fixture(4))
            {
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.True);
                fixture.SetLayers(new[] { fixture.Layers[0] });
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                Assert.That(fixture.Material.GetFloat("_TerrainLayerCount"), Is.EqualTo(1f));
                for (var index = 1; index < 4; index++)
                {
                    var prefix = "_TerrainLayer" + index;
                    Assert.That(fixture.Material.GetTexture(prefix + "Diffuse"), Is.SameAs(Texture2D.whiteTexture));
                    Assert.That(fixture.Material.GetTexture(prefix + "Normal"), Is.SameAs(Texture2D.normalTexture));
                    Assert.That(fixture.Material.GetTexture(prefix + "Mask"), Is.SameAs(Texture2D.whiteTexture));
                    Assert.That(fixture.Material.GetVector(prefix + "Flags"), Is.EqualTo(Vector4.zero));
                    Assert.That(fixture.Material.GetVector(prefix + "TileSizeOffset"), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                    Assert.That(fixture.Material.GetVector(prefix + "Surface"), Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)));
                    Assert.That(fixture.Material.GetVector(prefix + "DiffuseRemapMin"), Is.EqualTo(Vector4.zero));
                    Assert.That(fixture.Material.GetVector(prefix + "DiffuseRemapMax"), Is.EqualTo(Vector4.one));
                    Assert.That(fixture.Material.GetVector(prefix + "MaskRemapMin"), Is.EqualTo(Vector4.zero));
                    Assert.That(fixture.Material.GetVector(prefix + "MaskRemapMax"), Is.EqualTo(Vector4.one));
                }
            }
        }

        [TestCase("missing-terrain", "Select a Terrain")]
        [TestCase("missing-data", "no TerrainData")]
        [TestCase("missing-material", "destination material")]
        [TestCase("no-layers", "at least one")]
        [TestCase("five-layers", "at most four")]
        [TestCase("missing-layer", "Layer 3 is missing")]
        [TestCase("zero-tile-x", "nonzero tile size")]
        [TestCase("zero-tile-y", "nonzero tile size")]
        [TestCase("nonfinite-remap", "non-finite")]
        [TestCase("rotation", "zero world rotation")]
        [TestCase("scale", "unit world scale")]
        public void Invalid_source_does_not_partially_change_the_material(string failure, string expected)
        {
            using (var fixture = new Fixture(failure == "five-layers" ? 5 : 4))
            {
                Terrain terrain = fixture.Terrain;
                Material material = fixture.Material;
                switch (failure)
                {
                    case "missing-terrain": terrain = null; break;
                    case "missing-data": terrain.terrainData = null; break;
                    case "missing-material": material = null; break;
                    case "no-layers": fixture.Data.terrainLayers = Array.Empty<TerrainLayer>(); break;
                    case "missing-layer":
                        var layers = (TerrainLayer[])fixture.Layers.Clone();
                        layers[3] = null;
                        fixture.Data.terrainLayers = layers;
                        break;
                    case "zero-tile-x": fixture.Layers[3].tileSize = new Vector2(0f, 3f); break;
                    case "zero-tile-y": fixture.Layers[3].tileSize = new Vector2(3f, 0f); break;
                    case "nonfinite-remap": fixture.Layers[3].maskMapRemapMin = new Vector4(float.NaN, 0f, 0f, 0f); break;
                    case "rotation": terrain.transform.rotation = Quaternion.Euler(0f, 10f, 0f); break;
                    case "scale": terrain.transform.localScale = new Vector3(2f, 1f, 1f); break;
                }
                var before = EditorJsonUtility.ToJson(fixture.Material);
                Assert.That(TerrainMaterialBinder.TryBind(terrain, material, out var message), Is.False);
                Assert.That(message, Does.Contain(expected));
                Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
            }
        }

        [TestCase("TerrainBindingWrongVersion.shader", "contract v1")]
        [TestCase("TerrainBindingWrongType.shader", "_TerrainOrigin must be Vector")]
        [TestCase("LayerBlend3.shader", "contract v1")]
        public void Incompatible_shader_is_rejected_without_material_changes(string asset, string expected)
        {
            using (var fixture = new Fixture(1))
            {
                var path = asset == "LayerBlend3.shader"
                    ? "Packages/com.simpletools.simpleblend/Shaders/" + asset : ShaderRoot + asset;
                fixture.Material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                var before = EditorJsonUtility.ToJson(fixture.Material);
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.False);
                Assert.That(message, Does.Contain(expected));
                Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
            }
        }

        [Test]
        public void Bind_is_one_complete_undo_redo_operation()
        {
            using (var fixture = new Fixture(4))
            {
                var before = EditorJsonUtility.ToJson(fixture.Material);
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                Undo.FlushUndoRecordObjects();
                var after = EditorJsonUtility.ToJson(fixture.Material);
                Undo.PerformUndo();
                Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
                Undo.PerformRedo();
                Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(after));
            }
        }

        [Test]
        public void Failed_binding_does_not_insert_an_undo_operation()
        {
            using (var fixture = new Fixture(5))
            {
                Undo.RegisterCompleteObjectUndo(fixture.Material, "Existing material edit");
                fixture.Material.SetFloat("_OverlaySentinel", 0.9f);
                Undo.FlushUndoRecordObjects();
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.False);
                Undo.PerformUndo();
                Assert.That(fixture.Material.GetFloat("_OverlaySentinel"), Is.EqualTo(0.37f));
            }
        }

        [Test]
        public void Alphamap_pixels_remain_live_but_parameter_changes_wait_for_rebind()
        {
            using (var fixture = new Fixture(2))
            {
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.True);
                var control = fixture.Material.GetTexture("_TerrainControl");
                var oldTiling = fixture.Material.GetVector("_TerrainLayer0TileSizeOffset");
                fixture.WriteWeights(2, 1);
                Assert.That(fixture.Data.GetAlphamapTexture(0), Is.SameAs(control));
                var weights = fixture.Data.GetAlphamaps(0, 0, 1, 1);
                Assert.That(weights[0, 0, 1], Is.EqualTo(1f).Within(0.01f));
                fixture.Layers[0].tileSize = new Vector2(25f, 30f);
                fixture.Layers[0].tileOffset = new Vector2(2f, -4f);
                fixture.Layers[0].smoothness = 0.9f;
                fixture.Terrain.transform.position = new Vector3(-5f, 3f, 10f);
                Assert.That(fixture.Material.GetVector("_TerrainLayer0TileSizeOffset"), Is.EqualTo(oldTiling));
                Assert.That(fixture.Material.GetVector("_TerrainOrigin"), Is.EqualTo(new Vector4(14f, 2f, -9f, 0f)));
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.True);
                Assert.That(fixture.Material.GetVector("_TerrainLayer0TileSizeOffset"), Is.EqualTo(new Vector4(25f, 30f, 2f, -4f)));
                Assert.That(fixture.Material.GetVector("_TerrainLayer0Surface").z, Is.EqualTo(0.9f));
                Assert.That(fixture.Material.GetVector("_TerrainOrigin"), Is.EqualTo(new Vector4(-5f, 3f, 10f, 0f)));
            }
        }

        [Test]
        public void Layer_replacement_waits_for_explicit_rebind()
        {
            using (var fixture = new Fixture(2))
            {
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.True);
                var oldTexture = fixture.Material.GetTexture("_TerrainLayer0Diffuse");
                fixture.SetLayers(new[] { fixture.Layers[1] });
                Assert.That(fixture.Material.GetTexture("_TerrainLayer0Diffuse"), Is.SameAs(oldTexture));
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out _), Is.True);
                Assert.That(fixture.Material.GetTexture("_TerrainLayer0Diffuse"), Is.SameAs(fixture.Layers[1].diffuseTexture));
                Assert.That(fixture.Material.GetFloat("_TerrainLayerCount"), Is.EqualTo(1f));
            }
        }

        [Test]
        public void Binding_preserves_source_asset_bytes_terrain_transform_mesh_and_paint_data()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(4))
            {
                var sourcePaths = fixture.PersistSources(assets.AssetPath);
                var mesh = TestMeshFactory.CreateQuad(includeColors: true);
                var meshPath = assets.AssetPath + "/SourceMesh.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                sourcePaths.Add(meshPath);
                var paintedObject = new GameObject("Painted mesh");
                try
                {
                    paintedObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = paintedObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = fixture.Material;
                    var paint = paintedObject.AddComponent<VertexPaintData>();
                    var initialization = VertexPaintDataAuthoring.Initialize(paint, renderer, false);
                    Assert.That(initialization.IsValid, Is.True, initialization.Message);
                    AssetDatabase.SaveAssets();
                    var sourceBytes = sourcePaths.ToDictionary(path => path, File.ReadAllBytes);
                    var terrainJson = EditorJsonUtility.ToJson(fixture.Terrain);
                    var transformJson = EditorJsonUtility.ToJson(fixture.Terrain.transform);
                    var paintJson = EditorJsonUtility.ToJson(paint);
                    Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                    AssetDatabase.SaveAssets();
                    foreach (var source in sourceBytes)
                        Assert.That(File.ReadAllBytes(source.Key), Is.EqualTo(source.Value), source.Key);
                    Assert.That(EditorJsonUtility.ToJson(fixture.Terrain), Is.EqualTo(terrainJson));
                    Assert.That(EditorJsonUtility.ToJson(fixture.Terrain.transform), Is.EqualTo(transformJson));
                    Assert.That(EditorJsonUtility.ToJson(paint), Is.EqualTo(paintJson));
                    Assert.That(paintedObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                }
                finally
                {
                    Object.DestroyImmediate(paintedObject);
                }
            }
        }

        [Test]
        public void Saved_material_binding_survives_reimport_and_undo()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(2))
            {
                fixture.PersistSources(assets.AssetPath);
                var materialPath = assets.AssetPath + "/Bound.mat";
                AssetDatabase.CreateAsset(fixture.Material, materialPath);
                AssetDatabase.SaveAssets();
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.True, message);
                Undo.FlushUndoRecordObjects();
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Assert.That(reloaded.GetFloat("_TerrainLayerCount"), Is.EqualTo(2f));
                Assert.That(reloaded.GetTexture("_TerrainControl"), Is.SameAs(fixture.Data.GetAlphamapTexture(0)));
                Assert.That(reloaded.GetTexture("_TerrainLayer1Diffuse"), Is.SameAs(fixture.Layers[1].diffuseTexture));
                Undo.PerformUndo();
                Assert.That(reloaded.GetFloat("_TerrainLayerCount"), Is.EqualTo(0f));
            }
        }

        [Test]
        public void Material_asset_rejects_unsaved_terrain_data()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(1))
            {
                AssetDatabase.CreateAsset(fixture.Material, assets.AssetPath + "/Bound.mat");
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.False);
                Assert.That(message, Does.Contain("Save the TerrainData"));
            }
        }

        [Test]
        public void Material_subasset_is_rejected()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(1))
            {
                fixture.PersistSources(assets.AssetPath);
                AssetDatabase.AddObjectToAsset(fixture.Material, fixture.Data);
                AssetDatabase.SaveAssets();
                Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.False);
                Assert.That(message, Does.Contain("standalone material"));
            }
        }

        [Test]
        public void Terrain_window_constructs_without_opening_a_desktop_window()
        {
            var window = ScriptableObject.CreateInstance<TerrainBindingWindow>();
            try { Assert.That(window, Is.Not.Null); }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void Material_asset_rejects_unsaved_layer_textures()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(1))
            {
                fixture.PersistSources(assets.AssetPath);
                AssetDatabase.CreateAsset(fixture.Material, assets.AssetPath + "/Bound.mat");
                var transient = new Texture2D(2, 2);
                try
                {
                    fixture.Layers[0].diffuseTexture = transient;
                    var before = EditorJsonUtility.ToJson(fixture.Material);
                    Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.False);
                    Assert.That(message, Does.Contain("unsaved texture"));
                    Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
                }
                finally { Object.DestroyImmediate(transient); }
            }
        }

        [Test]
        public void Read_only_destination_material_is_rejected()
        {
            using (var assets = new TestAssetScope())
            using (var fixture = new Fixture(1))
            {
                fixture.PersistSources(assets.AssetPath);
                var path = assets.AssetPath + "/Bound.mat";
                AssetDatabase.CreateAsset(fixture.Material, path);
                AssetDatabase.SaveAssets();
                var attributes = File.GetAttributes(path);
                try
                {
                    File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
                    var before = EditorJsonUtility.ToJson(fixture.Material);
                    Assert.That(TerrainMaterialBinder.TryBind(fixture.Terrain, fixture.Material, out var message), Is.False);
                    Assert.That(message, Does.Contain("editable standalone material"));
                    Assert.That(EditorJsonUtility.ToJson(fixture.Material), Is.EqualTo(before));
                }
                finally { File.SetAttributes(path, attributes); }
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly TerrainData Data;
            internal readonly TerrainLayer[] Layers;
            internal readonly Texture2D[] Textures;
            internal readonly GameObject TerrainObject;
            internal readonly Terrain Terrain;
            internal readonly Material Material;

            internal Fixture(int count)
            {
                Data = new TerrainData { heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(80f, 20f, 60f) };
                Layers = new TerrainLayer[count];
                Textures = new Texture2D[count * 3];
                for (var index = 0; index < count; index++)
                {
                    for (var map = 0; map < 3; map++)
                        Textures[index * 3 + map] = new Texture2D(2, 2) { name = "Layer " + index + " Map " + map };
                    Layers[index] = new TerrainLayer
                    {
                        diffuseTexture = Textures[index * 3], normalMapTexture = Textures[index * 3 + 1],
                        maskMapTexture = Textures[index * 3 + 2], tileSize = new Vector2(2f + index, 3f + index),
                        tileOffset = new Vector2(0.25f * index, -0.5f * index),
                        normalScale = 0.5f + index * 0.1f, metallic = index * 0.1f, smoothness = 0.25f + index * 0.1f,
                        diffuseRemapMin = new Vector4(0.05f, 0.1f, 0.15f, 0.2f),
                        diffuseRemapMax = new Vector4(0.8f, 0.9f, 1f, 0.7f),
                        maskMapRemapMin = new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
                        maskMapRemapMax = new Vector4(0.7f, 0.8f, 0.9f, 1f)
                    };
                }
                SetLayers(Layers);
                TerrainObject = Terrain.CreateTerrainGameObject(Data);
                Terrain = TerrainObject.GetComponent<Terrain>();
                Terrain.transform.position = new Vector3(14f, 2f, -9f);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderRoot + "TerrainBindingFixture.shader");
                Assert.That(shader, Is.Not.Null);
                Material = new Material(shader) { name = "Terrain destination" };
            }

            internal void SetLayers(TerrainLayer[] layers)
            {
                Data.terrainLayers = layers;
                WriteWeights(layers.Length, 0);
            }

            internal void WriteWeights(int count, int selected)
            {
                var weights = new float[16, 16, count];
                for (var y = 0; y < 16; y++)
                    for (var x = 0; x < 16; x++)
                        weights[y, x, selected] = 1f;
                Data.SetAlphamaps(0, 0, weights);
            }

            internal List<string> PersistSources(string root)
            {
                var paths = new List<string>();
                for (var i = 0; i < Textures.Length; i++)
                {
                    var path = root + "/Texture" + i + ".asset";
                    AssetDatabase.CreateAsset(Textures[i], path);
                    paths.Add(path);
                }
                for (var i = 0; i < Layers.Length; i++)
                {
                    var path = root + "/Layer" + i + ".asset";
                    AssetDatabase.CreateAsset(Layers[i], path);
                    paths.Add(path);
                }
                var dataPath = root + "/Data.asset";
                AssetDatabase.CreateAsset(Data, dataPath);
                paths.Add(dataPath);
                AssetDatabase.SaveAssets();
                return paths;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(TerrainObject);
                foreach (var layer in Layers) DestroyTransient(layer);
                foreach (var texture in Textures) DestroyTransient(texture);
                DestroyTransient(Data);
                DestroyTransient(Material);
            }

            private static void DestroyTransient(Object value)
            {
                if (value != null && !EditorUtility.IsPersistent(value))
                    Object.DestroyImmediate(value);
            }
        }
    }
}
