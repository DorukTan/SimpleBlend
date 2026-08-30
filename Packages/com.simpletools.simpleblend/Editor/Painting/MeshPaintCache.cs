using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class MeshPaintCache
    {
        private readonly UniformVertexGrid _grid = new UniformVertexGrid();
        private MeshPaintSnapshot _snapshot;
        private Mesh _sourceMesh;
        private MeshSignature _sourceSignature;
        private Matrix4x4 _localToWorld;
        private Vector3[] _worldVertices = Array.Empty<Vector3>();
        private Vector3[] _worldNormals = Array.Empty<Vector3>();
        private bool _topologyInvalidated = true;
        private bool _hasTransform;

        internal MeshPaintSnapshot Snapshot => _snapshot;

        internal IReadOnlyList<Vector3> WorldVertices => _worldVertices;

        internal IReadOnlyList<Vector3> WorldNormals => _worldNormals;

        internal Matrix4x4 LocalToWorld => _localToWorld;

        internal int SnapshotRevision { get; private set; }

        internal int TransformRevision { get; private set; }

        internal void InvalidateTopology()
        {
            _topologyInvalidated = true;
        }

        internal VertexPaintValidationResult Ensure(VertexPaintData data)
        {
            if (data == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            var needsSnapshot = _topologyInvalidated
                                || _snapshot == null
                                || _sourceMesh != data.SourceMesh
                                || _sourceSignature != data.SourceSignature;
            if (needsSnapshot)
            {
                var identity = MeshAssetIdentity.GetStableIdentity(data.SourceMesh);
                var validation = VertexPaintValidator.Validate(data, identity);
                if (!validation.IsValid)
                {
                    return validation;
                }

                var snapshotResult = MeshPaintSnapshot.TryCreate(data.SourceMesh, out var snapshot);
                if (!snapshotResult.IsValid)
                {
                    return snapshotResult;
                }

                _snapshot = snapshot;
                _sourceMesh = data.SourceMesh;
                _sourceSignature = data.SourceSignature;
                _worldVertices = new Vector3[snapshot.LocalVertices.Length];
                _worldNormals = new Vector3[snapshot.LocalNormals.Length];
                _topologyInvalidated = false;
                _hasTransform = false;
                SnapshotRevision++;
            }

            var currentTransform = data.transform.localToWorldMatrix;
            if (!_hasTransform || !ApproximatelyEqual(_localToWorld, currentTransform))
            {
                RebuildWorldData(currentTransform);
            }

            return VertexPaintValidationResult.Valid();
        }

        internal bool TryRaycast(Ray worldRay, out MeshRaycastHit hit)
        {
            if (_snapshot == null)
            {
                hit = default;
                return false;
            }

            return MeshTriangleRaycaster.TryRaycast(
                worldRay,
                _worldVertices,
                _worldNormals,
                _snapshot.Triangles,
                _snapshot.TriangleSubmeshIds,
                out hit);
        }

        internal void QueryVertices(Vector3 center, float radius, List<int> results)
        {
            _grid.QuerySphere(center, radius, results);
        }

        private void RebuildWorldData(Matrix4x4 localToWorld)
        {
            _localToWorld = localToWorld;
            var normalMatrix = localToWorld.inverse.transpose;
            var bounds = new Bounds();

            for (var index = 0; index < _snapshot.LocalVertices.Length; index++)
            {
                var worldVertex = localToWorld.MultiplyPoint3x4(_snapshot.LocalVertices[index]);
                var worldNormal = normalMatrix.MultiplyVector(_snapshot.LocalNormals[index]);
                _worldVertices[index] = worldVertex;
                _worldNormals[index] = worldNormal.sqrMagnitude > 0.000001f
                    ? worldNormal.normalized
                    : Vector3.up;

                if (index == 0)
                {
                    bounds = new Bounds(worldVertex, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(worldVertex);
                }
            }

            var largestExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var density = Mathf.Max(1f, Mathf.Pow(Mathf.Max(1, _worldVertices.Length), 1f / 3f));
            var cellSize = Mathf.Max(0.0001f, largestExtent / density);
            _grid.Rebuild(_worldVertices, cellSize);
            _hasTransform = true;
            TransformRevision++;
        }

        private static bool ApproximatelyEqual(Matrix4x4 left, Matrix4x4 right)
        {
            for (var index = 0; index < 16; index++)
            {
                if (!Mathf.Approximately(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
