using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    /// <summary>Section-local triangle components joined by exact positional edges, independent of UV/normal splits.</summary>
    internal sealed class MeshSurfaceConnectivity
    {
        private readonly int[] _components;

        internal MeshSurfaceConnectivity(Vector3[] positions, int[] triangles)
        {
            if (positions == null || triangles == null || triangles.Length % 3 != 0)
                throw new ArgumentException("Connectivity needs positions and complete triangles.");
            _components = new int[triangles.Length / 3];
            var vertices = new Dictionary<Vector3, int>();
            var canonical = new int[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                if (!vertices.TryGetValue(positions[i], out var id))
                { id = vertices.Count; vertices.Add(positions[i], id); }
                canonical[i] = id;
            }
            var edges = new Dictionary<ulong, int>();
            for (var t = 0; t < _components.Length; t++)
            {
                _components[t] = t;
                var a = triangles[t * 3]; var b = triangles[t * 3 + 1]; var c = triangles[t * 3 + 2];
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length)
                    throw new ArgumentException("A connectivity triangle references a missing vertex.");
                var area = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).sqrMagnitude;
                if (!float.IsFinite(area) || area <= 0) continue;
                JoinEdge(canonical[a], canonical[b], t, edges);
                JoinEdge(canonical[b], canonical[c], t, edges);
                JoinEdge(canonical[c], canonical[a], t, edges);
            }
            for (var t = 0; t < _components.Length; t++) _components[t] = Root(t);
        }

        internal int Component(int triangle) => triangle >= 0 && triangle < _components.Length ? _components[triangle] : -1;

        private int Root(int triangle)
        {
            while (_components[triangle] != triangle)
            { _components[triangle] = _components[_components[triangle]]; triangle = _components[triangle]; }
            return triangle;
        }

        private void JoinEdge(int a, int b, int triangle, Dictionary<ulong, int> edges)
        {
            var key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            if (!edges.TryGetValue(key, out var previous)) { edges.Add(key, triangle); return; }
            var first = Root(previous); var second = Root(triangle);
            // Stable smaller roots keep results deterministic without recursive graph traversal.
            _components[Math.Max(first, second)] = Math.Min(first, second);
        }
    }
}
