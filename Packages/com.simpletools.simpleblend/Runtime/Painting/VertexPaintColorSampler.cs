using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend
{
    internal static class VertexPaintColorSampler
    {
        internal static Color32 SampleTriangle(
            IReadOnlyList<Color32> colors,
            IReadOnlyList<int> triangles,
            int triangleIndex,
            Vector3 barycentricCoordinate)
        {
            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            var offset = triangleIndex * 3;
            if (triangleIndex < 0 || offset + 2 >= triangles.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(triangleIndex));
            }

            var first = colors[triangles[offset]];
            var second = colors[triangles[offset + 1]];
            var third = colors[triangles[offset + 2]];
            var weights = NormalizeBarycentric(barycentricCoordinate);
            return new Color32(
                Interpolate(first.r, second.r, third.r, weights),
                Interpolate(first.g, second.g, third.g, weights),
                Interpolate(first.b, second.b, third.b, weights),
                Interpolate(first.a, second.a, third.a, weights));
        }

        private static Vector3 NormalizeBarycentric(Vector3 value)
        {
            value.x = Mathf.Max(0f, value.x);
            value.y = Mathf.Max(0f, value.y);
            value.z = Mathf.Max(0f, value.z);
            var sum = value.x + value.y + value.z;
            return sum <= 0.000001f ? new Vector3(1f, 0f, 0f) : value / sum;
        }

        private static byte Interpolate(byte first, byte second, byte third, Vector3 weights)
        {
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt(first * weights.x + second * weights.y + third * weights.z),
                byte.MinValue,
                byte.MaxValue);
        }
    }
}
