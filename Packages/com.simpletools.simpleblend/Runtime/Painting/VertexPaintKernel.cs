using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend
{
    internal static class VertexPaintKernel
    {
        internal static int Apply(
            VertexBrushCommand command,
            IReadOnlyList<Vector3> worldPositions,
            IReadOnlyList<Vector3> worldNormals,
            IReadOnlyList<int> candidateIndices,
            VertexPaintTopology topology,
            Color32[] colors,
            bool uniformWeight = false,
            Color32[] strokeBaseline = null,
            float[] strongestStrokeWeights = null,
            ICollection<int> changedVertices = null)
        {
            ValidateInputs(
                worldPositions,
                worldNormals,
                candidateIndices,
                topology,
                colors,
                strokeBaseline,
                strongestStrokeWeights);

            if (command.Mode == VertexPaintBrushMode.Sample)
            {
                return 0;
            }

            var smoothingSource = command.Mode == VertexPaintBrushMode.Smooth
                ? strokeBaseline ?? (Color32[])colors.Clone()
                : null;
            var changedCount = 0;
            for (var candidateIndex = 0; candidateIndex < candidateIndices.Count; candidateIndex++)
            {
                var vertexIndex = candidateIndices[candidateIndex];
                ValidateVertex(vertexIndex, colors.Length);
                var existingForMask = strokeBaseline == null
                    ? colors[vertexIndex]
                    : strokeBaseline[vertexIndex];
                if (!command.Mask.Allows(
                        vertexIndex,
                        worldNormals[vertexIndex],
                        existingForMask,
                        topology))
                {
                    continue;
                }

                var weight = uniformWeight
                    ? command.UniformWeight
                    : command.EvaluateWeight(Vector3.Distance(worldPositions[vertexIndex], command.Center));
                if (weight <= 0f)
                {
                    continue;
                }

                if (strongestStrokeWeights != null)
                {
                    if (weight <= strongestStrokeWeights[vertexIndex])
                    {
                        continue;
                    }

                    strongestStrokeWeights[vertexIndex] = weight;
                }

                var source = strokeBaseline == null ? colors[vertexIndex] : strokeBaseline[vertexIndex];
                if (!TryGetTarget(command, vertexIndex, topology, smoothingSource, out var target))
                {
                    continue;
                }

                var result = command.SurfaceWeightChannels > 0
                    ? VertexSurfaceWeightUtility.Blend(source, target, command.Channel, weight,
                        command.SurfaceWeightChannels, command.Mode == VertexPaintBrushMode.Erase)
                    : VertexColorChannelUtility.BlendSelected(
                    source,
                    target,
                    command.Channel,
                    weight);
                if (result.Equals(colors[vertexIndex]))
                {
                    continue;
                }

                colors[vertexIndex] = result;
                changedVertices?.Add(vertexIndex);
                changedCount++;
            }

            return changedCount;
        }

        private static bool TryGetTarget(
            VertexBrushCommand command,
            int vertexIndex,
            VertexPaintTopology topology,
            IReadOnlyList<Color32> smoothingSource,
            out Color32 target)
        {
            switch (command.Mode)
            {
                case VertexPaintBrushMode.Paint:
                case VertexPaintBrushMode.Fill:
                    target = command.Color;
                    return true;
                case VertexPaintBrushMode.Erase:
                    target = new Color32(0, 0, 0, 0);
                    return true;
                case VertexPaintBrushMode.Smooth:
                    return TryAverageNeighbors(vertexIndex, topology, smoothingSource, out target);
                default:
                    target = default;
                    return false;
            }
        }

        private static bool TryAverageNeighbors(
            int vertexIndex,
            VertexPaintTopology topology,
            IReadOnlyList<Color32> source,
            out Color32 average)
        {
            var neighbors = topology.GetNeighbors(vertexIndex);
            if (neighbors.Count == 0)
            {
                average = default;
                return false;
            }

            var red = 0;
            var green = 0;
            var blue = 0;
            var alpha = 0;
            for (var index = 0; index < neighbors.Count; index++)
            {
                var color = source[neighbors[index]];
                red += color.r;
                green += color.g;
                blue += color.b;
                alpha += color.a;
            }

            average = new Color32(
                RoundAverage(red, neighbors.Count),
                RoundAverage(green, neighbors.Count),
                RoundAverage(blue, neighbors.Count),
                RoundAverage(alpha, neighbors.Count));
            return true;
        }

        private static byte RoundAverage(int sum, int count)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt((float)sum / count), byte.MinValue, byte.MaxValue);
        }

        private static void ValidateInputs(
            IReadOnlyList<Vector3> worldPositions,
            IReadOnlyList<Vector3> worldNormals,
            IReadOnlyList<int> candidateIndices,
            VertexPaintTopology topology,
            Color32[] colors,
            Color32[] strokeBaseline,
            float[] strongestStrokeWeights)
        {
            if (worldPositions == null)
            {
                throw new ArgumentNullException(nameof(worldPositions));
            }

            if (worldNormals == null)
            {
                throw new ArgumentNullException(nameof(worldNormals));
            }

            if (candidateIndices == null)
            {
                throw new ArgumentNullException(nameof(candidateIndices));
            }

            if (topology == null)
            {
                throw new ArgumentNullException(nameof(topology));
            }

            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (worldPositions.Count != colors.Length
                || worldNormals.Count != colors.Length
                || topology.VertexCount != colors.Length)
            {
                throw new ArgumentException("Positions, normals, topology, and colors must have matching counts.");
            }

            if (strokeBaseline != null && strokeBaseline.Length != colors.Length)
            {
                throw new ArgumentException("Stroke baseline and color counts must match.", nameof(strokeBaseline));
            }

            if (strongestStrokeWeights != null && strongestStrokeWeights.Length != colors.Length)
            {
                throw new ArgumentException(
                    "Stroke-weight and color counts must match.",
                    nameof(strongestStrokeWeights));
            }

            if ((strokeBaseline == null) != (strongestStrokeWeights == null))
            {
                throw new ArgumentException(
                    "Non-accumulating strokes require both a baseline and strongest-weight buffer.");
            }
        }

        private static void ValidateVertex(int vertexIndex, int vertexCount)
        {
            if (vertexIndex < 0 || vertexIndex >= vertexCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexIndex),
                    $"Vertex index {vertexIndex} is outside 0..{vertexCount - 1}.");
            }
        }
    }
}
