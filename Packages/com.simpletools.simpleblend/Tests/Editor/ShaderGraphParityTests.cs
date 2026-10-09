using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class ShaderGraphParityTests
    {
        private const string HeightIncludePath =
            "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendHeightBlend.hlsl";
        private const string SurfaceIncludePath =
            "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendSurfaceBlend.hlsl";

        [TestCase(
            "Packages/com.simpletools.simpleblend/ShaderGraph/Subgraphs/SimpleBlend Height Weights 3.shadersubgraph",
            "SimpleBlendHeightWeights3",
            HeightIncludePath,
            "aea465f48fa239a44b895c30c699841c")]
        [TestCase(
            "Packages/com.simpletools.simpleblend/ShaderGraph/Subgraphs/SimpleBlend Height Weights 4.shadersubgraph",
            "SimpleBlendHeightWeights4",
            HeightIncludePath,
            "aea465f48fa239a44b895c30c699841c")]
        [TestCase(
            "Packages/com.simpletools.simpleblend/ShaderGraph/Subgraphs/SimpleBlend Surface Blend 4.shadersubgraph",
            "SimpleBlendSurfaceBlend4",
            SurfaceIncludePath,
            "b7e3c83d8f5a48b1a6daef9b1885f83b")]
        public void Subgraph_imports_and_uses_shared_hlsl(
            string assetPath,
            string functionName,
            string includePath,
            string includeGuid)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importErrors = (AssetImporter.GetImportLog(assetPath)?.logEntries
                ?? Array.Empty<ImportLog.ImportLogEntry>())
                .Where(entry => (entry.flags & ImportLogFlags.Error) != 0)
                .Select(entry => entry.message).ToArray();
            Assert.That(importErrors, Is.Empty, string.Join(Environment.NewLine, importErrors));
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            Assert.That(asset.GetType().Name, Is.EqualTo("SubGraphAsset"));

            var source = File.ReadAllText(assetPath);
            AssertSlotReferences(source);
            Assert.That(source, Does.Contain($"\"m_FunctionName\": \"{functionName}\""));
            Assert.That(source, Does.Contain(includeGuid));
            Assert.That(
                AssetDatabase.GetDependencies(assetPath),
                Does.Contain(includePath));
        }

        private static void AssertSlotReferences(string source)
        {
            var objects = Regex.Split(source, @"(?m)(?<=\})\s*(?=^\{)")
                .Select(JsonUtility.FromJson<GraphObject>).ToArray();
            var lookup = objects.ToDictionary(value => value.m_ObjectId);
            foreach (var node in objects.Where(value => value.m_Slots != null))
            foreach (var slot in node.m_Slots)
            {
                Assert.That(lookup.ContainsKey(slot.m_Id), Is.True, node.m_ObjectId);
                Assert.That(lookup[slot.m_Id].m_Type, Does.EndWith("MaterialSlot"), node.m_ObjectId);
            }
        }

        [Serializable]
        private sealed class GraphObject
        {
            public string m_ObjectId = string.Empty;
            public string m_Type = string.Empty;
            public ObjectReference[] m_Slots = null;
        }

        [Serializable]
        private sealed class ObjectReference
        {
            public string m_Id = string.Empty;
        }

        [Test]
        public void Shared_include_exposes_half_and_float_graph_entry_points()
        {
            var source = File.ReadAllText(HeightIncludePath);
            var requiredFunctions = new[]
            {
                "SimpleBlendHeightWeights3_half",
                "SimpleBlendHeightWeights3_float",
                "SimpleBlendHeightWeights4_half",
                "SimpleBlendHeightWeights4_float"
            };

            Assert.That(
                requiredFunctions.Where(functionName => !source.Contains(functionName)),
                Is.Empty);
        }

        [Test]
        public void Shared_surface_include_exposes_half_and_float_graph_entry_points()
        {
            var source = File.ReadAllText(SurfaceIncludePath);
            Assert.That(source, Does.Contain("SimpleBlendSurfaceBlend4_half"));
            Assert.That(source, Does.Contain("SimpleBlendSurfaceBlend4_float"));
        }
    }
}
