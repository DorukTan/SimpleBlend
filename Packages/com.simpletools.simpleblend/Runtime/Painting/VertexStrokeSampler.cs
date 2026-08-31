using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend
{
    internal sealed class VertexStrokeSampler
    {
        private const float SpacingRatio = 0.2f;
        private const float MinimumSpacing = 0.0001f;
        private bool _hasInput;
        private Vector3 _lastInput;
        private float _spacing;
        private float _distanceToNextSample;

        internal void Reset()
        {
            _hasInput = false;
            _lastInput = default;
            _spacing = 0f;
            _distanceToNextSample = 0f;
        }

        internal void Append(Vector3 input, float radius, List<Vector3> samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            samples.Clear();
            if (!_hasInput)
            {
                _hasInput = true;
                _lastInput = input;
                _spacing = Mathf.Max(MinimumSpacing, radius * SpacingRatio);
                _distanceToNextSample = _spacing;
                samples.Add(input);
                return;
            }

            var segmentStart = _lastInput;
            var segment = input - segmentStart;
            var segmentLength = segment.magnitude;
            while (segmentLength + 0.000001f >= _distanceToNextSample)
            {
                var ratio = segmentLength <= 0.000001f
                    ? 0f
                    : _distanceToNextSample / segmentLength;
                segmentStart = Vector3.LerpUnclamped(segmentStart, input, ratio);
                samples.Add(segmentStart);
                segment = input - segmentStart;
                segmentLength = segment.magnitude;
                _distanceToNextSample = _spacing;
            }

            _distanceToNextSample -= segmentLength;
            _lastInput = input;
        }
    }
}
