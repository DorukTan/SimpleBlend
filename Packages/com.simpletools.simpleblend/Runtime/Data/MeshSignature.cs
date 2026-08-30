using System;
using UnityEngine;

namespace SimpleBlend
{
    [Serializable]
    public struct MeshSignature : IEquatable<MeshSignature>
    {
        [SerializeField] private string _sourceIdentity;
        [SerializeField] private int _vertexCount;
        [SerializeField] private MeshTopology[] _subMeshTopologies;
        [SerializeField] private int[] _subMeshIndexCounts;
        [SerializeField] private long _topologyHash;

        internal MeshSignature(
            string sourceIdentity,
            int vertexCount,
            MeshTopology[] subMeshTopologies,
            int[] subMeshIndexCounts,
            long topologyHash)
        {
            _sourceIdentity = sourceIdentity ?? string.Empty;
            _vertexCount = vertexCount;
            _subMeshTopologies = subMeshTopologies;
            _subMeshIndexCounts = subMeshIndexCounts;
            _topologyHash = topologyHash;
        }

        public string SourceIdentity => _sourceIdentity ?? string.Empty;

        public int VertexCount => _vertexCount;

        public int SubMeshCount => _subMeshTopologies?.Length ?? 0;

        public long TopologyHash => _topologyHash;

        public bool IsCreated => _vertexCount >= 0
                                 && _subMeshTopologies != null
                                 && _subMeshIndexCounts != null
                                 && _subMeshTopologies.Length == _subMeshIndexCounts.Length;

        public MeshTopology GetSubMeshTopology(int index)
        {
            return _subMeshTopologies[index];
        }

        public int GetSubMeshIndexCount(int index)
        {
            return _subMeshIndexCounts[index];
        }

        public VertexPaintValidationFailure Compare(MeshSignature other)
        {
            if (!IsCreated || !other.IsCreated)
            {
                return VertexPaintValidationFailure.MissingSignature;
            }

            if (!string.Equals(SourceIdentity, other.SourceIdentity, StringComparison.Ordinal))
            {
                return VertexPaintValidationFailure.SourceIdentityMismatch;
            }

            if (_vertexCount != other._vertexCount)
            {
                return VertexPaintValidationFailure.VertexCountMismatch;
            }

            if (_subMeshTopologies.Length != other._subMeshTopologies.Length)
            {
                return VertexPaintValidationFailure.SubMeshLayoutMismatch;
            }

            for (var i = 0; i < _subMeshTopologies.Length; i++)
            {
                if (_subMeshTopologies[i] != other._subMeshTopologies[i]
                    || _subMeshIndexCounts[i] != other._subMeshIndexCounts[i])
                {
                    return VertexPaintValidationFailure.SubMeshLayoutMismatch;
                }
            }

            return _topologyHash == other._topologyHash
                ? VertexPaintValidationFailure.None
                : VertexPaintValidationFailure.TopologyMismatch;
        }

        public bool Equals(MeshSignature other)
        {
            return Compare(other) == VertexPaintValidationFailure.None;
        }

        public override bool Equals(object obj)
        {
            return obj is MeshSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SourceIdentity.GetHashCode();
                hashCode = (hashCode * 397) ^ _vertexCount;
                hashCode = (hashCode * 397) ^ _topologyHash.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(MeshSignature left, MeshSignature right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MeshSignature left, MeshSignature right)
        {
            return !left.Equals(right);
        }
    }
}
