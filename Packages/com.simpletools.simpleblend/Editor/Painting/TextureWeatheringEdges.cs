using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    /// <summary>Physical-border or UV0-seam segments, with bounds for local-space distance queries.</summary>
    internal sealed class TextureWeatheringEdges
    {
        private sealed class Edge
        {
            internal Vector3 A, B;
            internal Vector2 UvA, UvB;
            internal int Incidence;
            internal bool Selected, Seam;
        }

        private struct Node
        {
            internal Bounds Bounds;
            internal int Start, Count, Left, Right;
        }

        private readonly Edge[] _edges;
        private readonly List<Node> _nodes = new List<Node>();
        internal int Count => _edges.Length;

        internal TextureWeatheringEdges(Mesh mesh, int section, TextureWeatheringSource source, Func<bool> cancelled = null)
        {
            var vertices = mesh.vertices; var uv = mesh.uv;
            if (source != TextureWeatheringSource.MeshBorders && source != TextureWeatheringSource.UvSeams)
                throw new ArgumentException("Choose mesh borders or UV seams.");
            if (source == TextureWeatheringSource.UvSeams && uv.Length != vertices.Length)
                throw new ArgumentException("UV seam weathering needs original UV0 coordinates for every vertex.");
            var welded = new Dictionary<Vector3, int>(); var ids = new int[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                if ((i & 1023) == 0) CheckCancellation(cancelled);
                if (!welded.TryGetValue(vertices[i], out var id)) { id = welded.Count; welded.Add(vertices[i], id); }
                ids[i] = id;
            }
            var edges = new Dictionary<(int, int), Edge>();
            for (var slot = 0; slot < mesh.subMeshCount; slot++)
            {
                var triangles = mesh.GetTriangles(slot);
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    if ((i & 255) == 0) CheckCancellation(cancelled);
                    Add(triangles[i], triangles[i + 1], slot == section);
                    Add(triangles[i + 1], triangles[i + 2], slot == section);
                    Add(triangles[i + 2], triangles[i], slot == section);
                }
            }
            var selected = new List<Edge>();
            foreach (var edge in edges.Values)
                if (edge.Selected && (source == TextureWeatheringSource.MeshBorders ? edge.Incidence == 1 : edge.Incidence == 2 && edge.Seam)) selected.Add(edge);
            _edges = selected.ToArray();
            if (_edges.Length > 0) Build(0, _edges.Length);

            void Add(int a, int b, bool inSection)
            {
                if (ids[a] == ids[b]) return;
                if (ids[a] > ids[b]) { var swap = a; a = b; b = swap; }
                var key = (ids[a], ids[b]);
                var ua = uv.Length == vertices.Length ? uv[a] : Vector2.zero;
                var ub = uv.Length == vertices.Length ? uv[b] : Vector2.zero;
                if (!edges.TryGetValue(key, out var edge))
                {
                    edge = new Edge { A = vertices[a], B = vertices[b], UvA = ua, UvB = ub };
                    edges.Add(key, edge);
                }
                else if (!edge.UvA.Equals(ua) || !edge.UvB.Equals(ub)) edge.Seam = true;
                edge.Incidence++; edge.Selected |= inSection;
            }
        }

        internal static void CheckCancellation(Func<bool> cancelled)
        {
            if (cancelled != null && cancelled()) throw new OperationCanceledException("Weathering generation cancelled; saved paint was not changed.");
        }

        private int Build(int start, int count)
        {
            var bounds = new Bounds(_edges[start].A, Vector3.zero);
            for (var i = start; i < start + count; i++) { bounds.Encapsulate(_edges[i].A); bounds.Encapsulate(_edges[i].B); }
            var index = _nodes.Count; var node = new Node { Bounds = bounds, Start = start, Count = count }; _nodes.Add(node);
            if (count > 8)
            {
                var size = bounds.size; var axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                Array.Sort(_edges, start, count, Comparer<Edge>.Create((a, b) => ((a.A[axis] + a.B[axis]) * 0.5f).CompareTo((b.A[axis] + b.B[axis]) * 0.5f)));
                var half = count / 2; node.Left = Build(start, half); node.Right = Build(start + half, count - half); node.Count = 0; _nodes[index] = node;
            }
            return index;
        }

        internal float Distance(Vector3 point, float limit)
        {
            var squared = limit * limit;
            if (_nodes.Count > 0) Visit(0, point, ref squared);
            return Mathf.Sqrt(squared);
        }

        private void Visit(int index, Vector3 point, ref float squared)
        {
            var node = _nodes[index];
            if (node.Bounds.SqrDistance(point) > squared) return;
            if (node.Count == 0) { Visit(node.Left, point, ref squared); Visit(node.Right, point, ref squared); return; }
            for (var i = node.Start; i < node.Start + node.Count; i++)
            {
                var edge = _edges[i]; var delta = edge.B - edge.A;
                var t = Mathf.Clamp01(Vector3.Dot(point - edge.A, delta) / delta.sqrMagnitude);
                squared = Mathf.Min(squared, (point - (edge.A + delta * t)).sqrMagnitude);
            }
        }
    }
}
