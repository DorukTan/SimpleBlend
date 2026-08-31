using System;
using UnityEngine;

namespace SimpleBlend
{
    internal enum VertexPaintChannel
    {
        Red,
        Green,
        Blue,
        Alpha,
        RGB
    }

    internal enum VertexPaintBrushMode
    {
        Paint,
        Erase,
        Smooth,
        Fill,
        Sample
    }

    internal enum VertexPaintFillScope
    {
        WholeMesh,
        HitSubmesh
    }

    [Serializable]
    internal readonly struct VertexBrushMask
    {
        internal static readonly VertexBrushMask None = new VertexBrushMask(
            false,
            Vector3.up,
            180f,
            false,
            0,
            false,
            VertexPaintChannel.Red,
            0f,
            1f,
            true);

        internal VertexBrushMask(
            bool normalEnabled,
            Vector3 normalDirection,
            float normalAngle,
            bool submeshEnabled,
            int submeshIndex,
            bool thresholdEnabled,
            VertexPaintChannel thresholdChannel,
            float thresholdMinimum,
            float thresholdMaximum,
            bool thresholdInclusive)
        {
            NormalEnabled = normalEnabled;
            NormalDirection = normalDirection.sqrMagnitude > 0.000001f
                ? normalDirection.normalized
                : Vector3.up;
            NormalAngle = Mathf.Clamp(normalAngle, 0f, 180f);
            SubmeshEnabled = submeshEnabled;
            SubmeshIndex = Mathf.Max(0, submeshIndex);
            ThresholdEnabled = thresholdEnabled;
            ThresholdChannel = thresholdChannel;
            ThresholdMinimum = Mathf.Clamp01(Mathf.Min(thresholdMinimum, thresholdMaximum));
            ThresholdMaximum = Mathf.Clamp01(Mathf.Max(thresholdMinimum, thresholdMaximum));
            ThresholdInclusive = thresholdInclusive;
        }

        internal bool NormalEnabled { get; }

        internal Vector3 NormalDirection { get; }

        internal float NormalAngle { get; }

        internal bool SubmeshEnabled { get; }

        internal int SubmeshIndex { get; }

        internal bool ThresholdEnabled { get; }

        internal VertexPaintChannel ThresholdChannel { get; }

        internal float ThresholdMinimum { get; }

        internal float ThresholdMaximum { get; }

        internal bool ThresholdInclusive { get; }

        internal bool Allows(
            int vertexIndex,
            Vector3 worldNormal,
            Color32 existingColor,
            VertexPaintTopology topology)
        {
            if (NormalEnabled)
            {
                if (worldNormal.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                var minimumDot = Mathf.Cos(NormalAngle * Mathf.Deg2Rad);
                if (Vector3.Dot(worldNormal.normalized, NormalDirection) < minimumDot)
                {
                    return false;
                }
            }

            if (SubmeshEnabled
                && (topology == null || !topology.ContainsVertex(SubmeshIndex, vertexIndex)))
            {
                return false;
            }

            if (!ThresholdEnabled)
            {
                return true;
            }

            var value = VertexColorChannelUtility.ReadNormalized(existingColor, ThresholdChannel);
            return ThresholdInclusive
                ? value >= ThresholdMinimum && value <= ThresholdMaximum
                : value > ThresholdMinimum && value < ThresholdMaximum;
        }
    }

    [Serializable]
    internal readonly struct VertexBrushCommand
    {
        private static readonly AnimationCurve DefaultFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        internal VertexBrushCommand(
            Vector3 center,
            float radius,
            float strength,
            float flow,
            Color32 color,
            AnimationCurve falloff,
            VertexPaintBrushMode mode,
            VertexPaintChannel channel,
            bool accumulate,
            VertexBrushMask mask)
        {
            Center = center;
            Radius = Mathf.Max(0.0001f, radius);
            Strength = Mathf.Clamp01(strength);
            Flow = Mathf.Clamp01(flow);
            Color = color;
            Falloff = falloff ?? DefaultFalloff;
            Mode = mode;
            Channel = channel;
            Accumulate = accumulate;
            Mask = mask;
        }

        internal Vector3 Center { get; }

        internal float Radius { get; }

        internal float Strength { get; }

        internal float Flow { get; }

        internal Color32 Color { get; }

        internal AnimationCurve Falloff { get; }

        internal VertexPaintBrushMode Mode { get; }

        internal VertexPaintChannel Channel { get; }

        internal bool Accumulate { get; }

        internal VertexBrushMask Mask { get; }

        internal float UniformWeight => Strength * Flow;

        internal VertexBrushCommand WithCenter(Vector3 center)
        {
            return new VertexBrushCommand(
                center,
                Radius,
                Strength,
                Flow,
                Color,
                Falloff,
                Mode,
                Channel,
                Accumulate,
                Mask);
        }

        internal float EvaluateWeight(float distance)
        {
            if (distance < 0f || distance > Radius)
            {
                return 0f;
            }

            var normalizedDistance = Mathf.Clamp01(distance / Radius);
            return Mathf.Clamp01(Falloff.Evaluate(normalizedDistance) * UniformWeight);
        }
    }

    internal static class VertexColorChannelUtility
    {
        internal static float ReadNormalized(Color32 color, VertexPaintChannel channel)
        {
            switch (channel)
            {
                case VertexPaintChannel.Red:
                    return color.r / 255f;
                case VertexPaintChannel.Green:
                    return color.g / 255f;
                case VertexPaintChannel.Blue:
                    return color.b / 255f;
                case VertexPaintChannel.Alpha:
                    return color.a / 255f;
                case VertexPaintChannel.RGB:
                    return (color.r + color.g + color.b) / (3f * 255f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        internal static Color32 BlendSelected(
            Color32 source,
            Color32 target,
            VertexPaintChannel channel,
            float weight)
        {
            weight = Mathf.Clamp01(weight);
            switch (channel)
            {
                case VertexPaintChannel.Red:
                    source.r = BlendByte(source.r, target.r, weight);
                    break;
                case VertexPaintChannel.Green:
                    source.g = BlendByte(source.g, target.g, weight);
                    break;
                case VertexPaintChannel.Blue:
                    source.b = BlendByte(source.b, target.b, weight);
                    break;
                case VertexPaintChannel.Alpha:
                    source.a = BlendByte(source.a, target.a, weight);
                    break;
                case VertexPaintChannel.RGB:
                    source.r = BlendByte(source.r, target.r, weight);
                    source.g = BlendByte(source.g, target.g, weight);
                    source.b = BlendByte(source.b, target.b, weight);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }

            return source;
        }

        private static byte BlendByte(byte source, byte target, float weight)
        {
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(source, target, weight)),
                byte.MinValue,
                byte.MaxValue);
        }
    }
}
