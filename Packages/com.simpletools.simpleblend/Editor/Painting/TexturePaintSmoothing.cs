using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed partial class TexturePaintStroke
    {
        private bool _smooth;
        private readonly Dictionary<long, WeightAverage> _smoothTargets = new Dictionary<long, WeightAverage>();
        private readonly HashSet<int> _smoothSources = new HashSet<int>();

        private long SmoothGroup(int index, Vector3 normal) => ((long)_atlas.ComponentAt(index) << 1)
            | (Vector3.Dot(_atlas.WorldNormal(index), normal) < 0 ? 1L : 0L);

        private void BuildSmoothTargets(Vector3 center, Vector3 normal, Vector3 right, Vector3 up)
        {
            _smoothTargets.Clear(); _smoothSources.Clear();
            foreach (var index in _candidates)
            {
                if (!TryInfluence(index, center, normal, right, up, false, out var influence)) continue;
                var source = _atlas.Coverage[_atlas.Pixel(index)];
                if (!_smoothSources.Add(source)) continue;
                var key = SmoothGroup(index, normal);
                _smoothTargets.TryGetValue(key, out var average);
                // Integer sums make the average independent of grid traversal and duplicated gutters.
                var weight = Mathf.Clamp(Mathf.RoundToInt(influence / (_command.Strength * _command.Flow) * 65535), 1, 65535);
                var color = VertexSurfaceWeightUtility.Blend(new Color32(), _baseline[source], VertexPaintChannel.RGB, 1,
                    _paint.WeightChannels, false);
                average.Add(color, weight, _paint.WeightChannels);
                _smoothTargets[key] = average;
            }
        }

        private struct WeightAverage
        {
            private long _red, _green, _blue, _weight;

            internal void Add(Color32 color, int weight, int channels)
            {
                _red += (long)color.r * weight; _green += (long)color.g * weight;
                if (channels == 3) _blue += (long)color.b * weight;
                _weight += weight;
            }

            internal Color32 Color(int channels)
            {
                var red = (int)((_red + _weight / 2) / _weight);
                var green = (int)((_green + _weight / 2) / _weight);
                var blue = channels == 3 ? (int)((_blue + _weight / 2) / _weight) : 0;
                var excess = Mathf.Max(0, red + green + blue - 255);
                if (red >= green && red >= blue) red -= excess;
                else if (green >= blue) green -= excess;
                else blue -= excess;
                return new Color32((byte)red, (byte)green, (byte)blue, 0);
            }
        }
    }
}
