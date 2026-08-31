using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal sealed class MeshPaintSnapshot
    {
        private MeshPaintSnapshot(
            Mesh sourceMesh,
            Vector3[] localVertices,
            Vector3[] localNormals,
            int[] triangles,
            int[] triangleSubmeshIds,
            VertexPaintTopology topology,
            Bounds localBounds)
        {
            SourceMesh = sourceMesh;
            LocalVertices = localVertices;
            LocalNormals = localNormals;
            Triangles = triangles;
            TriangleSubmeshIds = triangleSubmeshIds;
            Topology = topology;
            LocalBounds = localBounds;
        }

        internal Mesh SourceMesh { get; }

        internal Vector3[] LocalVertices { get; }

        internal Vector3[] LocalNormals { get; }

        internal int[] Triangles { get; }

        internal int[] TriangleSubmeshIds { get; }

        internal VertexPaintTopology Topology { get; }

        internal Bounds LocalBounds { get; }

        internal int TriangleCount => Triangles.Length / 3;

        internal static VertexPaintValidationResult TryCreate(Mesh sourceMesh, out MeshPaintSnapshot snapshot)
        {
            snapshot = null;
            if (sourceMesh == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            try
            {
                using (var meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh))
                {
                    var meshData = meshDataArray[0];
                    var vertices = ReadVertices(meshData);
                    var triangles = new List<int>();
                    var submeshIds = new List<int>();

                    for (var submeshIndex = 0; submeshIndex < meshData.subMeshCount; submeshIndex++)
                    {
                        var descriptor = meshData.GetSubMesh(submeshIndex);
                        if (descriptor.topology != MeshTopology.Triangles || descriptor.indexCount < 3)
                        {
                            continue;
                        }

                        using (var indices = new NativeArray<int>(
                                   descriptor.indexCount,
                                   Allocator.Temp,
                                   NativeArrayOptions.UninitializedMemory))
                        {
                            meshData.GetIndices(indices, submeshIndex, true);
                            var completeIndexCount = indices.Length - indices.Length % 3;
                            for (var index = 0; index < completeIndexCount; index += 3)
                            {
                                var first = indices[index];
                                var second = indices[index + 1];
                                var third = indices[index + 2];
                                ValidateVertexIndex(first, vertices.Length);
                                ValidateVertexIndex(second, vertices.Length);
                                ValidateVertexIndex(third, vertices.Length);
                                triangles.Add(first);
                                triangles.Add(second);
                                triangles.Add(third);
                                submeshIds.Add(submeshIndex);
                            }
                        }
                    }

                    if (triangles.Count == 0)
                    {
                        return VertexPaintValidationResult.Invalid(
                            VertexPaintValidationFailure.NoTriangleGeometry,
                            $"Mesh '{sourceMesh.name}' has no triangle submeshes to paint.");
                    }

                    var triangleArray = triangles.ToArray();
                    var triangleSubmeshArray = submeshIds.ToArray();
                    var normals = ReadOrBuildNormals(meshData, vertices, triangleArray);
                    snapshot = new MeshPaintSnapshot(
                        sourceMesh,
                        vertices,
                        normals,
                        triangleArray,
                        triangleSubmeshArray,
                        VertexPaintTopology.Create(
                            vertices.Length,
                            triangleArray,
                            triangleSubmeshArray,
                            meshData.subMeshCount),
                        sourceMesh.bounds);
                }

                return VertexPaintValidationResult.Valid();
            }
            catch (Exception exception)
            {
                snapshot = null;
                return VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.SourceDataUnavailable,
                    $"SimpleBlend could not build a paint snapshot for '{sourceMesh.name}': {exception.Message}");
            }
        }

        private static Vector3[] ReadVertices(Mesh.MeshData meshData)
        {
            var vertices = new Vector3[meshData.vertexCount];
            using (var nativeVertices = new NativeArray<Vector3>(
                       meshData.vertexCount,
                       Allocator.Temp,
                       NativeArrayOptions.UninitializedMemory))
            {
                meshData.GetVertices(nativeVertices);
                nativeVertices.CopyTo(vertices);
            }

            return vertices;
        }

        private static Vector3[] ReadOrBuildNormals(
            Mesh.MeshData meshData,
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles)
        {
            var normals = new Vector3[vertices.Count];
            if (meshData.HasVertexAttribute(VertexAttribute.Normal))
            {
                using (var nativeNormals = new NativeArray<Vector3>(
                           meshData.vertexCount,
                           Allocator.Temp,
                           NativeArrayOptions.UninitializedMemory))
                {
                    meshData.GetNormals(nativeNormals);
                    nativeNormals.CopyTo(normals);
                }
            }

            var generatedNormals = new Vector3[vertices.Count];
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var first = triangles[index];
                var second = triangles[index + 1];
                var third = triangles[index + 2];
                var faceNormal = Vector3.Cross(
                    vertices[second] - vertices[first],
                    vertices[third] - vertices[first]);
                generatedNormals[first] += faceNormal;
                generatedNormals[second] += faceNormal;
                generatedNormals[third] += faceNormal;
            }

            for (var index = 0; index < normals.Length; index++)
            {
                var normal = normals[index].sqrMagnitude > 0.000001f
                    ? normals[index]
                    : generatedNormals[index];
                normals[index] = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            }

            return normals;
        }

        private static void ValidateVertexIndex(int index, int vertexCount)
        {
            if (index < 0 || index >= vertexCount)
            {
                throw new InvalidOperationException(
                    $"Triangle index {index} is outside the mesh vertex range 0..{vertexCount - 1}.");
            }
        }
    }
}
