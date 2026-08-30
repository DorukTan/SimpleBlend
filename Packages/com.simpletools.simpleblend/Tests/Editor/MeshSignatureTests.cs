using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    public sealed class MeshSignatureTests
    {
        [Test]
        public void IdenticalTopologyProducesIdenticalSignature()
        {
            var mesh = TestMeshFactory.CreateQuad();
            try
            {
                var firstResult = MeshSignatureFactory.TryCreate(mesh, "asset:1", out var first);
                var secondResult = MeshSignatureFactory.TryCreate(mesh, "asset:1", out var second);

                Assert.That(firstResult.IsValid, Is.True, firstResult.Message);
                Assert.That(secondResult.IsValid, Is.True, secondResult.Message);
                Assert.That(first.Compare(second), Is.EqualTo(VertexPaintValidationFailure.None));
                Assert.That(first, Is.EqualTo(second));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void SameVertexCountWithChangedIndicesIsRejected()
        {
            var mesh = TestMeshFactory.CreateQuad();
            try
            {
                MeshSignatureFactory.TryCreate(mesh, "asset:1", out var before);
                mesh.SetTriangles(new[] { 0, 1, 3 }, 0);
                MeshSignatureFactory.TryCreate(mesh, "asset:1", out var after);

                Assert.That(before.VertexCount, Is.EqualTo(after.VertexCount));
                Assert.That(before.Compare(after), Is.EqualTo(VertexPaintValidationFailure.TopologyMismatch));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ChangedSubMeshLayoutIsRejectedBeforeTopologyHash()
        {
            var mesh = TestMeshFactory.CreateQuad();
            try
            {
                MeshSignatureFactory.TryCreate(mesh, "asset:1", out var before);
                mesh.subMeshCount = 1;
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                MeshSignatureFactory.TryCreate(mesh, "asset:1", out var after);

                Assert.That(before.Compare(after), Is.EqualTo(VertexPaintValidationFailure.SubMeshLayoutMismatch));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ChangedVertexCountAndSourceIdentityAreDistinctFailures()
        {
            var mesh = TestMeshFactory.CreateQuad();
            var triangle = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 }
            };

            try
            {
                MeshSignatureFactory.TryCreate(mesh, "asset:1", out var original);
                MeshSignatureFactory.TryCreate(triangle, "asset:1", out var changedCount);
                MeshSignatureFactory.TryCreate(mesh, "asset:2", out var changedIdentity);

                Assert.That(original.Compare(changedCount), Is.EqualTo(VertexPaintValidationFailure.VertexCountMismatch));
                Assert.That(original.Compare(changedIdentity), Is.EqualTo(VertexPaintValidationFailure.SourceIdentityMismatch));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(triangle);
            }
        }
    }
}
