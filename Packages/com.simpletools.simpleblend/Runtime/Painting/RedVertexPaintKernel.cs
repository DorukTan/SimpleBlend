using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend
{
    internal static class RedVertexPaintKernel
    {
        internal static int Apply(
            RedBrushCommand command,
            IReadOnlyList<Vector3> worldPositions,
            IReadOnlyList<int> candidateIndices,
            Color32[] colors)
        {
            if (worldPositions == null)
            {
                throw new ArgumentNullException(nameof(worldPositions));
            }

            if (candidateIndices == null)
            {
                throw new ArgumentNullException(nameof(candidateIndices));
            }

            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (worldPositions.Count != colors.Length)
            {
                throw new ArgumentException("World-position and color counts must match.", nameof(colors));
            }

            var changedCount = 0;
            for (var candidateIndex = 0; candidateIndex < candidateIndices.Count; candidateIndex++)
            {
                var vertexIndex = candidateIndices[candidateIndex];
                if (vertexIndex < 0 || vertexIndex >= colors.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(candidateIndices),
                        $"Vertex index {vertexIndex} is outside the color array.");
                }

                var distance = Vector3.Distance(worldPositions[vertexIndex], command.Center);
                var weight = command.EvaluateWeight(distance);
                if (weight <= 0f)
                {
                    continue;
                }

                var current = colors[vertexIndex];
                var paintedRed = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Lerp(current.r, command.Color.r, weight)),
                    byte.MinValue,
                    byte.MaxValue);
                if (paintedRed == current.r)
                {
                    continue;
                }

                current.r = paintedRed;
                colors[vertexIndex] = current;
                changedCount++;
            }

            return changedCount;
        }
    }
}
