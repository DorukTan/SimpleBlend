using System;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal enum TextureWeatheringSource { Noise, MeshBorders, UvSeams }

    [Serializable]
    internal sealed class TextureWeatheringSettings
    {
        [SerializeField] internal TextureWeatheringSource Source;
        [SerializeField] internal int Seed = 1;
        [SerializeField] internal float NoiseScale = 4;
        [SerializeField] internal float Coverage = 0.5f;
        [SerializeField] internal float Width = 0.1f;
        [SerializeField] internal float Softness = 0.5f;
        [SerializeField] internal float Strength = 1;

        internal TextureWeatheringSettings Snapshot()
        {
            if (!Enum.IsDefined(typeof(TextureWeatheringSource), Source)
                || !float.IsFinite(NoiseScale) || NoiseScale <= 0 || NoiseScale > 1000
                || !Unit(Coverage) || !Unit(Softness) || !Unit(Strength) || !float.IsFinite(Width) || Width < 0 || Width > 1000000)
                throw new ArgumentException("Weathering requires finite settings: scale in (0, 1000], width in [0, 1000000] and coverage/softness/strength in [0, 1].");
            return (TextureWeatheringSettings)MemberwiseClone();
        }

        private static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
    }

    internal static class TextureWeatheringMask
    {
        internal static int NextSeed(int seed) => unchecked(seed + 0x6D2B79F5);

        internal static float Evaluate(Vector3 position, TextureWeatheringSettings settings, float edgeDistance)
        {
            if (settings.Coverage <= 0 || settings.Strength <= 0) return 0;
            var patch = settings.Coverage >= 1 ? 1 : Patches(Noise(position * settings.NoiseScale, settings.Seed), settings.Coverage, settings.Softness);
            if (settings.Source != TextureWeatheringSource.Noise)
            {
                if (settings.Width <= 0 || edgeDistance >= settings.Width) return 0;
                var inner = settings.Width * (1 - settings.Softness);
                patch *= edgeDistance <= inner ? 1 : 1 - Mathf.SmoothStep(0, 1, (edgeDistance - inner) / (settings.Width - inner));
            }
            return Mathf.Clamp01(patch * settings.Strength);
        }

        private static float Patches(float noise, float coverage, float softness)
        {
            var threshold = 1 - coverage;
            if (softness <= 0) return noise >= threshold ? 1 : 0;
            return Mathf.SmoothStep(0, 1, (noise - threshold) / softness + 0.5f);
        }

        internal static float Noise(Vector3 point, int seed)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)
                || Mathf.Abs(point.x) > 100000000 || Mathf.Abs(point.y) > 100000000 || Mathf.Abs(point.z) > 100000000)
                throw new ArgumentException("Weathering coordinates exceed the supported finite noise range. Reduce noise scale or mesh coordinates.");
            var x = Mathf.FloorToInt(point.x); var y = Mathf.FloorToInt(point.y); var z = Mathf.FloorToInt(point.z);
            var dx = Fade(point.x - x); var dy = Fade(point.y - y); var dz = Fade(point.z - z);
            var lower = Mathf.Lerp(Mathf.Lerp(Hash(x, y, z, seed), Hash(x + 1, y, z, seed), dx),
                Mathf.Lerp(Hash(x, y + 1, z, seed), Hash(x + 1, y + 1, z, seed), dx), dy);
            var upper = Mathf.Lerp(Mathf.Lerp(Hash(x, y, z + 1, seed), Hash(x + 1, y, z + 1, seed), dx),
                Mathf.Lerp(Hash(x, y + 1, z + 1, seed), Hash(x + 1, y + 1, z + 1, seed), dx), dy);
            return Mathf.Lerp(lower, upper, dz);
        }

        private static float Fade(float value) => value * value * value * (value * (value * 6 - 15) + 10);

        private static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                var hash = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00ffffff) / 16777215f;
            }
        }
    }
}
