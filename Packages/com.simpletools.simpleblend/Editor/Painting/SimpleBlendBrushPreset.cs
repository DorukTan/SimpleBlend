using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [CreateAssetMenu(menuName = "SimpleBlend/Brush Preset", fileName = "BrushPreset")]
    public sealed class SimpleBlendBrushPreset : ScriptableObject
    {
        [SerializeField] private float _radius = 0.15f;
        [SerializeField, Range(0, 1)] private float _strength = 0.6f;
        [SerializeField, Range(0, 1)] private float _flow = 1;
        [SerializeField] private bool _buildUp;
        [SerializeField] private AnimationCurve _falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        internal static readonly string[] BuiltInNames = { "Choose a preset…", "Soft brush", "Hard brush", "Wash" };

        internal void Apply(SimpleBlendBrushSettings settings)
        {
            settings.Radius = FiniteClamp(_radius, 0.001f, 10, 0.15f);
            settings.Strength = FiniteClamp(_strength, 0, 1, 0.6f);
            settings.Flow = FiniteClamp(_flow, 0, 1, 1);
            settings.Accumulate = _buildUp;
            settings.Falloff = CopyCurve(_falloff);
        }

        internal void Capture(SimpleBlendBrushSettings settings)
        {
            _radius = settings.Radius;
            _strength = settings.Strength;
            _flow = settings.Flow;
            _buildUp = settings.Accumulate;
            _falloff = CopyCurve(settings.Falloff);
        }

        internal static void ApplyBuiltIn(int index, SimpleBlendBrushSettings settings)
        {
            if (index < 1 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
            settings.Radius = index == 3 ? 0.5f : 0.15f;
            settings.Strength = index == 2 ? 1 : index == 3 ? 0.15f : 0.6f;
            settings.Flow = index == 3 ? 0.5f : 1;
            settings.Accumulate = index == 3;
            settings.Falloff = index == 2 ? AnimationCurve.Constant(0, 1, 1) : AnimationCurve.EaseInOut(0, 1, 1, 0);
        }

        internal static SimpleBlendBrushPreset SaveNew(string path, SimpleBlendBrushSettings settings)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || !AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'))
                || System.IO.File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("Choose a new preset asset path inside Assets. Existing presets are never overwritten.");
            var preset = CreateInstance<SimpleBlendBrushPreset>();
            preset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            preset.Capture(settings);
            AssetDatabase.CreateAsset(preset, path);
            if (AssetDatabase.LoadMainAssetAtPath(path) != preset)
            {
                DestroyImmediate(preset);
                throw new InvalidOperationException("The brush preset could not be saved.");
            }
            AssetDatabase.SaveAssetIfDirty(preset);
            return preset;
        }

        private static AnimationCurve CopyCurve(AnimationCurve curve)
        {
            if (curve == null) return AnimationCurve.EaseInOut(0, 1, 1, 0);
            return new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        }

        private static float FiniteClamp(float value, float min, float max, float fallback)
            => float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }
}
