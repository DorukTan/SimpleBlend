using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class VertexPaintVisualizationTests
    {
        [Test]
        public void Preview_modes_isolate_channels_without_changing_source_color()
        {
            var source = new Color32(64, 128, 192, 32);

            var red = VertexPaintPreviewUtility.Evaluate(source, 0f, VertexPaintPreviewMode.Red);
            var green = VertexPaintPreviewUtility.Evaluate(source, 0f, VertexPaintPreviewMode.Green);
            var blue = VertexPaintPreviewUtility.Evaluate(source, 0f, VertexPaintPreviewMode.Blue);
            var alpha = VertexPaintPreviewUtility.Evaluate(source, 0f, VertexPaintPreviewMode.Alpha);
            var rgb = VertexPaintPreviewUtility.Evaluate(source, 0f, VertexPaintPreviewMode.RGB);

            Assert.That(red.r, Is.EqualTo(64f / 255f).Within(0.0001f));
            Assert.That(red.g, Is.Zero);
            Assert.That(red.b, Is.Zero);
            Assert.That(green.r, Is.Zero);
            Assert.That(green.g, Is.EqualTo(128f / 255f).Within(0.0001f));
            Assert.That(green.b, Is.Zero);
            Assert.That(blue.r, Is.Zero);
            Assert.That(blue.g, Is.Zero);
            Assert.That(blue.b, Is.EqualTo(192f / 255f).Within(0.0001f));
            Assert.That(alpha.r, Is.EqualTo(32f / 255f).Within(0.0001f));
            Assert.That(alpha.g, Is.EqualTo(alpha.r).Within(0.0001f));
            Assert.That(alpha.b, Is.EqualTo(alpha.r).Within(0.0001f));
            Assert.That(rgb.r, Is.EqualTo(64f / 255f).Within(0.0001f));
            Assert.That(rgb.g, Is.EqualTo(128f / 255f).Within(0.0001f));
            Assert.That(rgb.b, Is.EqualTo(192f / 255f).Within(0.0001f));
            Assert.That(source, Is.EqualTo(new Color32(64, 128, 192, 32)));
        }

        [Test]
        public void Preview_shader_exposes_mode_control()
        {
            var shader = Shader.Find(VertexColorPreviewRenderer.PreviewShaderName);

            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_PreviewMode"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Equal_spacing_maps_connected_vertices_to_neutral_density()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0.5f, 0f, 0.8660254f)
            };
            var topology = VertexPaintTopology.Create(3, new[] { 0, 1, 2 }, new[] { 0 }, 1);

            var density = VertexDensityCalculator.Calculate(positions, topology);

            Assert.That(density, Is.All.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Density_distinguishes_dense_and_sparse_topology_and_marks_isolated_vertices_sparse()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.1f, 0f, 0f),
                new Vector3(0.05f, 0f, 0.08660254f),
                new Vector3(10f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                new Vector3(15f, 0f, 8.660254f),
                new Vector3(50f, 0f, 50f)
            };
            var topology = VertexPaintTopology.Create(
                positions.Length,
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 0 },
                1);

            var density = VertexDensityCalculator.Calculate(positions, topology);

            Assert.That(density.Take(3), Is.All.GreaterThan(0.99f));
            Assert.That(density.Skip(3).Take(3), Is.All.LessThan(0.01f));
            Assert.That(density[6], Is.Zero);
        }

        [Test]
        public void Cache_replaces_density_data_when_nonuniform_scale_changes_world_spacing()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = new GameObject("Density transform");
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var data = gameObject.AddComponent<VertexPaintData>();
            var cache = new MeshPaintCache();
            try
            {
                Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var firstRevision = cache.TransformRevision;
                var before = cache.DensityValues;

                gameObject.transform.localScale = new Vector3(6f, 1f, 1f);
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var after = cache.DensityValues.ToArray();
                var expected = VertexDensityCalculator.Calculate(
                    cache.WorldVertices,
                    cache.Snapshot.Topology);

                Assert.That(cache.TransformRevision, Is.GreaterThan(firstRevision));
                Assert.That(cache.DensityValues, Is.Not.SameAs(before));
                Assert.That(after, Is.EqualTo(expected).Within(0.0001f));
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Density_preview_uses_cold_to_hot_scale()
        {
            var sparse = VertexPaintPreviewUtility.Evaluate(default, 0f, VertexPaintPreviewMode.Density);
            var dense = VertexPaintPreviewUtility.Evaluate(default, 1f, VertexPaintPreviewMode.Density);

            Assert.That(sparse.b, Is.GreaterThan(sparse.r));
            Assert.That(dense.r, Is.GreaterThan(dense.b));
            Assert.That(sparse.a, Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(dense.a, Is.EqualTo(0.85f).Within(0.0001f));
        }
    }
}
