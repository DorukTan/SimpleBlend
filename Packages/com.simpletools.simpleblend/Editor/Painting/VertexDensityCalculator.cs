using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class VertexDensityCalculator
    {
        internal static float[] Calculate(
            IReadOnlyList<Vector3> worldPositions,
            VertexPaintTopology topology)
        {
            if (worldPositions == null)
            {
                throw new ArgumentNullException(nameof(worldPositions));
            }

            if (topology == null)
            {
                throw new ArgumentNullException(nameof(topology));
            }

            if (worldPositions.Count != topology.VertexCount)
            {
                throw new ArgumentException("World positions and topology must have matching counts.");
            }

            var averageEdgeLengths = new float[worldPositions.Count];
            var connectedLengths = new float[worldPositions.Count];
            var connectedCount = 0;
            for (var vertexIndex = 0; vertexIndex < worldPositions.Count; vertexIndex++)
            {
                var neighbors = topology.GetNeighbors(vertexIndex);
                var totalLength = 0f;
                var validNeighborCount = 0;
                for (var neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
                {
                    var length = Vector3.Distance(
                        worldPositions[vertexIndex],
                        worldPositions[neighbors[neighborIndex]]);
                    if (length <= 0.000001f)
                    {
                        continue;
                    }

                    totalLength += length;
                    validNeighborCount++;
                }

                if (validNeighborCount == 0)
                {
                    continue;
                }

                var average = totalLength / validNeighborCount;
                averageEdgeLengths[vertexIndex] = average;
                connectedLengths[connectedCount++] = average;
            }

            var density = new float[worldPositions.Count];
            if (connectedCount == 0)
            {
                return density;
            }

            Array.Sort(connectedLengths, 0, connectedCount);
            var lowerIndex = connectedCount < 10
                ? 0
                : Mathf.FloorToInt((connectedCount - 1) * 0.1f);
            var upperIndex = connectedCount < 10
                ? connectedCount - 1
                : Mathf.CeilToInt((connectedCount - 1) * 0.9f);
            var denseSpacing = connectedLengths[lowerIndex];
            var sparseSpacing = connectedLengths[upperIndex];
            var spacingRange = sparseSpacing - denseSpacing;

            for (var vertexIndex = 0; vertexIndex < density.Length; vertexIndex++)
            {
                var spacing = averageEdgeLengths[vertexIndex];
                if (spacing <= 0f)
                {
                    density[vertexIndex] = 0f;
                }
                else if (spacingRange <= 0.000001f)
                {
                    density[vertexIndex] = 0.5f;
                }
                else
                {
                    density[vertexIndex] = Mathf.Clamp01(
                        (sparseSpacing - spacing) / spacingRange);
                }
            }

            return density;
        }
    }
}
