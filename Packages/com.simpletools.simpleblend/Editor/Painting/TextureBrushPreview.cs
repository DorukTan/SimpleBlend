using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class TextureBrushTipCache
    {
        private string _key;
        private TextureBrushTip _tip;
        private string _error;
        private string _sourceKey;
        private bool _customSource;

        internal bool TryGet(TextureBrushShape shape, out TextureBrushTip tip, out string error)
        {
            var path = shape.Texture == null ? string.Empty : AssetDatabase.GetAssetPath(shape.Texture);
            var sourceKey = path + shape.MaskMode + (string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.GetAssetDependencyHash(path).ToString());
            var key = JsonUtility.ToJson(shape) + sourceKey;
            if (key != _key)
            {
                _key = key;
                try
                {
                    _tip = _tip != null && _customSource && shape.Tip == TextureBrushTipKind.Custom && _sourceKey == sourceKey
                        ? _tip.Reframe(shape) : TextureBrushTip.Capture(shape);
                    _sourceKey = sourceKey;
                    _customSource = shape.Tip == TextureBrushTipKind.Custom;
                    _error = null;
                }
                catch (Exception exception) { _tip = null; _error = exception.Message; }
            }
            tip = _tip; error = _error;
            return _tip != null;
        }
    }

    internal sealed class TextureBrushPreview : IDisposable
    {
        private readonly TextureBrushTipCache _cache = new TextureBrushTipCache();
        private Texture2D _texture;
        private string _key;

        internal void Draw(SimpleBlendBrushSettings settings)
        {
            if (!_cache.TryGet(settings.TextureBrush, out var tip, out var error))
            { EditorGUILayout.HelpBox(error, MessageType.Warning); return; }
            var key = EditorJsonUtility.ToJson(settings) + EditorGUIUtility.isProSkin + tip.GetHashCode();
            if (key != _key || _texture == null)
            {
                _key = key;
                const int width = 256, height = 48;
                if (_texture == null) _texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { name = "SimpleBlend Brush Stroke Preview", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                var colors = new Color32[width * height];
                var coverage = new float[colors.Length];
                const float radius = 12;
                var step = Mathf.Max(0.1f, radius * 2 * tip.Spacing);
                var shade = (byte)(EditorGUIUtility.isProSkin ? 220 : 45);
                for (var dab = 18f; dab <= width - 18; dab += step)
                {
                    var centerY = height * 0.5f + Mathf.Sin(dab / width * Mathf.PI * 2) * 5;
                    var extent = radius * tip.BoundingRadius;
                    for (var y = Mathf.Max(0, Mathf.FloorToInt(centerY - extent)); y <= Mathf.Min(height - 1, Mathf.CeilToInt(centerY + extent)); y++)
                    for (var x = Mathf.Max(0, Mathf.FloorToInt(dab - extent)); x <= Mathf.Min(width - 1, Mathf.CeilToInt(dab + extent)); x++)
                    {
                        var ink = tip.Evaluate(new Vector2(x - dab, y - centerY) / radius, settings.Falloff) * settings.Flow;
                        if (!tip.LimitOpacity) ink *= settings.Strength;
                        var i = y * width + x;
                        coverage[i] = settings.Accumulate ? 1 - (1 - coverage[i]) * (1 - ink) : Mathf.Max(coverage[i], ink);
                    }
                }
                for (var i = 0; i < colors.Length; i++) colors[i] = new Color32(shade, shade, shade,
                    (byte)Mathf.RoundToInt(coverage[i] * (tip.LimitOpacity ? settings.Strength : 1) * 255));
                _texture.SetPixels32(colors); _texture.Apply(false, false);
            }
            var rect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUI.DrawTexture(rect, _texture, ScaleMode.ScaleToFit, true);
            EditorGUILayout.LabelField("Tip/stroke preview · not a material preview", EditorStyles.miniLabel);
        }

        public void Dispose()
        {
            if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null; _key = null;
        }
    }
}
