using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class TextureStrokeSampler
    {
        private readonly VertexStrokeSampler _spacing = new VertexStrokeSampler();
        private bool _started;
        private Vector3 _filtered, _lastRaw;

        internal void Reset() { _spacing.Reset(); _started = false; }

        internal void Append(Vector3 input, float radius, float spacing, float smoothing, List<Vector3> samples)
        {
            samples.Clear();
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y) || !float.IsFinite(input.z)) { Reset(); return; }
            if (!_started)
            { _filtered = input; _lastRaw = input; _started = true; }
            else
            {
                var delta = input - _filtered;
                var lag = radius * Mathf.Clamp01(smoothing) * 2;
                if (delta.magnitude > lag) _filtered = input - delta.normalized * lag;
                _lastRaw = input;
            }
            // The shared sampler's fixed interval is 0.2 * radius; convert diameter spacing to that input.
            _spacing.Append(_filtered, radius * Mathf.Clamp(spacing, 0.01f, 2) * 10, samples);
        }

        internal void CatchUp(float radius, float spacing, List<Vector3> samples)
        {
            samples.Clear();
            if (!_started) return;
            _spacing.Append(_lastRaw, radius * Mathf.Clamp(spacing, 0.01f, 2) * 10, samples);
        }
    }
}
