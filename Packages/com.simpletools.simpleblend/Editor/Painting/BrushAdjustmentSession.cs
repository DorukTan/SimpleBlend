using UnityEngine;

namespace SimpleBlend.Editor
{
    internal enum BrushAdjustment { None, Radius, Strength }

    // Preview values stay outside saved preferences and the paint/Undo pipeline.
    internal sealed class BrushAdjustmentSession
    {
        internal BrushAdjustment Kind { get; private set; }
        internal bool IsActive => Kind != BrushAdjustment.None;
        internal float Value { get; private set; }
        private float _startX;
        private float _baseline;
        private float _sensitivity;

        internal bool Begin(BrushAdjustment kind, float pointerX, SimpleBlendBrushSettings settings)
        {
            if (IsActive || kind == BrushAdjustment.None || !Finite(pointerX)) return false;
            Kind = kind;
            _startX = pointerX;
            _sensitivity = NormalizeSensitivity(settings.AdjustmentSensitivity);
            var value = kind == BrushAdjustment.Radius ? settings.Radius : settings.Strength;
            if (!Finite(value)) value = kind == BrushAdjustment.Radius ? 1.25f : 1f;
            _baseline = Value = kind == BrushAdjustment.Radius
                ? Mathf.Clamp(value, 0.0001f, 10000f) : Mathf.Clamp01(value);
            return true;
        }

        internal void Update(float pointerX)
        {
            if (!IsActive || !Finite(pointerX)) return;
            var delta = (double)pointerX - _startX;
            Value = Kind == BrushAdjustment.Radius
                ? Mathf.Clamp(_baseline * Mathf.Pow(2f, (float)System.Math.Max(-30, System.Math.Min(30, delta * _sensitivity / 100))), 0.0001f, 10000f)
                : Mathf.Clamp01((float)(_baseline + delta * _sensitivity / 200));
        }

        // Caller persists only a changed, completed gesture. Cancellation has nothing to restore.
        internal bool Commit(SimpleBlendBrushSettings settings)
        {
            if (!IsActive) return false;
            var changed = Kind == BrushAdjustment.Radius ? settings.Radius != Value : settings.Strength != Value;
            if (changed)
            {
                if (Kind == BrushAdjustment.Radius) settings.Radius = Value;
                else settings.Strength = Value;
            }
            Cancel();
            return changed;
        }

        internal void Cancel() => Kind = BrushAdjustment.None;
        internal static float NormalizeSensitivity(float value) => Finite(value) ? Mathf.Clamp(value, 0.25f, 4f) : 1f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
