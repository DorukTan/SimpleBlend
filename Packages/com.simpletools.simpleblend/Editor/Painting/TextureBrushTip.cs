using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal enum TextureBrushTipKind { Round, Flat, Chalk, Custom }
    internal enum TextureBrushMaskMode { DarkPixels, Alpha }

    [Serializable]
    internal sealed class TextureBrushShape
    {
        [SerializeField] internal TextureBrushTipKind Tip;
        [SerializeField] internal Texture2D Texture;
        [SerializeField] internal TextureBrushMaskMode MaskMode;
        [SerializeField, Range(0, 1)] internal float Hardness;
        [SerializeField, Range(0.05f, 1)] internal float Roundness = 1;
        [SerializeField, Range(-180, 180)] internal float Angle;
        [SerializeField, Range(0.01f, 2)] internal float Spacing = 0.1f;
        [SerializeField, Range(0, 1)] internal float Smoothing;
        [SerializeField] internal bool LimitOpacity;

        internal TextureBrushShape Copy() => new TextureBrushShape
        {
            Tip = Tip, Texture = Texture, MaskMode = MaskMode,
            Hardness = Clamp(Hardness, 0, 1, 0), Roundness = Clamp(Roundness, 0.05f, 1, 1),
            Angle = Clamp(Angle, -180, 180, 0), Spacing = Clamp(Spacing, 0.01f, 2, 0.1f),
            Smoothing = Clamp(Smoothing, 0, 1, 0), LimitOpacity = LimitOpacity
        };

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }

    /// <summary>An immutable brush-tip snapshot shared by stroke painting and diagnostic previews.</summary>
    internal sealed class TextureBrushTip
    {
        private readonly TextureBrushShape _shape;
        private readonly float[] _mask;
        private readonly int _width, _height;
        private readonly float _cos, _sin;
        internal bool LimitOpacity => _shape.LimitOpacity;
        internal float Spacing => _shape.Spacing;
        internal float Smoothing => _shape.Smoothing;
        internal float BoundingRadius => _shape.Tip == TextureBrushTipKind.Flat || _shape.Tip == TextureBrushTipKind.Custom
            ? Mathf.Sqrt(1 + _shape.Roundness * _shape.Roundness) : 1;
        internal TextureBrushTip Reframe(TextureBrushShape shape) => new TextureBrushTip(shape, _mask, _width, _height);

        internal TextureBrushTip(TextureBrushShape shape, float[] mask = null, int width = 0, int height = 0)
        {
            _shape = (shape ?? new TextureBrushShape()).Copy();
            _mask = mask == null ? null : (float[])mask.Clone();
            _width = width; _height = height;
            _cos = Mathf.Cos(_shape.Angle * Mathf.Deg2Rad); _sin = Mathf.Sin(_shape.Angle * Mathf.Deg2Rad);
        }

        internal static TextureBrushTip Capture(TextureBrushShape shape)
        {
            shape = (shape ?? new TextureBrushShape()).Copy();
            if (shape.Tip != TextureBrushTipKind.Custom) return new TextureBrushTip(shape);
            if (shape.Texture == null || !AssetDatabase.Contains(shape.Texture))
                throw new InvalidOperationException("Choose a saved brush-tip texture, or use a built-in tip.");
            var width = Mathf.Min(256, shape.Texture.width);
            var height = Mathf.Max(1, Mathf.RoundToInt(width * (float)shape.Texture.height / shape.Texture.width));
            if (height > 256) { width = Mathf.Max(1, Mathf.RoundToInt(width * 256f / height)); height = 256; }
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(shape.Texture, rt);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0); readback.Apply(false, false);
                var colors = readback.GetPixels32();
                var mask = new float[colors.Length];
                for (var i = 0; i < colors.Length; i++)
                {
                    var color = colors[i];
                    mask[i] = color.a / 255f * (shape.MaskMode == TextureBrushMaskMode.Alpha ? 1
                        : 1 - (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f);
                }
                return new TextureBrushTip(shape, mask, width, height);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        internal float Evaluate(Vector2 point, AnimationCurve falloff)
        {
            var x = _cos * point.x + _sin * point.y;
            var y = (-_sin * point.x + _cos * point.y) / _shape.Roundness;
            if (_shape.Tip == TextureBrushTipKind.Custom && _width > 0 && _height > 0)
            {
                if (_width > _height) y *= (float)_width / _height;
                else x *= (float)_height / _width;
            }
            var square = _shape.Tip == TextureBrushTipKind.Flat || _shape.Tip == TextureBrushTipKind.Custom;
            var distance = square ? Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) : Mathf.Sqrt(x * x + y * y);
            if (!float.IsFinite(distance) || distance > 1) return 0;
            var edge = _shape.Hardness >= 1 ? 0 : Mathf.Clamp01((distance - _shape.Hardness) / (1 - _shape.Hardness));
            var weight = falloff == null ? 1 - edge : Mathf.Clamp01(falloff.Evaluate(edge));
            if (_shape.Tip == TextureBrushTipKind.Chalk)
            {
                var noise = Grain(Mathf.FloorToInt((x + 1) * 32), Mathf.FloorToInt((y + 1) * 32));
                weight *= noise < 0.3f ? 0.05f : noise;
            }
            if (_shape.Tip == TextureBrushTipKind.Custom)
            {
                if (_mask == null || _width < 1 || _height < 1 || _mask.Length != _width * _height) return 0;
                var px = Mathf.Clamp01(x * 0.5f + 0.5f) * (_width - 1);
                var py = Mathf.Clamp01(y * 0.5f + 0.5f) * (_height - 1);
                var ix = Mathf.FloorToInt(px); var iy = Mathf.FloorToInt(py);
                var nx = Mathf.Min(ix + 1, _width - 1); var ny = Mathf.Min(iy + 1, _height - 1);
                weight *= Mathf.Lerp(Mathf.Lerp(_mask[iy * _width + ix], _mask[iy * _width + nx], px - ix),
                    Mathf.Lerp(_mask[ny * _width + ix], _mask[ny * _width + nx], px - ix), py - iy);
            }
            return float.IsFinite(weight) ? Mathf.Clamp01(weight) : 0;
        }

        internal Vector2 Boundary(float radians)
        {
            var x = Mathf.Cos(radians); var y = Mathf.Sin(radians);
            if (_shape.Tip == TextureBrushTipKind.Flat || _shape.Tip == TextureBrushTipKind.Custom)
            { var extent = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)); x /= extent; y /= extent; }
            if (_shape.Tip == TextureBrushTipKind.Custom && _width > 0 && _height > 0)
            { if (_width > _height) y *= (float)_height / _width; else x *= (float)_width / _height; }
            y *= _shape.Roundness;
            return new Vector2(_cos * x - _sin * y, _sin * x + _cos * y);
        }

        internal static void Frame(Vector3 normal, Vector3 preferredRight, out Vector3 right, out Vector3 up)
        {
            right = Vector3.ProjectOnPlane(preferredRight, normal).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right, normal).normalized;
            up = Vector3.Cross(right, normal).normalized;
        }

        private static float Grain(int x, int y)
        {
            unchecked
            {
                var hash = (uint)(x * 374761393 + y * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177;
                return (hash & 65535) / 65535f;
            }
        }
    }
}
