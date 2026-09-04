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
        private float[] _densityValues = Array.Empty<float>();
        private bool _topologyInvalidated = true;
        private bool _hasTransform;
        private Mesh _bakedSkinnedMesh;
        private SkinnedMeshRenderer _skinnedRenderer;
        private int _skinnedPoseHash;

        internal MeshPaintSnapshot Snapshot => _snapshot;

        internal IReadOnlyList<Vector3> WorldVertices => _worldVertices;

        internal IReadOnlyList<Vector3> WorldNormals => _worldNormals;

        internal IReadOnlyList<float> DensityValues => _densityValues;

        internal Matrix4x4 LocalToWorld => _localToWorld;

        internal int SnapshotRevision { get; private set; }

        internal int TransformRevision { get; private set; }

        internal void InvalidateTopology()
        {
            _topologyInvalidated = true;
        }

        internal void Dispose()
        {
            if (_bakedSkinnedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(_bakedSkinnedMesh);
                _bakedSkinnedMesh = null;
            }

            _snapshot = null;
            _sourceMesh = null;
            _skinnedRenderer = null;
            _worldVertices = Array.Empty<Vector3>();
            _worldNormals = Array.Empty<Vector3>();
            _densityValues = Array.Empty<float>();
            _topologyInvalidated = true;
            _hasTransform = false;
        }

        internal VertexPaintValidationResult Ensure(VertexPaintData data)
        {
            if (data == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();
            var currentPoseHash = skinnedRenderer == null ? 0 : ComputePoseHash(skinnedRenderer);
            var needsSnapshot = _topologyInvalidated
                                || _snapshot == null
                                || _sourceMesh != data.SourceMesh
                                || _sourceSignature != data.SourceSignature
                                || _skinnedRenderer != skinnedRenderer
                                || (skinnedRenderer != null && _skinnedPoseHash != currentPoseHash);
            if (needsSnapshot)
            {
                var identity = MeshAssetIdentity.GetStableIdentity(data.SourceMesh);
                var validation = VertexPaintValidator.Validate(data, identity);
                if (!validation.IsValid)
                {
                    return validation;
                }

                var snapshotMesh = data.SourceMesh;
                if (skinnedRenderer != null)
                {
                    if (_bakedSkinnedMesh == null)
                    {
                        _bakedSkinnedMesh = new Mesh
                        {
                            name = "SimpleBlend Skinned Paint Snapshot",
                            hideFlags = HideFlags.HideAndDontSave
                        };
                    }

                    skinnedRenderer.BakeMesh(_bakedSkinnedMesh, true);
                    if (_bakedSkinnedMesh.vertexCount != data.ColorCount)
                    {
                        return VertexPaintValidationResult.Invalid(
                            VertexPaintValidationFailure.GeneratedMeshMismatch,
                            "The current skinned pose no longer maps one-to-one to the signed source vertices.");
                    }

                    snapshotMesh = _bakedSkinnedMesh;
                }

                var snapshotResult = MeshPaintSnapshot.TryCreate(snapshotMesh, out var snapshot);
                if (!snapshotResult.IsValid)
                {
                    return snapshotResult;
                }

                _snapshot = snapshot;
                _sourceMesh = data.SourceMesh;
                _sourceSignature = data.SourceSignature;
                _skinnedRenderer = skinnedRenderer;
                _skinnedPoseHash = currentPoseHash;
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
            _densityValues = VertexDensityCalculator.Calculate(_worldVertices, _snapshot.Topology);
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

        private static int ComputePoseHash(SkinnedMeshRenderer renderer)
        {
            unchecked
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    return 0;
                }

                var hash = mesh.GetEntityId().GetHashCode();
                for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    hash = (hash * 397) ^ renderer.GetBlendShapeWeight(shapeIndex).GetHashCode();
                }

                var bones = renderer.bones;
                for (var boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    var bone = bones[boneIndex];
                    if (bone == null)
                    {
                        hash *= 397;
                        continue;
                    }

                    var matrix = bone.localToWorldMatrix;
                    for (var element = 0; element < 16; element++)
                    {
                        hash = (hash * 397) ^ matrix[element].GetHashCode();
                    }
                }

                return hash;
            }
        }
    }
}
