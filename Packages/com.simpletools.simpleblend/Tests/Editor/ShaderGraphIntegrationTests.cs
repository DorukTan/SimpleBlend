using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using HeightBlendReference = SimpleBlend.Editor.Tests.LayerBlendShaderTests.HeightBlendReference;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class ShaderGraphIntegrationTests
    {
        private const string Root = "Packages/com.simpletools.simpleblend/ShaderGraph/Examples/";

        [TestCase(3)]
        [TestCase(4)]
        public void Reference_graph_resolves_subgraphs_and_compiles_lit_passes(int layerCount)
        {
            var path = Root + "LayerBlend" + layerCount + "Parity.shadergraph";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importLog = AssetImporter.GetImportLog(path);
            var importErrors = (importLog?.logEntries ?? Array.Empty<ImportLog.ImportLogEntry>())
                .Where(entry => (entry.flags & ImportLogFlags.Error) != 0)
                .Select(entry => entry.message).ToArray();
            Assert.That(importErrors, Is.Empty, string.Join(Environment.NewLine, importErrors));
            var dependencies = AssetDatabase.GetDependencies(path);
            Assert.That(dependencies, Does.Contain(
                "Packages/com.simpletools.simpleblend/ShaderGraph/Subgraphs/SimpleBlend Height Weights "
                + layerCount + ".shadersubgraph"));
            Assert.That(dependencies, Does.Contain(
                "Packages/com.simpletools.simpleblend/ShaderGraph/Subgraphs/SimpleBlend Surface Blend 4.shadersubgraph"));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            // Batch mode has no render loop to select the URP-tagged subshader.
#pragma warning disable CS0618
            var previousPipeline = Shader.globalRenderPipeline;
            Shader.globalRenderPipeline = "UniversalPipeline";
            var material = new Material(shader);
            try
            {
                var initialMessages = string.Join(Environment.NewLine,
                    ShaderUtil.GetShaderMessages(shader).Select(message => message.message));
                Assert.That(material.passCount, Is.GreaterThanOrEqualTo(5),
                    shader.name + Environment.NewLine + initialMessages);
                for (var pass = 0; pass < material.passCount; pass++)
                    ShaderUtil.CompilePass(material, pass, false);
                var errors = ShaderUtil.GetShaderMessages(shader)
                    .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                    .Select(message => message.message).ToArray();
                Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                Shader.globalRenderPipeline = previousPipeline;
            }
#pragma warning restore CS0618
        }

        [TestCase(3)]
        [TestCase(4)]
        public void Reference_materials_use_identical_textures_and_surface_controls(int layerCount)
        {
            var graph = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Graph" + layerCount + ".mat");
            var hlsl = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Hlsl" + layerCount + ".mat");
            Assert.That(graph, Is.Not.Null);
            Assert.That(hlsl, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(graph.shader),
                Is.EqualTo(Root + "LayerBlend" + layerCount + "Parity.shadergraph"));
            Assert.That(graph.GetFloat("_BlendTransition"), Is.EqualTo(hlsl.GetFloat("_BlendTransition")));
            for (var layer = 0; layer < layerCount; layer++)
            {
                var prefix = "_Layer" + layer;
                foreach (var suffix in new[] { "Albedo", "Normal", "Mask", "Height" })
                {
                    Assert.That(graph.HasProperty(prefix + suffix), Is.True, prefix + suffix);
                    Assert.That(graph.GetTexture(prefix + suffix), Is.Not.Null, prefix + suffix);
                    Assert.That(graph.GetTexture(prefix + suffix),
                        Is.SameAs(hlsl.GetTexture(prefix + suffix)), prefix + suffix);
                }
                Assert.That(graph.GetColor(prefix + "Color"), Is.EqualTo(hlsl.GetColor(prefix + "Color")));
                foreach (var suffix in new[] { "NormalScale", "Metallic", "Smoothness", "OcclusionStrength" })
                    Assert.That(graph.GetFloat(prefix + suffix), Is.EqualTo(hlsl.GetFloat(prefix + suffix)));
                Assert.That(graph.GetTextureScale(prefix + "Albedo"),
                    Is.EqualTo(hlsl.GetTextureScale(prefix + "Albedo")));
                Assert.That(graph.GetTextureOffset(prefix + "Albedo"),
                    Is.EqualTo(hlsl.GetTextureOffset(prefix + "Albedo")));
            }
        }

        [TestCase(3, false)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        [TestCase(4, true)]
        public void Graph_height_entry_points_match_reference_vectors(int layerCount, bool useFloat)
        {
            RequireGraphics();
            var raw = new[]
            {
                Vector4.zero,
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(0f, 0.5f, 0.5f, 0f),
                new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
                new Vector4(-1f, 0.4f, 0.6f, 0f)
            };
            var heights = new[]
            {
                Vector4.zero,
                Vector4.one,
                new Vector4(0f, 0.45f, 0.55f, 0f),
                new Vector4(-1f, 0.2f, 2f, 0.5f)
            };
            var material = CreateProbe();
            try
            {
                material.SetFloat("_LayerCount", layerCount);
                material.SetFloat("_UseFloat", useFloat ? 1f : 0f);
                foreach (var weights in raw)
                foreach (var height in heights)
                foreach (var transition in new[] { 0f, 0.05f, 0.5f })
                {
                    material.SetVector("_RawWeights", weights);
                    material.SetVector("_Heights", height);
                    material.SetFloat("_Transition", transition);
                    var actual = RenderProbe(material);
                    var expected = HeightBlendReference.Calculate(weights, height, transition, layerCount);
                    AssertVector(actual, expected);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Surface_outputs_share_the_height_weights_and_normalize_normals()
        {
            RequireGraphics();
            var material = CreateProbe();
            var raw = new Vector4(0.1f, 0.3f, 0.4f, 0.2f);
            var heights = new Vector4(0.2f, 0.1f, 0.3f, 0.15f);
            var masks = new[]
            {
                new Vector4(0.1f, 0.7f, 0.4f, 0.2f), new Vector4(0.6f, 0.8f, 0.1f, 0.5f),
                new Vector4(0.3f, 0.5f, 0.2f, 0.8f), new Vector4(0.9f, 0.6f, 0.3f, 0.4f)
            };
            var normals = new[]
            {
                new Vector3(0f, 0f, 1f), new Vector3(0.3f, 0f, 0.95f).normalized,
                new Vector3(0f, 0.4f, 0.92f).normalized, new Vector3(-0.2f, 0.1f, 0.97f).normalized
            };
            try
            {
                material.SetVector("_RawWeights", raw);
                material.SetVector("_Heights", heights);
                material.SetFloat("_Transition", 0.5f);
                material.SetFloat("_LayerCount", 4f);
                material.SetFloat("_UseFloat", 1f);
                var weights = HeightBlendReference.Calculate(raw, heights, 0.5f, 4);
                var expectedMask = Vector4.zero;
                var expectedNormal = Vector3.zero;
                var expectedAlbedo = Vector3.zero;
                for (var i = 0; i < 4; i++)
                {
                    material.SetVector("_Mask" + i, masks[i]);
                    material.SetVector("_Normal" + i, normals[i]);
                    material.SetVector("_Albedo" + i, masks[i]);
                    expectedMask += masks[i] * weights[i];
                    expectedNormal += normals[i] * weights[i];
                    expectedAlbedo += new Vector3(masks[i].x, masks[i].y, masks[i].z) * weights[i];
                }
                material.SetFloat("_Output", 1f);
                AssertVector(RenderProbe(material), expectedMask);
                material.SetFloat("_Output", 2f);
                expectedNormal.Normalize();
                AssertVector(RenderProbe(material),
                    new Vector4(expectedNormal.x, expectedNormal.y, expectedNormal.z, 1f));
                material.SetFloat("_Output", 3f);
                AssertVector(RenderProbe(material),
                    new Vector4(expectedAlbedo.x, expectedAlbedo.y, expectedAlbedo.z, 1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [TestCase(3, 0f)]
        [TestCase(4, 0f)]
        [TestCase(3, 1f)]
        [TestCase(4, 1f)]
        [TestCase(4, 0.5f)]
        public void MaterialBlendModeGpuProbeDistinguishesSmoothWeightsFromHeightContrast(int layers, float contrast)
        {
            RequireGraphics();
            var material = CreateProbe();
            try
            {
                material.SetFloat("_UseMaterialBlend", 1);
                material.SetFloat("_LayerCount", layers);
                material.SetFloat("_HeightBlendStrength", contrast);
                material.SetFloat("_Transition", 0.2f);
                material.SetVector("_RawWeights", new Vector4(0.25f, 0.75f, 0, 0));
                material.SetVector("_Heights", Vector4.one * 0.5f);
                AssertVector(RenderProbe(material), Vector4.Lerp(new Vector4(0.25f, 0.75f, 0, 0),
                    new Vector4(0, 1, 0, 0), contrast));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("The parity probe requires a graphics device; run batch mode without -nographics.");
        }

        private static Material CreateProbe()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.simpletools.simpleblend/Tests/Editor/Shaders/ShaderGraphParityProbe.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            return new Material(shader);
        }

        private static Vector4 RenderProbe(Material material)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                readback.Apply();
                var color = readback.GetPixel(0, 0);
                return new Vector4(color.r, color.g, color.b, color.a);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static void AssertVector(Vector4 actual, Vector4 expected)
        {
            for (var channel = 0; channel < 4; channel++)
                Assert.That(actual[channel], Is.EqualTo(expected[channel]).Within(0.003f),
                    "Channel " + channel + "; expected " + expected + ", actual " + actual);
        }
    }
}
