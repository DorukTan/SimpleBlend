using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class LayerBlendShaderTests
    {
        [TestCase(SimpleBlendLayerBlendContract.LayerBlend3ShaderName, 3)]
        [TestCase(SimpleBlendLayerBlendContract.LayerBlend4ShaderName, 4)]
        public void Public_layer_shader_has_expected_properties_and_passes(
            string shaderName,
            int expectedLayerCount)
        {
            var assetName = expectedLayerCount == 3 ? "LayerBlend3" : "LayerBlend4";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                $"Packages/com.simpletools.simpleblend/Shaders/{assetName}.shader");
            Assert.That(shader, Is.Not.Null, shaderName);
            AssertNoShaderErrors(shader);
            Assert.That(shader.name, Is.EqualTo(shaderName));

            var material = new Material(shader);
            try
            {
                Assert.That(material.GetFloat("_LayerCount"), Is.EqualTo(expectedLayerCount));
                Assert.That(material.HasProperty("_BlendTransition"), Is.True);
                Assert.That(material.GetFloat("_HeightBlendStrength"), Is.EqualTo(1), "Existing materials keep legacy height contrast until explicitly changed.");
                Assert.That(material.HasProperty("_Cull"), Is.True);
                for (var layerIndex = 0; layerIndex < 4; layerIndex++)
                {
                    AssertLayerProperties(material, layerIndex);
                }

                var shaderSource = File.ReadAllText(AssetDatabase.GetAssetPath(shader));
                foreach (var passName in SimpleBlendLayerBlendContract.RequiredPassNames)
                {
                    Assert.That(
                        shaderSource,
                        Does.Contain($"UsePass \"Hidden/SimpleBlend/Layer Blend Passes/{passName}\""),
                        passName);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Hidden_shared_pass_shader_compiles_without_errors()
        {
            var shader = Shader.Find("Hidden/SimpleBlend/Layer Blend Passes");

            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                foreach (var passName in SimpleBlendLayerBlendContract.RequiredPassNames)
                {
                    var passIndex = material.FindPass(passName);
                    Assert.That(
                        passIndex,
                        Is.GreaterThanOrEqualTo(0),
                        $"{passName}; available: {AvailablePassNames(material)}");
                    ShaderUtil.CompilePass(material, passIndex, false);
                }

                AssertNoShaderErrors(shader);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Channel_to_layer_contract_is_explicit_and_stable()
        {
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerLabel(0),
                Is.EqualTo("Base Layer — Unpainted"));
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerLabel(1),
                Does.Contain("Red Channel"));
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerLabel(2),
                Does.Contain("Green Channel"));
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerLabel(3),
                Does.Contain("Blue Channel"));
            Assert.That(SimpleBlendLayerBlendContract.MaskConvention, Does.Contain("R Metallic"));
            Assert.That(SimpleBlendLayerBlendContract.MaskConvention, Does.Contain("G Ambient Occlusion"));
            Assert.That(SimpleBlendLayerBlendContract.MaskConvention, Does.Contain("A Smoothness"));
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerCount(
                    SimpleBlendLayerBlendContract.LayerBlend3ShaderName),
                Is.EqualTo(3));
            Assert.That(
                SimpleBlendLayerBlendContract.GetLayerCount(
                    SimpleBlendLayerBlendContract.LayerBlend4ShaderName),
                Is.EqualTo(4));
        }

        [Test]
        public void Clear_vertex_color_selects_base_and_extreme_channels_select_overlays()
        {
            Assert.That(
                HeightBlendReference.RawWeights(Color.clear, 4),
                Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)));
            Assert.That(
                HeightBlendReference.RawWeights(Color.red, 4),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(
                HeightBlendReference.RawWeights(Color.green, 4),
                Is.EqualTo(new Vector4(0f, 0f, 1f, 0f)));
            Assert.That(
                HeightBlendReference.RawWeights(Color.blue, 4),
                Is.EqualTo(new Vector4(0f, 0f, 0f, 1f)));
            Assert.That(
                HeightBlendReference.RawWeights(Color.blue, 3),
                Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)),
                "Blue is intentionally reserved by Layer Blend 3.");
        }

        [Test]
        public void Height_breaks_weight_ties_without_allowing_zero_weight_layers_to_win()
        {
            var raw = new Vector4(0f, 0.5f, 0.5f, 0f);
            var result = HeightBlendReference.Calculate(
                raw,
                new Vector4(1f, 0.2f, 0.9f, 1f),
                0.1f,
                4);

            Assert.That(result.x, Is.Zero.Within(0.0001f));
            Assert.That(result.y, Is.Zero.Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.w, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Height_transition_is_normalized_and_controls_blend_width()
        {
            var raw = new Vector4(0f, 0.5f, 0.5f, 0f);
            var narrow = HeightBlendReference.Calculate(
                raw,
                new Vector4(0f, 0.45f, 0.55f, 0f),
                0.05f,
                3);
            var wide = HeightBlendReference.Calculate(
                raw,
                new Vector4(0f, 0.45f, 0.55f, 0f),
                0.5f,
                3);

            Assert.That(narrow.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(wide.y, Is.GreaterThan(0f));
            Assert.That(wide.z, Is.GreaterThan(wide.y));
            Assert.That(wide.x + wide.y + wide.z + wide.w, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Custom_material_inspector_constructs_without_editor_state()
        {
            Assert.That(new SimpleBlendLayerBlendShaderGUI(), Is.Not.Null);
        }

        [Test]
        public void Shared_passes_keep_one_material_buffer_and_required_batching_declarations()
        {
            const string inputPath =
                "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendLayerBlendInput.hlsl";
            const string passPath =
                "Packages/com.simpletools.simpleblend/Shaders/Hidden/SimpleBlendLayerBlendPasses.shader";
            var inputSource = File.ReadAllText(inputPath);
            var passSource = File.ReadAllText(passPath);

            Assert.That(
                CountOccurrences(inputSource, "CBUFFER_START(UnityPerMaterial)"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(inputSource, "CBUFFER_END"),
                Is.EqualTo(1));
            Assert.That(passSource, Does.Contain("#pragma multi_compile_instancing"));
            Assert.That(
                passSource,
                Does.Contain("#include_with_pragmas \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl\""));
        }

        private static void AssertLayerProperties(Material material, int layerIndex)
        {
            var prefix = $"_Layer{layerIndex}";
            Assert.That(material.HasProperty($"{prefix}Albedo"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Color"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Normal"), Is.True);
            Assert.That(material.HasProperty($"{prefix}NormalScale"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Mask"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Metallic"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Smoothness"), Is.True);
            Assert.That(material.HasProperty($"{prefix}OcclusionStrength"), Is.True);
            Assert.That(material.HasProperty($"{prefix}Height"), Is.True);
        }

        private static string AvailablePassNames(Material material)
        {
            return string.Join(
                ", ",
                Enumerable.Range(0, material.passCount).Select(material.GetPassName));
        }

        private static void AssertNoShaderErrors(Shader shader)
        {
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => $"{message.file}:{message.line} {message.message} {message.messageDetails}")
                .ToArray();
            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        }

        private static int CountOccurrences(string source, string value)
        {
            return source.Split(new[] { value }, StringSplitOptions.None).Length - 1;
        }

        internal static class HeightBlendReference
        {
            internal static Vector4 RawWeights(Color vertexColor, int layerCount)
            {
                var blue = layerCount == 4 ? Mathf.Clamp01(vertexColor.b) : 0f;
                return new Vector4(
                    Mathf.Clamp01(1f - vertexColor.r - vertexColor.g - blue),
                    Mathf.Clamp01(vertexColor.r),
                    Mathf.Clamp01(vertexColor.g),
                    blue);
            }

            internal static Vector4 Calculate(
                Vector4 rawWeights,
                Vector4 heights,
                float transition,
                int layerCount)
            {
                transition = Mathf.Max(transition, 0.0001f);
                if (layerCount == 3)
                {
                    rawWeights.w = 0f;
                    heights.w = 0f;
                }

                rawWeights = Max(rawWeights, 0f);
                heights = new Vector4(
                    Mathf.Clamp01(heights.x),
                    Mathf.Clamp01(heights.y),
                    Mathf.Clamp01(heights.z),
                    Mathf.Clamp01(heights.w));
                var scores = rawWeights + heights;
                var highest = Mathf.Max(
                    Mathf.Max(scores.x, scores.y),
                    Mathf.Max(scores.z, scores.w));
                var contributions = new Vector4(
                    Mathf.Max(scores.x - highest + transition, 0f) * rawWeights.x,
                    Mathf.Max(scores.y - highest + transition, 0f) * rawWeights.y,
                    Mathf.Max(scores.z - highest + transition, 0f) * rawWeights.z,
                    Mathf.Max(scores.w - highest + transition, 0f) * rawWeights.w);
                var total = contributions.x + contributions.y + contributions.z + contributions.w;
                if (total <= 0.0001f)
                {
                    total = rawWeights.x + rawWeights.y + rawWeights.z + rawWeights.w;
                    return total > 0.0001f
                        ? rawWeights / total
                        : new Vector4(1f, 0f, 0f, 0f);
                }

                return contributions / total;
            }

            private static Vector4 Max(Vector4 value, float minimum)
            {
                return new Vector4(
                    Mathf.Max(value.x, minimum),
                    Mathf.Max(value.y, minimum),
                    Mathf.Max(value.z, minimum),
                    Mathf.Max(value.w, minimum));
            }
        }
    }
}
