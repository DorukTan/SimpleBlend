using System;
using Unity.Collections;
using UnityEngine;

namespace SimpleBlend
{
    internal static class MeshSignatureFactory
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        internal static VertexPaintValidationResult TryCreate(
            Mesh mesh,
            string sourceIdentity,
            out MeshSignature signature)
        {
            signature = default;

            if (mesh == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            try
            {
                using (var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var meshData = meshDataArray[0];
                    var topologies = new MeshTopology[meshData.subMeshCount];
                    var indexCounts = new int[meshData.subMeshCount];
                    var hash = FnvOffsetBasis;

                    AddInt(ref hash, meshData.vertexCount);
                    AddInt(ref hash, meshData.subMeshCount);

                    for (var subMeshIndex = 0; subMeshIndex < meshData.subMeshCount; subMeshIndex++)
                    {
                        var descriptor = meshData.GetSubMesh(subMeshIndex);
                        topologies[subMeshIndex] = descriptor.topology;
                        indexCounts[subMeshIndex] = descriptor.indexCount;

                        AddInt(ref hash, (int)descriptor.topology);
                        AddInt(ref hash, descriptor.indexCount);
                        AddInt(ref hash, descriptor.baseVertex);

                        if (descriptor.indexCount == 0)
                        {
                            continue;
                        }

                        using (var indices = new NativeArray<int>(
                                   descriptor.indexCount,
                                   Allocator.Temp,
                                   NativeArrayOptions.UninitializedMemory))
                        {
                            meshData.GetIndices(indices, subMeshIndex, false);
                            for (var index = 0; index < indices.Length; index++)
                            {
                                AddInt(ref hash, indices[index]);
                            }
                        }
                    }

                    signature = new MeshSignature(
                        sourceIdentity,
                        meshData.vertexCount,
                        topologies,
                        indexCounts,
                        unchecked((long)hash));
                }

                return VertexPaintValidationResult.Valid();
            }
            catch (Exception exception)
            {
                return VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.SourceDataUnavailable,
                    $"Unity could not read source mesh '{mesh.name}': {exception.Message}");
            }
        }

        private static void AddInt(ref ulong hash, int value)
        {
            var unsigned = unchecked((uint)value);
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(unsigned >> shift);
                hash *= FnvPrime;
            }
        }
    }
}
