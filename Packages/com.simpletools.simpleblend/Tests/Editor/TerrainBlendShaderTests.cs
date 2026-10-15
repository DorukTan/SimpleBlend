using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TerrainBlendShaderTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Material _probe;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Terrain GPU checks require a graphics device; run batch tests without -nographics.");
            _probe = Track(new Material(Shader.Find("Hidden/SimpleBlend/Terrain Surface Probe")));
            _probe.SetVector("_TerrainOrigin", new Vector4(14, 2, -9, 0));
            _probe.SetVector("_TerrainSize", new Vector4(80, 20, 60, 0));
            _probe.SetVector("_ProbePosition", new Vector4(54, 2, 21, 0));
            _probe.SetVector("_ProbeTangent", new Vector4(1, 0, 0, -1));
            _probe.SetFloat("_TerrainLayerCount", 4);
            _probe.SetTexture("_TerrainControl", Solid(new Color(1, 0, 0, 0)));
            for (var i = 0; i < 4; i++)
            {
                _probe.SetTexture("_TerrainLayer" + i + "Diffuse", Solid(Color.white));
                _probe.SetTexture("_TerrainLayer" + i + "Mask", Solid(Color.white));
                _probe.SetVector("_TerrainLayer" + i + "Flags", Vector4.zero);
            }
            for (var i = 1; i <= 3; i++)
            {
                _probe.SetTexture("_Layer" + i + "Albedo", Solid(Color.white));
                _probe.SetTexture("_Layer" + i + "Height", Solid(Color.black));
                _probe.SetTexture("_Layer" + i + "Mask", Solid(Color.white));
                _probe.SetFloat("_Layer" + i + "Metallic", 0);
                _probe.SetFloat("_Layer" + i + "Smoothness", 0.5f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects)
                Object.DestroyImmediate(item);
            _objects.Clear();
        }

        [Test]
        public void World_coordinates_include_translated_origin_non_square_size_and_layer_offsets()
        {
            _probe.SetVector("_TerrainLayer0TileSizeOffset", new Vector4(8, 12, 2, -6));
            AssertPixel(Read(0), new Color(0.5f, 0.5f, 5.25f, 2));
        }

        [Test]
        public void Layer_coordinates_preserve_negative_tile_sizes()
        {
            _probe.SetVector("_TerrainLayer0TileSizeOffset", new Vector4(-8, -12, 2, -6));
            AssertPixel(Read(0), new Color(0.5f, 0.5f, -5.25f, -2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Each_control_channel_selects_its_corresponding_terrain_layer(int selected)
        {
            var control = Vector4.zero;
            control[selected] = 1;
            _probe.SetTexture("_TerrainControl", Solid(control));
            for (var i = 0; i < 4; i++)
                SetDiffuse(i, new Color(i * 0.2f, 0.1f, 0.8f - i * 0.1f, 1));
            AssertPixel(Read(1), new Color(selected * 0.2f, 0.1f, 0.8f - selected * 0.1f, 1));
        }

        [Test]
        public void Control_weights_normalize_and_ignore_unused_channels()
        {
            _probe.SetFloat("_TerrainLayerCount", 2);
            _probe.SetTexture("_TerrainControl", Solid(new Color(0.1f, 0.3f, 1, 1)));
            SetDiffuse(0, Color.red);
            SetDiffuse(1, Color.blue);
            SetDiffuse(2, Color.green);
            AssertPixel(Read(1), new Color(0.25f, 0, 0.75f, 1));
        }

        [Test]
        public void Empty_control_weights_fall_back_to_first_layer()
        {
            _probe.SetTexture("_TerrainControl", Solid(Color.clear));
            SetDiffuse(0, Color.red);
            AssertPixel(Read(1), Color.red);
        }

        [TestCase(14f, -9f, 1f, 0f)]
        [TestCase(94f, 51f, 0f, 1f)]
        [TestCase(-66f, -69f, 1f, 0f)]
        [TestCase(174f, 111f, 0f, 1f)]
        public void Control_sampling_aligns_texel_centers_and_clamps_at_terrain_edges(
            float x, float z, float expectedRed, float expectedBlue)
        {
            var texture = Track(new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true));
            texture.SetPixels(new[] { Color.red, Color.blue, Color.red, Color.blue });
            texture.Apply();
            _probe.SetTexture("_TerrainControl", texture);
            _probe.SetVector("_ProbePosition", new Vector4(x, 2, z, 0));
            _probe.SetFloat("_TerrainLayerCount", 3);
            SetDiffuse(0, Color.red);
            SetDiffuse(2, Color.blue);
            AssertPixel(Read(1), new Color(expectedRed, 0, expectedBlue, 1));
        }

        [Test]
        public void Mask_remaps_supply_surface_values_without_overlay_multipliers()
        {
            _probe.SetTexture("_TerrainLayer0Mask", Solid(new Color(0.2f, 0.4f, 0.6f, 0.8f)));
            _probe.SetVector("_TerrainLayer0Flags", new Vector4(0, 0, 1, 0));
            _probe.SetVector("_TerrainLayer0MaskRemapMin", new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
            _probe.SetVector("_TerrainLayer0MaskRemapMax", new Vector4(0.6f, 0.7f, 0.8f, 0.9f));
            _probe.SetVector("_TerrainLayer0Surface", new Vector4(1, 0, 0, 0));
            AssertPixel(Read(2), new Color(0.2f, 0.4f, 0.8f, 0.6f));
        }

        [Test]
        public void Missing_mask_uses_constants_and_public_ao_height_maxima()
        {
            _probe.SetVector("_TerrainLayer0Surface", new Vector4(1, 0.3f, 0.7f, 0));
            _probe.SetVector("_TerrainLayer0MaskRemapMax", new Vector4(1, 0.6f, 0.2f, 1));
            AssertPixel(Read(2), new Color(0.3f, 0.6f, 0.7f, 0.2f));
        }

        [Test]
        public void Diffuse_alpha_smoothness_is_explicit_and_mask_has_priority()
        {
            SetDiffuse(0, new Color(1, 1, 1, 0.4f));
            _probe.SetVector("_TerrainLayer0Surface", new Vector4(1, 0.3f, 0.7f, 1));
            AssertPixel(Read(2), new Color(0.3f, 1, 0.4f, 1));
            _probe.SetVector("_TerrainLayer0Flags", new Vector4(1, 0, 1, 0));
            _probe.SetTexture("_TerrainLayer0Mask", Solid(new Color(0.1f, 0.2f, 0.3f, 0.9f)));
            AssertPixel(Read(2), new Color(0.1f, 0.2f, 0.9f, 0.3f));
        }

        [Test]
        public void Diffuse_tint_uses_public_remap_scale()
        {
            SetDiffuse(0, new Color(0.8f, 0.6f, 0.4f, 1));
            _probe.SetVector("_TerrainLayer0DiffuseRemapMin", new Vector4(0.1f, 0.2f, 0.3f, 0));
            _probe.SetVector("_TerrainLayer0DiffuseRemapMax", new Vector4(0.6f, 0.7f, 0.8f, 1));
            AssertPixel(Read(1), new Color(0.4f, 0.3f, 0.2f, 1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Clear_and_extreme_vertex_channels_select_terrain_or_overlay(int channel)
        {
            SetDiffuse(0, new Color(0.2f, 0.3f, 0.4f, 1));
            _probe.SetVector("_Layer1Color", Color.red);
            _probe.SetVector("_Layer2Color", Color.green);
            _probe.SetVector("_Layer3Color", Color.blue);
            var paint = Vector4.zero;
            if (channel > 0) paint[channel - 1] = 1;
            _probe.SetVector("_ProbeColor", paint);
            AssertPixel(Read(4), channel == 0 ? new Color(0.2f, 0.3f, 0.4f, 1)
                : channel == 1 ? Color.red : channel == 2 ? Color.green : Color.blue);
        }

        [Test]
        public void Zero_painted_high_overlays_cannot_replace_the_terrain_and_alpha_is_unused()
        {
            _probe.SetFloat("_TerrainHeightScale", 1);
            SetDiffuse(0, new Color(0.2f, 0.3f, 0.4f, 1));
            for (var i = 1; i <= 3; i++)
                _probe.SetTexture("_Layer" + i + "Height", Solid(Color.white));
            _probe.SetVector("_ProbeColor", new Vector4(0, 0, 0, 1));
            AssertPixel(Read(4), new Color(0.2f, 0.3f, 0.4f, 1));
            _probe.SetVector("_ProbeColor", Vector4.zero);
            AssertPixel(Read(4), new Color(0.2f, 0.3f, 0.4f, 1));
        }

        [Test]
        public void Default_terrain_height_influence_does_not_suppress_ordinary_overlay_paint()
        {
            Assert.That(_probe.GetFloat("_TerrainHeightScale"), Is.Zero);
            SetDiffuse(0, new Color(0.2f, 0.3f, 0.4f, 1));
            _probe.SetColor("_Layer1Color", Color.red);
            _probe.SetVector("_ProbeColor", new Vector4(0.5f, 0, 0, 0));
            AssertPixel(Read(4), new Color(0.6f, 0.15f, 0.2f, 1));
        }

        [Test]
        public void Projected_normals_follow_world_axes_independently_of_mesh_tangent_rotation()
        {
            var normal = new Vector3(0.3f, 0.4f, Mathf.Sqrt(0.75f));
            _probe.SetVector("_ProbeNormal", normal);
            AssertPixel(Read(3), new Color(normal.x, normal.y, normal.z, 1));
            _probe.SetVector("_ProbeTangent", new Vector4(0, 0, 1, 1));
            AssertPixel(Read(3), new Color(normal.y, normal.x, normal.z, 1));
        }

        [Test]
        public void Terrain_normal_maps_use_world_projection_in_the_actual_surface_path()
        {
            _probe.SetTexture("_TerrainLayer0Normal", Solid(new Color(1, 0.7f, 1, 0.65f)));
            _probe.SetVector("_TerrainLayer0Flags", new Vector4(0, 1, 0, 0));
            AssertPixel(Read(6), new Color(0.3f, 0.4f, Mathf.Sqrt(0.75f), 1));
            _probe.SetVector("_ProbeTangent", new Vector4(0, 0, 1, 1));
            AssertPixel(Read(6), new Color(0.4f, 0.3f, Mathf.Sqrt(0.75f), 1));
            _probe.SetVector("_TerrainLayer0Flags", Vector4.zero);
            AssertPixel(Read(6), new Color(0, 0, 1, 1));
        }

        [Test]
        public void Height_breaks_active_overlay_ties_without_activating_unpainted_layers()
        {
            _probe.SetVector("_ProbeColor", new Vector4(0.5f, 0.5f, 0, 0));
            _probe.SetTexture("_Layer1Height", Solid(new Color(0.2f, 0, 0, 1)));
            _probe.SetTexture("_Layer2Height", Solid(new Color(0.9f, 0, 0, 1)));
            _probe.SetTexture("_Layer3Height", Solid(Color.white));
            _probe.SetColor("_Layer1Color", Color.red);
            _probe.SetColor("_Layer2Color", Color.green);
            _probe.SetFloat("_BlendTransition", 0.1f);
            AssertPixel(Read(4), Color.green);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Each_terrain_layer_contributes_its_mask_channels(int selected)
        {
            var control = Vector4.zero;
            control[selected] = 1;
            _probe.SetTexture("_TerrainControl", Solid(control));
            for (var i = 0; i < 4; i++)
            {
                _probe.SetVector("_TerrainLayer" + i + "Flags", new Vector4(0, 0, 1, 0));
                _probe.SetTexture("_TerrainLayer" + i + "Mask",
                    Solid(new Color(0.1f * i, 0.2f, 0.3f, 0.4f + 0.1f * i)));
            }
            AssertPixel(Read(2), new Color(0.1f * selected, 0.2f, 0.4f + 0.1f * selected, 0.3f));
        }

        [Test]
        public void Reference_fixture_preserves_active_scene_and_validates_untitled_setup()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            using (var scope = new TestAssetScope())
            {
                var hasUntitled = false;
                for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                    hasUntitled |= string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).path);
                if (hasUntitled)
                {
                    Assert.That(Assert.Throws<InvalidOperationException>(() =>
                        TerrainBlendReferenceFixture.Create(scope.AssetPath)).Message, Does.Contain("untitled scenes"));
                    Assert.That(AssetDatabase.FindAssets(string.Empty, new[] { scope.AssetPath }), Is.Empty);
                }
                else
                {
                    var path = TerrainBlendReferenceFixture.Create(scope.AssetPath);
                    Assert.That(System.IO.File.Exists(path), Is.True);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(scope.AssetPath + "/TerrainBlend.mat");
                    var data = AssetDatabase.LoadAssetAtPath<TerrainData>(scope.AssetPath + "/TerrainData.asset");
                    Assert.That(material.GetTexture("_TerrainControl"), Is.SameAs(data.GetAlphamapTexture(0)));
                }
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(active));
            }
        }

        [Test]
        public void Public_shader_accepts_binding_contract_and_compiles_all_required_passes()
        {
            var shader = Shader.Find("SimpleBlend/Terrain Blend");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.GetPropertyDefaultFloatValue(shader.FindPropertyIndex(
                TerrainBindingProperties.Version)), Is.EqualTo(1));
            var material = Track(new Material(shader));
            var terrainData = Track(new TerrainData { heightmapResolution = 33, alphamapResolution = 16 });
            terrainData.terrainLayers = new[] { Track(new TerrainLayer()) };
            var terrainObject = Track(Terrain.CreateTerrainGameObject(terrainData));
            Assert.That(TerrainMaterialBinder.TryBind(terrainObject.GetComponent<Terrain>(), material,
                out var message), Is.True, message);
#pragma warning disable CS0618
            var previousPipeline = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                foreach (var pass in SimpleBlendLayerBlendContract.RequiredPassNames)
                {
                    var index = material.FindPass(pass);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0), pass);
                    ShaderUtil.CompilePass(material, index, true);
                }
                AssertNoErrors(shader);
            }
            finally { Shader.globalRenderPipeline = previousPipeline; }
#pragma warning restore CS0618
            Assert.That(new SimpleBlendTerrainShaderGUI(), Is.Not.Null);
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }

        private Texture2D Solid(Color color)
        {
            var texture = Track(new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true));
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private void SetDiffuse(int index, Color color)
        {
            _probe.SetTexture("_TerrainLayer" + index + "Diffuse", Solid(color));
            _probe.SetVector("_TerrainLayer" + index + "Flags", new Vector4(1, 0, 0, 0));
        }

        private Color Read(int mode)
        {
            _probe.SetFloat("_ProbeMode", mode);
            var target = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            var oldActive = RenderTexture.active;
            var commands = new CommandBuffer();
            var result = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.magenta);
                commands.DrawProcedural(Matrix4x4.identity, _probe, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                result.Apply();
                AssertNoErrors(_probe.shader);
                return result.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = oldActive;
                commands.Release();
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(result);
            }
        }

        private static void AssertPixel(Color actual, Color expected)
        {
            for (var index = 0; index < 4; index++)
                Assert.That(actual[index], Is.EqualTo(expected[index]).Within(0.003f),
                    "Component " + index + "; actual " + actual + ", expected " + expected);
        }

        private static void AssertNoErrors(Shader shader)
        {
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(m => m.severity == ShaderCompilerMessageSeverity.Error)
                .Select(m => m.message + " " + m.messageDetails).ToArray();
            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        }
    }
}
