using UnityEngine;

namespace SimpleBlend
{
    internal static class VertexSurfaceWeightUtility
    {
        internal static Color32 Blend(Color32 source, Color32 target, VertexPaintChannel channel,
            float influence, int channels, bool erase)
        {
            var from = Weights(source, channels);
            var to = Weights(target, channels);
            if (erase)
            {
                to = from;
                if (channel == VertexPaintChannel.Red || channel == VertexPaintChannel.RGB) to.x = 0;
                if (channel == VertexPaintChannel.Green || channel == VertexPaintChannel.RGB) to.y = 0;
                if (channel == VertexPaintChannel.Blue || channel == VertexPaintChannel.RGB) to.z = 0;
            }
            var mixed = Vector3.Lerp(from, to, Mathf.Clamp01(influence)) * 255;
            var red = Mathf.RoundToInt(mixed.x);
            var green = Mathf.RoundToInt(mixed.y);
            var blue = channels == 3 ? Mathf.RoundToInt(mixed.z) : 0;
            // Independent byte rounding must not manufacture negative Base weight.
            var excess = Mathf.Max(0, red + green + blue - 255);
            if (red >= green && red >= blue) red -= excess;
            else if (green >= blue) green -= excess;
            else blue -= excess;
            return new Color32((byte)red, (byte)green, channels == 3 ? (byte)blue : source.b, source.a);
        }

        private static Vector3 Weights(Color32 color, int channels)
        {
            var weights = new Vector3(color.r, color.g, channels == 3 ? color.b : 0) / 255;
            var total = weights.x + weights.y + weights.z;
            return total > 1 ? weights / total : weights;
        }
    }
}
