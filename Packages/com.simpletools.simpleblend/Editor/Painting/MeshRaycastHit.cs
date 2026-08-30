using UnityEngine;

namespace SimpleBlend.Editor
{
    internal readonly struct MeshRaycastHit
    {
        internal MeshRaycastHit(
            float distance,
            Vector3 point,
            Vector3 normal,
            Vector3 barycentricCoordinate,
            int triangleIndex,
            int submeshIndex)
        {
            Distance = distance;
            Point = point;
            Normal = normal;
            BarycentricCoordinate = barycentricCoordinate;
            TriangleIndex = triangleIndex;
            SubmeshIndex = submeshIndex;
        }

        internal float Distance { get; }

        internal Vector3 Point { get; }

        internal Vector3 Normal { get; }

        internal Vector3 BarycentricCoordinate { get; }

        internal int TriangleIndex { get; }

        internal int SubmeshIndex { get; }
    }
}
