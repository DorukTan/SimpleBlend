using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    /// <summary>Maps covered mask texels and two-pixel gutters to points on the selected mesh section.</summary>
    internal sealed class TexturePaintAtlas
    {
        internal readonly Vector3[] Positions;
        internal readonly Vector3[] Normals;
        internal readonly int[] Coverage;
        internal readonly int[] Triangles;
        private readonly Vector3[] _vertices;
        private readonly Vector3[] _normals;
        private readonly int[] _sections;
        private Matrix4x4 _transform;
        private bool _hasTransform;
        private readonly UniformVertexGrid _grid = new UniformVertexGrid();
        private readonly Vector3[] _rayVertices;
        private readonly Vector3[] _rayNormals;
        private readonly int[] _pixels;
        private readonly Vector3[] _worldPositions;
        private readonly Vector3[] _worldNormals;

        internal TexturePaintAtlas(Mesh mesh, int section, int resolution)
        {
            if (mesh == null || !mesh.isReadable || section < 0 || section >= mesh.subMeshCount)
                throw new ArgumentException("The paint mesh section is unavailable.");
            _vertices = mesh.vertices;
            _normals = mesh.normals;
            _rayVertices = new Vector3[_vertices.Length];
            _rayNormals = new Vector3[_normals.Length];
            if (_normals.Length != _vertices.Length) throw new ArgumentException("The paint mesh needs normals.");
            var uv = mesh.uv4;
            if (uv.Length != _vertices.Length) throw new ArgumentException("The paint mesh needs its generated UV4 atlas.");
            Triangles = mesh.GetTriangles(section);
            _sections = new int[Triangles.Length / 3];
            for (var i = 0; i < _sections.Length; i++) _sections[i] = section;
            var count = resolution * resolution;
            Positions = new Vector3[count];
            Normals = new Vector3[count];
            Coverage = new int[count];
            for (var i = 0; i < count; i++) Coverage[i] = -1;
            for (var t = 0; t < Triangles.Length; t += 3)
            {
                var a = Triangles[t]; var b = Triangles[t + 1]; var c = Triangles[t + 2];
                var ua = uv[a] * resolution; var ub = uv[b] * resolution; var uc = uv[c] * resolution;
                var denominator = Cross(ub - ua, uc - ua);
                if (Mathf.Abs(denominator) < 0.000001f) continue;
                var minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x))), 0, resolution - 1);
                var maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))), 0, resolution - 1);
                var minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y))), 0, resolution - 1);
                var maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))), 0, resolution - 1);
                for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                {
                    var delta = new Vector2(x + 0.5f, y + 0.5f) - ua;
                    var wb = Cross(delta, uc - ua) / denominator;
                    var wc = Cross(ub - ua, delta) / denominator;
                    var wa = 1 - wb - wc;
                    if (wa < -0.00001f || wb < -0.00001f || wc < -0.00001f) continue;
                    var pixel = y * resolution + x;
                    Coverage[pixel] = pixel;
                    Positions[pixel] = _vertices[a] * wa + _vertices[b] * wb + _vertices[c] * wc;
                    Normals[pixel] = (_normals[a] * wa + _normals[b] * wb + _normals[c] * wc).normalized;
                }
            }
            for (var pass = 0; pass < 2; pass++)
            {
                var before = (int[])Coverage.Clone();
                for (var y = 0; y < resolution; y++)
                for (var x = 0; x < resolution; x++)
                {
                    var pixel = y * resolution + x;
                    if (before[pixel] >= 0) continue;
                    for (var dy = -1; dy <= 1 && Coverage[pixel] < 0; dy++)
                    for (var dx = -1; dx <= 1 && Coverage[pixel] < 0; dx++)
                    {
                        var nx = x + dx; var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;
                        var nearest = before[ny * resolution + nx];
                        if (nearest < 0) continue;
                        Coverage[pixel] = nearest;
                        Positions[pixel] = Positions[nearest];
                        Normals[pixel] = Normals[nearest];
                    }
                }
            }
            var pixels = new List<int>();
            for (var i = 0; i < Coverage.Length; i++) if (Coverage[i] >= 0) pixels.Add(i);
            _pixels = pixels.ToArray();
            _worldPositions = new Vector3[_pixels.Length];
            _worldNormals = new Vector3[_pixels.Length];
        }

        internal IReadOnlyList<int> Pixels => _pixels;
        internal Vector3 WorldPosition(int index) => _worldPositions[index];
        internal Vector3 WorldNormal(int index) => _worldNormals[index];
        internal int Pixel(int index) => _pixels[index];

        internal void Query(Vector3 center, float radius, List<int> results)
        {
            if (_grid.QuerySphereLimited(center, radius, results, 8192, int.MaxValue)) return;
            // Bound empty-cell traversal for very large brushes; never silently drop paint candidates.
            results.Clear();
            var radiusSquared = radius * radius;
            for (var i = 0; i < _worldPositions.Length; i++)
                if ((_worldPositions[i] - center).sqrMagnitude <= radiusSquared) results.Add(i);
        }

        internal void SetTransform(Matrix4x4 matrix)
        {
            if (_hasTransform && _transform == matrix) return;
            _transform = matrix;
            _hasTransform = true;
            var normalMatrix = matrix.inverse.transpose;
            for (var i = 0; i < _rayVertices.Length; i++)
            {
                _rayVertices[i] = matrix.MultiplyPoint3x4(_vertices[i]);
                _rayNormals[i] = normalMatrix.MultiplyVector(_normals[i]).normalized;
            }
            var bounds = new Bounds(matrix.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
            for (var i = 0; i < _pixels.Length; i++)
            {
                _worldPositions[i] = matrix.MultiplyPoint3x4(Positions[_pixels[i]]);
                _worldNormals[i] = normalMatrix.MultiplyVector(Normals[_pixels[i]]).normalized;
                bounds.Encapsulate(_worldPositions[i]);
            }
            var extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            _grid.Rebuild(_worldPositions, Mathf.Max(0.0001f, extent / 32));
        }

        internal bool Raycast(Ray ray, out MeshRaycastHit hit)
        {
            return MeshTriangleRaycaster.TryRaycast(ray, _rayVertices, _rayNormals, Triangles, _sections, out hit);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
