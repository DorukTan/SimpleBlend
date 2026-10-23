using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class MeshTriangleRaycaster
    {
        private const float Epsilon = 0.000001f;

        internal static bool TryRaycast(
            Ray worldRay,
            IReadOnlyList<Vector3> worldVertices,
            IReadOnlyList<Vector3> worldNormals,
            IReadOnlyList<int> triangles,
            IReadOnlyList<int> triangleSubmeshIds,
            out MeshRaycastHit hit)
        {
            hit = default;
            if (worldVertices == null
                || worldNormals == null
                || triangles == null
                || triangleSubmeshIds == null
                || worldVertices.Count != worldNormals.Count
                || triangles.Count / 3 != triangleSubmeshIds.Count)
            {
                return false;
            }

            var direction = worldRay.direction.normalized;
            if (direction.sqrMagnitude < Epsilon)
            {
                return false;
            }

            var nearestDistance = float.PositiveInfinity;
            var foundHit = false;
            for (var triangleIndex = 0; triangleIndex < triangles.Count / 3; triangleIndex++)
            {
                var offset = triangleIndex * 3;
                var firstIndex = triangles[offset];
                var secondIndex = triangles[offset + 1];
                var thirdIndex = triangles[offset + 2];
                var first = worldVertices[firstIndex];
                var second = worldVertices[secondIndex];
                var third = worldVertices[thirdIndex];

                if (!TryIntersect(
                        worldRay.origin,
                        direction,
                        first,
                        second,
                        third,
                        out var distance,
                        out var barycentric)
                    || distance >= nearestDistance)
                {
                    continue;
                }

                var normal = worldNormals[firstIndex] * barycentric.x
                             + worldNormals[secondIndex] * barycentric.y
                             + worldNormals[thirdIndex] * barycentric.z;
                if (normal.sqrMagnitude < Epsilon)
                {
                    normal = Vector3.Cross(second - first, third - first);
                }

                normal.Normalize();
                if (Vector3.Dot(normal, direction) > 0f)
                {
                    normal = -normal;
                }

                nearestDistance = distance;
                foundHit = true;
                hit = new MeshRaycastHit(
                    distance,
                    worldRay.origin + direction * distance,
                    normal,
                    barycentric,
                    triangleIndex,
                    triangleSubmeshIds[triangleIndex]);
            }

            return foundHit;
        }

        internal static bool TryIntersect(
            Vector3 rayOrigin,
            Vector3 rayDirection,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            out float distance,
            out Vector3 barycentric)
        {
            distance = 0f;
            barycentric = default;
            var edgeOne = second - first;
            var edgeTwo = third - first;
            var cross = Vector3.Cross(rayDirection, edgeTwo);
            var determinant = Vector3.Dot(edgeOne, cross);
            if (Mathf.Abs(determinant) < Epsilon)
            {
                return false;
            }

            var inverseDeterminant = 1f / determinant;
            var originOffset = rayOrigin - first;
            var secondWeight = Vector3.Dot(originOffset, cross) * inverseDeterminant;
            if (secondWeight < 0f || secondWeight > 1f)
            {
                return false;
            }

            var crossOffset = Vector3.Cross(originOffset, edgeOne);
            var thirdWeight = Vector3.Dot(rayDirection, crossOffset) * inverseDeterminant;
            if (thirdWeight < 0f || secondWeight + thirdWeight > 1f)
            {
                return false;
            }

            distance = Vector3.Dot(edgeTwo, crossOffset) * inverseDeterminant;
            if (distance < 0f)
            {
                return false;
            }

            barycentric = new Vector3(1f - secondWeight - thirdWeight, secondWeight, thirdWeight);
            return true;
        }
    }
}
