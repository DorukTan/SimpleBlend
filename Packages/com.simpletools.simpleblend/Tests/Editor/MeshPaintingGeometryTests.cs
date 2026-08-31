using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class MeshPaintingGeometryTests
    {
        [Test]
        public void Snapshot_reads_non_readable_vertices_normals_triangles_and_submeshes()
        {
            var mesh = TestMeshFactory.CreateQuad();
            try
            {
                mesh.UploadMeshData(true);

                var result = MeshPaintSnapshot.TryCreate(mesh, out var snapshot);

                Assert.That(result.IsValid, Is.True, result.Message);
                Assert.That(snapshot.LocalVertices.Length, Is.EqualTo(4));
                Assert.That(snapshot.LocalNormals, Has.Length.EqualTo(4));
                Assert.That(snapshot.Triangles, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
                Assert.That(snapshot.TriangleSubmeshIds, Is.EqualTo(new[] { 0, 1 }));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Cpu_raycast_hits_selected_mesh_without_a_collider()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = new GameObject("Collider-free paint target");
            try
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = gameObject.AddComponent<MeshRenderer>();
                var data = gameObject.AddComponent<VertexPaintData>();
                Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);
                Assert.That(gameObject.GetComponent<Collider>(), Is.Null);

                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);

                var ray = new Ray(new Vector3(0.5f, 2f, -0.5f), Vector3.down);
                Assert.That(cache.TryRaycast(ray, out var hit), Is.True);
                Assert.That(Vector3.Distance(hit.Point, new Vector3(0.5f, 0f, -0.5f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(hit.Normal, Vector3.up), Is.LessThan(0.0001f));
                Assert.That(hit.SubmeshIndex, Is.EqualTo(0));
                Assert.That(hit.Distance, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Cpu_raycast_hits_backfaces_and_orients_the_hit_normal_toward_the_ray_origin()
        {
            var vertices = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(0f, 0f, 1f)
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            var triangles = new[] { 0, 2, 1 };
            var submeshes = new[] { 0 };
            var ray = new Ray(new Vector3(0f, -2f, 0f), Vector3.up);

            Assert.That(
                MeshTriangleRaycaster.TryRaycast(
                    ray,
                    vertices,
                    normals,
                    triangles,
                    submeshes,
                    out var hit),
                Is.True);
            Assert.That(hit.Distance, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(Vector3.Distance(hit.Point, Vector3.zero), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(hit.Normal, Vector3.down), Is.LessThan(0.0001f));
        }

        [Test]
        public void Uniform_grid_queries_match_brute_force_results()
        {
            var positions = new List<Vector3>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    positions.Add(new Vector3(x * 0.4f, y * 0.35f, (x + y) * 0.1f));
                }
            }

            var grid = new UniformVertexGrid();
            grid.Rebuild(positions, 0.45f);
            var actual = new List<int>();
            var queries = new[]
            {
                (center: Vector3.zero, radius: 0.75f),
                (center: new Vector3(1.1f, -0.25f, 0.2f), radius: 1.2f),
                (center: new Vector3(-2f, 2f, 0f), radius: 0.2f)
            };

            foreach (var query in queries)
            {
                grid.QuerySphere(query.center, query.radius, actual);
                var expected = Enumerable.Range(0, positions.Count)
                    .Where(index => Vector3.Distance(positions[index], query.center) <= query.radius)
                    .OrderBy(index => index)
                    .ToArray();

                Assert.That(actual.OrderBy(index => index).ToArray(), Is.EqualTo(expected));
            }
        }

        [Test]
        public void Cache_has_one_owner_for_topology_and_transform_invalidation()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = new GameObject("Cache target");
            try
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = gameObject.AddComponent<MeshRenderer>();
                var data = gameObject.AddComponent<VertexPaintData>();
                Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);

                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var firstSnapshotRevision = cache.SnapshotRevision;
                var firstTransformRevision = cache.TransformRevision;
                var firstWorldVertex = cache.WorldVertices[0];

                Assert.That(cache.Ensure(data).IsValid, Is.True);
                Assert.That(cache.SnapshotRevision, Is.EqualTo(firstSnapshotRevision));
                Assert.That(cache.TransformRevision, Is.EqualTo(firstTransformRevision));

                gameObject.transform.position = new Vector3(3f, 2f, 1f);
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                Assert.That(cache.SnapshotRevision, Is.EqualTo(firstSnapshotRevision));
                Assert.That(cache.TransformRevision, Is.EqualTo(firstTransformRevision + 1));
                Assert.That(
                    Vector3.Distance(cache.WorldVertices[0], firstWorldVertex + gameObject.transform.position),
                    Is.LessThan(0.0001f));

                cache.InvalidateTopology();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                Assert.That(cache.SnapshotRevision, Is.EqualTo(firstSnapshotRevision + 1));
                Assert.That(cache.TransformRevision, Is.EqualTo(firstTransformRevision + 2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Normal_mask_uses_inverse_transpose_world_normals_under_rotation_and_negative_scale()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var gameObject = new GameObject("Normal mask transform");
            try
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = gameObject.AddComponent<MeshRenderer>();
                var data = gameObject.AddComponent<VertexPaintData>();
                gameObject.transform.rotation = Quaternion.Euler(25f, 35f, 15f);
                gameObject.transform.localScale = new Vector3(-2f, 0.5f, 3f);
                Assert.That(VertexPaintDataAuthoring.Initialize(data, renderer, false).IsValid, Is.True);
                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var expectedNormal = gameObject.transform.localToWorldMatrix.inverse.transpose
                    .MultiplyVector(Vector3.up)
                    .normalized;
                Assert.That(Vector3.Distance(cache.WorldNormals[0], expectedNormal), Is.LessThan(0.0001f));

                var colors = TestMeshFactory.SolidColors(mesh.vertexCount, new Color32(0, 0, 0, 0));
                var mask = new VertexBrushMask(
                    true,
                    expectedNormal,
                    1f,
                    false,
                    0,
                    false,
                    VertexPaintChannel.Red,
                    0f,
                    1f,
                    true);
                var command = new VertexBrushCommand(
                    cache.WorldVertices[0],
                    100f,
                    1f,
                    1f,
                    new Color32(255, 0, 0, 0),
                    AnimationCurve.Constant(0f, 1f, 1f),
                    VertexPaintBrushMode.Paint,
                    VertexPaintChannel.Red,
                    true,
                    mask);

                var changed = VertexPaintKernel.Apply(
                    command,
                    cache.WorldVertices,
                    cache.WorldNormals,
                    cache.Snapshot.Topology.AllVertices,
                    cache.Snapshot.Topology,
                    colors);

                Assert.That(changed, Is.EqualTo(mesh.vertexCount));
                Assert.That(colors, Is.All.Matches<Color32>(color => color.r == byte.MaxValue));
                cache.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
