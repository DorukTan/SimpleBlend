using System;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal enum VertexPaintPreviewMode
    {
        Red,
        Green,
        Blue,
        Alpha,
        RGB,
        Density,
        Material
    }

    internal static class VertexPaintPreviewUtility
    {
        internal static Color Evaluate(Color32 color, float density, VertexPaintPreviewMode mode)
        {
            var normalized = (Color)color;
            float intensity;
            Color result;
            switch (mode)
            {
                case VertexPaintPreviewMode.Material:
                    return Color.clear;
                case VertexPaintPreviewMode.Red:
                    intensity = normalized.r;
                    result = new Color(intensity, 0f, 0f, PreviewAlpha(intensity));
                    break;
                case VertexPaintPreviewMode.Green:
                    intensity = normalized.g;
                    result = new Color(0f, intensity, 0f, PreviewAlpha(intensity));
                    break;
                case VertexPaintPreviewMode.Blue:
                    intensity = normalized.b;
                    result = new Color(0f, 0f, intensity, PreviewAlpha(intensity));
                    break;
                case VertexPaintPreviewMode.Alpha:
                    intensity = normalized.a;
                    result = new Color(intensity, intensity, intensity, PreviewAlpha(intensity));
                    break;
                case VertexPaintPreviewMode.RGB:
                    intensity = Mathf.Max(normalized.r, Mathf.Max(normalized.g, normalized.b));
                    result = new Color(normalized.r, normalized.g, normalized.b, PreviewAlpha(intensity));
                    break;
                case VertexPaintPreviewMode.Density:
                    density = Mathf.Clamp01(density);
                    result = DensityColor(density);
                    result.a = 0.85f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            return result;
        }

        private static float PreviewAlpha(float intensity)
        {
            return 0.18f + Mathf.Clamp01(intensity) * 0.67f;
        }

        private static Color DensityColor(float density)
        {
            var cold = new Color(0.05f, 0.15f, 1f, 1f);
            var middle = new Color(0f, 1f, 0.35f, 1f);
            var hot = new Color(1f, 0.15f, 0f, 1f);
            return density < 0.5f
                ? Color.Lerp(cold, middle, density * 2f)
                : Color.Lerp(middle, hot, (density - 0.5f) * 2f);
        }
    }
}
