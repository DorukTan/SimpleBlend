using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    /// <summary>World-space triangle bounds for repeated, allocation-free mesh ray queries.</summary>
    internal sealed class MeshRaycastIndex
    {
        private struct Node
        {
            internal Bounds Bounds;
            internal int Start, Count, Left, Right;
        }

        private readonly Vector3[] _vertices, _normals;
        private readonly int[] _triangles, _sections, _order;
        private readonly Bounds[] _bounds;
        private readonly List<Node> _nodes = new List<Node>();

        internal MeshRaycastIndex(Vector3[] vertices, Vector3[] normals, int[] triangles, int[] sections)
        {
            _vertices = vertices; _normals = normals; _triangles = triangles; _sections = sections;
            _order = new int[sections.Length]; _bounds = new Bounds[sections.Length];
        }

        internal void Rebuild()
        {
            _nodes.Clear();
            for (var i = 0; i < _order.Length; i++)
            {
                _order[i] = i;
                var bounds = new Bounds(_vertices[_triangles[i * 3]], Vector3.zero);
                bounds.Encapsulate(_vertices[_triangles[i * 3 + 1]]);
                bounds.Encapsulate(_vertices[_triangles[i * 3 + 2]]);
                // Give flat bounds a small thickness for parallel/on-plane ray tests.
                bounds.Expand(0.000002f); _bounds[i] = bounds;
            }
            if (_order.Length > 0) Build(0, _order.Length);
        }

        private int Build(int start, int count)
        {
            var bounds = _bounds[_order[start]];
            for (var i = start + 1; i < start + count; i++) bounds.Encapsulate(_bounds[_order[i]]);
            var index = _nodes.Count;
            var node = new Node { Bounds = bounds, Start = start, Count = count };
            _nodes.Add(node);
            if (count > 8)
            {
                var size = bounds.size;
                var axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                Array.Sort(_order, start, count, Comparer<int>.Create((a, b) =>
                {
                    var comparison = _bounds[a].center[axis].CompareTo(_bounds[b].center[axis]);
                    return comparison != 0 ? comparison : a.CompareTo(b);
                }));
                var half = count / 2;
                node.Left = Build(start, half); node.Right = Build(start + half, count - half);
                node.Count = 0; _nodes[index] = node;
            }
            return index;
        }

        internal bool Raycast(Ray ray, out MeshRaycastHit hit, float maximumDistance = float.PositiveInfinity)
        {
            hit = default;
            if (_nodes.Count == 0 || ray.direction.sqrMagnitude < 0.000001f || maximumDistance < 0) return false;
            ray.direction = ray.direction.normalized;
            var distance = maximumDistance; var triangle = int.MaxValue; var barycentric = Vector3.zero;
            Visit(0, ray, ref distance, ref triangle, ref barycentric);
            if (triangle == int.MaxValue) return false;
            var offset = triangle * 3;
            var a = _triangles[offset]; var b = _triangles[offset + 1]; var c = _triangles[offset + 2];
            var normal = _normals[a] * barycentric.x + _normals[b] * barycentric.y + _normals[c] * barycentric.z;
            if (normal.sqrMagnitude < 0.000001f) normal = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
            normal.Normalize();
            if (Vector3.Dot(normal, ray.direction) > 0) normal = -normal;
            hit = new MeshRaycastHit(distance, ray.GetPoint(distance), normal, barycentric, triangle, _sections[triangle]);
            return true;
        }

        private void Visit(int index, Ray ray, ref float distance, ref int triangle, ref Vector3 barycentric)
        {
            var node = _nodes[index];
            if (!node.Bounds.IntersectRay(ray, out var entry) || entry > distance) return;
            if (node.Count == 0)
            {
                Visit(node.Left, ray, ref distance, ref triangle, ref barycentric);
                Visit(node.Right, ray, ref distance, ref triangle, ref barycentric);
                return;
            }
            for (var i = node.Start; i < node.Start + node.Count; i++)
            {
                var candidate = _order[i]; var offset = candidate * 3;
                if (!MeshTriangleRaycaster.TryIntersect(ray.origin, ray.direction, _vertices[_triangles[offset]],
                        _vertices[_triangles[offset + 1]], _vertices[_triangles[offset + 2]], out var found, out var weights)
                    || found > distance || (found == distance && candidate >= triangle)) continue;
                distance = found; triangle = candidate; barycentric = weights;
            }
        }
    }
}
