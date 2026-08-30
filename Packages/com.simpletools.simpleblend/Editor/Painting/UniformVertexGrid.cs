using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class UniformVertexGrid
    {
        private readonly Dictionary<Cell, List<int>> _cells = new Dictionary<Cell, List<int>>();
        private IReadOnlyList<Vector3> _positions = Array.Empty<Vector3>();
        private float _cellSize = 1f;

        internal float CellSize => _cellSize;

        internal int CellCount => _cells.Count;

        internal void Rebuild(IReadOnlyList<Vector3> positions, float cellSize)
        {
            _cells.Clear();
            _positions = positions ?? throw new ArgumentNullException(nameof(positions));
            _cellSize = Mathf.Max(0.0001f, cellSize);

            for (var index = 0; index < positions.Count; index++)
            {
                var cell = Cell.FromPosition(positions[index], _cellSize);
                if (!_cells.TryGetValue(cell, out var indices))
                {
                    indices = new List<int>();
                    _cells.Add(cell, indices);
                }

                indices.Add(index);
            }
        }

        internal void QuerySphere(Vector3 center, float radius, List<int> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            radius = Mathf.Max(0f, radius);
            var minimum = Cell.FromPosition(center - Vector3.one * radius, _cellSize);
            var maximum = Cell.FromPosition(center + Vector3.one * radius, _cellSize);
            var radiusSquared = radius * radius;

            for (var x = minimum.X; x <= maximum.X; x++)
            {
                for (var y = minimum.Y; y <= maximum.Y; y++)
                {
                    for (var z = minimum.Z; z <= maximum.Z; z++)
                    {
                        if (!_cells.TryGetValue(new Cell(x, y, z), out var indices))
                        {
                            continue;
                        }

                        for (var index = 0; index < indices.Count; index++)
                        {
                            var vertexIndex = indices[index];
                            if ((_positions[vertexIndex] - center).sqrMagnitude <= radiusSquared)
                            {
                                results.Add(vertexIndex);
                            }
                        }
                    }
                }
            }
        }

        private readonly struct Cell : IEquatable<Cell>
        {
            internal Cell(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            internal int X { get; }

            internal int Y { get; }

            internal int Z { get; }

            internal static Cell FromPosition(Vector3 position, float cellSize)
            {
                return new Cell(
                    Mathf.FloorToInt(position.x / cellSize),
                    Mathf.FloorToInt(position.y / cellSize),
                    Mathf.FloorToInt(position.z / cellSize));
            }

            public bool Equals(Cell other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is Cell other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = X;
                    hashCode = (hashCode * 397) ^ Y;
                    hashCode = (hashCode * 397) ^ Z;
                    return hashCode;
                }
            }
        }
    }
}
