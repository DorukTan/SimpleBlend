using System;
using UnityEngine;

namespace SimpleBlend
{
    [Serializable]
    internal readonly struct RedBrushCommand
    {
        private static readonly AnimationCurve DefaultFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        internal RedBrushCommand(
            Vector3 center,
            float radius,
            float strength,
            Color32 color,
            AnimationCurve falloff)
        {
            Center = center;
            Radius = Mathf.Max(0.0001f, radius);
            Strength = Mathf.Clamp01(strength);
            Color = color;
            Falloff = falloff ?? DefaultFalloff;
        }

        internal Vector3 Center { get; }

        internal float Radius { get; }

        internal float Strength { get; }

        internal Color32 Color { get; }

        internal AnimationCurve Falloff { get; }

        internal float EvaluateWeight(float distance)
        {
            if (distance < 0f || distance > Radius)
            {
                return 0f;
            }

            var normalizedDistance = Mathf.Clamp01(distance / Radius);
            return Mathf.Clamp01(Falloff.Evaluate(normalizedDistance) * Strength);
        }
    }
}
