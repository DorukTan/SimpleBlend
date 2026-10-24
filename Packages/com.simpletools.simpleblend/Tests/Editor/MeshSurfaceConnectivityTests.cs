using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class MeshSurfaceConnectivityTests
    {
        [Test]
        public void SplitIndicesAndReversedEdgesRemainConnected()
        {
            var positions = new[] { Vector3.zero, Vector3.right, Vector3.up,
                Vector3.up, Vector3.right, Vector3.one };
            var parts = new MeshSurfaceConnectivity(positions, new[] { 0, 1, 2, 3, 4, 5 });
            Assert.That(parts.Component(0), Is.EqualTo(parts.Component(1)));
        }

        [TestCase(0.0000001f)]
        [TestCase(0.0001f)]
        public void SmallGapsAreNotApproximatelyWelded(float gap)
        {
            var positions = new[] { Vector3.zero, Vector3.right, Vector3.up,
                Vector3.up + Vector3.forward * gap, Vector3.right + Vector3.forward * gap, Vector3.one };
            var parts = new MeshSurfaceConnectivity(positions, new[] { 0, 1, 2, 3, 4, 5 });
            Assert.That(parts.Component(0), Is.Not.EqualTo(parts.Component(1)));
        }

        [Test]
        public void CornerContactsDoNotConnectParts()
        {
            var positions = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.left, Vector3.down };
            var parts = new MeshSurfaceConnectivity(positions, new[] { 0, 1, 2, 0, 3, 4 });
            Assert.That(parts.Component(0), Is.Not.EqualTo(parts.Component(1)));
        }

        [Test]
        public void DegenerateTriangleCannotBridgeSeparateParts()
        {
            var positions = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right * 2, new Vector3(2, 1, 0) };
            var parts = new MeshSurfaceConnectivity(positions, new[] { 0, 1, 2, 1, 3, 4, 0, 1, 3 });
            Assert.That(parts.Component(0), Is.Not.EqualTo(parts.Component(1)));
            Assert.That(parts.Component(2), Is.Not.EqualTo(parts.Component(0)));
            Assert.That(parts.Component(2), Is.Not.EqualTo(parts.Component(1)));
        }

        [Test]
        public void TransitiveAndNonmanifoldEdgesHaveDeterministicComponents()
        {
            var positions = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.down, Vector3.forward, Vector3.one };
            var triangles = new[] { 0, 1, 2, 1, 0, 3, 0, 1, 4, 1, 2, 5 };
            var parts = new MeshSurfaceConnectivity(positions, triangles);
            var repeated = new MeshSurfaceConnectivity(positions, triangles);
            for (var i = 0; i < 4; i++)
            { Assert.That(parts.Component(i), Is.Zero); Assert.That(repeated.Component(i), Is.EqualTo(parts.Component(i))); }
        }

        [Test]
        public void LongTriangleChainDoesNotNeedRecursiveTraversal()
        {
            const int count = 20000;
            var positions = new Vector3[count + 2]; var triangles = new int[count * 3];
            for (var i = 0; i < positions.Length; i++) positions[i] = new Vector3(i, i % 2, 0);
            for (var i = 0; i < count; i++) { triangles[i * 3] = i; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            var parts = new MeshSurfaceConnectivity(positions, triangles);
            Assert.That(parts.Component(count - 1), Is.EqualTo(parts.Component(0)));
        }

        [TestCase(-1)]
        [TestCase(9)]
        public void InvalidVertexReferencesReject(int vertex)
        {
            Assert.Throws<ArgumentException>(() => new MeshSurfaceConnectivity(new[] { Vector3.zero, Vector3.up, Vector3.right },
                new[] { 0, 1, vertex }));
        }

        [Test]
        public void InvalidTriangleQueriesDoNotSelectAnotherPart()
        {
            var parts = new MeshSurfaceConnectivity(new[] { Vector3.zero, Vector3.up, Vector3.right }, new[] { 0, 1, 2 });
            Assert.That(parts.Component(-1), Is.EqualTo(-1)); Assert.That(parts.Component(1), Is.EqualTo(-1));
            Assert.Throws<ArgumentException>(() => new MeshSurfaceConnectivity(null, Array.Empty<int>()));
            Assert.Throws<ArgumentException>(() => new MeshSurfaceConnectivity(Array.Empty<Vector3>(), new[] { 0 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AtlasUsesGlobalTriangleOffsetsAndGutterSourceComponents(bool mirrored)
        {
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { new Vector3(10, 0, 0), new Vector3(11, 0, 0), new Vector3(10, 1, 0),
                    Vector3.zero, Vector3.right, Vector3.up, Vector3.up, Vector3.right, new Vector3(1, 1, 0),
                    new Vector3(2, 0, 0), new Vector3(3, 0, 0), new Vector3(2, 1, 0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.back, Vector3.back, Vector3.back };
                mesh.uv4 = new[] { Vector2.zero, Vector2.right, Vector2.up,
                    new Vector2(.05f, .05f), new Vector2(.4f, .05f), new Vector2(.05f, .4f),
                    new Vector2(.55f, .05f), new Vector2(.9f, .05f), new Vector2(.55f, .4f),
                    new Vector2(.05f, .55f), new Vector2(.4f, .55f), new Vector2(.05f, .9f) };
                mesh.subMeshCount = 2; mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1);
                var atlas = new TexturePaintAtlas(mesh, 1, 64);
                atlas.SetTransform(Matrix4x4.TRS(Vector3.one, Quaternion.Euler(10, 20, 30), new Vector3(mirrored ? -2 : 2, .5f, 1.5f)));
                Assert.That(atlas.ComponentForTriangle(0), Is.EqualTo(-1));
                Assert.That(atlas.ComponentForTriangle(1), Is.EqualTo(atlas.ComponentForTriangle(2)));
                Assert.That(atlas.ComponentForTriangle(3), Is.Not.EqualTo(atlas.ComponentForTriangle(1)));
                Assert.That(atlas.ComponentForTriangle(4), Is.EqualTo(-1));
                var gutters = 0; var other = 0;
                for (var i = 0; i < atlas.Pixels.Count; i++)
                {
                    var pixel = atlas.Pixel(i);
                    var expected = atlas.Positions[pixel].x >= 1.9f ? atlas.ComponentForTriangle(3) : atlas.ComponentForTriangle(1);
                    Assert.That(atlas.ComponentAt(i), Is.EqualTo(expected));
                    if (atlas.Coverage[pixel] != pixel) gutters++;
                    if (atlas.Positions[pixel].x >= 1.9f) other++;
                }
                Assert.That(gutters, Is.GreaterThan(0)); Assert.That(other, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
