using System;
using System.Collections.Generic;

namespace SimpleBlend
{
    internal sealed class VertexPaintTopology
    {
        private readonly int[] _allVertices;
        private readonly int[][] _neighbors;
        private readonly int[][] _submeshVertices;

        private VertexPaintTopology(int[] allVertices, int[][] neighbors, int[][] submeshVertices)
        {
            _allVertices = allVertices;
            _neighbors = neighbors;
            _submeshVertices = submeshVertices;
        }

        internal int VertexCount => _allVertices.Length;

        internal int SubmeshCount => _submeshVertices.Length;

        internal IReadOnlyList<int> AllVertices => _allVertices;

        internal IReadOnlyList<int> GetNeighbors(int vertexIndex)
        {
            ValidateVertex(vertexIndex);
            return _neighbors[vertexIndex];
        }

        internal IReadOnlyList<int> GetSubmeshVertices(int submeshIndex)
        {
            if (submeshIndex < 0 || submeshIndex >= _submeshVertices.Length)
            {
                return Array.Empty<int>();
            }

            return _submeshVertices[submeshIndex];
        }

        internal bool ContainsVertex(int submeshIndex, int vertexIndex)
        {
            if (submeshIndex < 0
                || submeshIndex >= _submeshVertices.Length
                || vertexIndex < 0
                || vertexIndex >= VertexCount)
            {
                return false;
            }

            return Array.BinarySearch(_submeshVertices[submeshIndex], vertexIndex) >= 0;
        }

        internal static VertexPaintTopology Create(
            int vertexCount,
            IReadOnlyList<int> triangles,
            IReadOnlyList<int> triangleSubmeshIds,
            int submeshCount)
        {
            if (vertexCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            }

            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            if (triangleSubmeshIds == null)
            {
                throw new ArgumentNullException(nameof(triangleSubmeshIds));
            }

            if (triangles.Count % 3 != 0 || triangles.Count / 3 != triangleSubmeshIds.Count)
            {
                throw new ArgumentException("Triangle indices and submesh IDs must describe the same triangles.");
            }

            submeshCount = Math.Max(0, submeshCount);
            var neighbors = new HashSet<int>[vertexCount];
            var submeshVertices = new HashSet<int>[submeshCount];
            for (var index = 0; index < vertexCount; index++)
            {
                neighbors[index] = new HashSet<int>();
            }

            for (var index = 0; index < submeshCount; index++)
            {
                submeshVertices[index] = new HashSet<int>();
            }

            for (var triangleIndex = 0; triangleIndex < triangleSubmeshIds.Count; triangleIndex++)
            {
                var offset = triangleIndex * 3;
                var first = triangles[offset];
                var second = triangles[offset + 1];
                var third = triangles[offset + 2];
                ValidateTriangleVertex(first, vertexCount);
                ValidateTriangleVertex(second, vertexCount);
                ValidateTriangleVertex(third, vertexCount);
                AddEdge(neighbors, first, second);
                AddEdge(neighbors, second, third);
                AddEdge(neighbors, third, first);

                var submesh = triangleSubmeshIds[triangleIndex];
                if (submesh < 0 || submesh >= submeshCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(triangleSubmeshIds),
                        $"Submesh index {submesh} is outside 0..{submeshCount - 1}.");
                }

                submeshVertices[submesh].Add(first);
                submeshVertices[submesh].Add(second);
                submeshVertices[submesh].Add(third);
            }

            var allVertices = new int[vertexCount];
            var neighborArrays = new int[vertexCount][];
            for (var index = 0; index < vertexCount; index++)
            {
                allVertices[index] = index;
                neighborArrays[index] = ToSortedArray(neighbors[index]);
            }

            var submeshArrays = new int[submeshCount][];
            for (var index = 0; index < submeshCount; index++)
            {
                submeshArrays[index] = ToSortedArray(submeshVertices[index]);
            }

            return new VertexPaintTopology(allVertices, neighborArrays, submeshArrays);
        }

        private static void AddEdge(IReadOnlyList<HashSet<int>> neighbors, int first, int second)
        {
            if (first == second)
            {
                return;
            }

            neighbors[first].Add(second);
            neighbors[second].Add(first);
        }

        private static int[] ToSortedArray(HashSet<int> values)
        {
            var result = new int[values.Count];
            values.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        private static void ValidateTriangleVertex(int vertexIndex, int vertexCount)
        {
            if (vertexIndex < 0 || vertexIndex >= vertexCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexIndex),
                    $"Triangle vertex {vertexIndex} is outside 0..{vertexCount - 1}.");
            }
        }

        private void ValidateVertex(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexIndex));
            }
        }
    }
}
