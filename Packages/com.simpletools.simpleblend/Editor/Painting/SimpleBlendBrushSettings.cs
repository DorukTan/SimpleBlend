using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [FilePath("SimpleBlend/BrushSettings.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal sealed class SimpleBlendBrushSettings : ScriptableSingleton<SimpleBlendBrushSettings>
    {
        internal static event System.Action Changed;
        [SerializeField] private VertexPaintBrushMode _mode = VertexPaintBrushMode.Paint;
        [SerializeField] private VertexPaintChannel _channel = VertexPaintChannel.Red;
        [SerializeField] private VertexPaintPreviewMode _previewMode = VertexPaintPreviewMode.Material;
        [SerializeField] private bool _previewSectionOnly;
        [SerializeField] private VertexPaintFillScope _fillScope = VertexPaintFillScope.WholeMesh;
        [SerializeField] private float _radius = 1.25f;
        [SerializeField] private float _strength = 1f;
        [SerializeField] private float _adjustmentSensitivity = 1f;
        [SerializeField] private float _flow = 1f;
        [SerializeField] private bool _accumulate = true;
        [SerializeField] private Color _color = Color.red;
        [SerializeField] private AnimationCurve _falloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private bool _normalMaskEnabled;
        [SerializeField] private Vector3 _normalDirection = Vector3.up;
        [SerializeField] private float _normalAngle = 60f;
        [SerializeField] private bool _submeshMaskEnabled;
        [SerializeField] private int _submeshMaskIndex;
        [SerializeField] private bool _thresholdMaskEnabled;
        [SerializeField] private VertexPaintChannel _thresholdChannel = VertexPaintChannel.Red;
        [SerializeField] private Vector2 _thresholdRange = new Vector2(0f, 1f);
        [SerializeField] private bool _thresholdInclusive = true;

        internal VertexPaintBrushMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        internal VertexPaintChannel Channel
        {
            get => _channel;
            set => _channel = value;
        }

        internal VertexPaintPreviewMode PreviewMode
        {
            get => _previewMode;
            set => _previewMode = value;
        }

        internal VertexPaintFillScope FillScope
        {
            get => _fillScope;
            set => _fillScope = value;
        }

        internal bool PreviewSectionOnly
        {
            get => _previewSectionOnly;
            set => _previewSectionOnly = value;
        }

        internal float Radius
        {
            get => _radius;
            set => _radius = value;
        }

        internal float Strength
        {
            get => _strength;
            set => _strength = value;
        }

        internal float Flow
        {
            get => _flow;
            set => _flow = value;
        }

        internal float AdjustmentSensitivity
        {
            get => _adjustmentSensitivity;
            set => _adjustmentSensitivity = value;
        }

        internal bool Accumulate
        {
            get => _accumulate;
            set => _accumulate = value;
        }

        internal Color Color
        {
            get => _color;
            set => _color = value;
        }

        internal AnimationCurve Falloff
        {
            get => _falloff;
            set => _falloff = value;
        }

        internal bool NormalMaskEnabled
        {
            get => _normalMaskEnabled;
            set => _normalMaskEnabled = value;
        }

        internal Vector3 NormalDirection
        {
            get => _normalDirection;
            set => _normalDirection = value;
        }

        internal float NormalAngle
        {
            get => _normalAngle;
            set => _normalAngle = value;
        }

        internal bool SubmeshMaskEnabled
        {
            get => _submeshMaskEnabled;
            set => _submeshMaskEnabled = value;
        }

        internal int SubmeshMaskIndex
        {
            get => _submeshMaskIndex;
            set => _submeshMaskIndex = value;
        }

        internal bool ThresholdMaskEnabled
        {
            get => _thresholdMaskEnabled;
            set => _thresholdMaskEnabled = value;
        }

        internal VertexPaintChannel ThresholdChannel
        {
            get => _thresholdChannel;
            set => _thresholdChannel = value;
        }

        internal Vector2 ThresholdRange
        {
            get => _thresholdRange;
            set => _thresholdRange = value;
        }

        internal bool ThresholdInclusive
        {
            get => _thresholdInclusive;
            set => _thresholdInclusive = value;
        }

        internal VertexBrushCommand CreateCommand(Vector3 center, VertexPaintBrushMode? modeOverride = null)
        {
            Normalize();
            var mask = new VertexBrushMask(
                _normalMaskEnabled,
                _normalDirection,
                _normalAngle,
                _submeshMaskEnabled,
                _submeshMaskIndex,
                _thresholdMaskEnabled,
                _thresholdChannel,
                _thresholdRange.x,
                _thresholdRange.y,
                _thresholdInclusive);
            return new VertexBrushCommand(
                center,
                _radius,
                _strength,
                _flow,
                (Color32)_color,
                _falloff,
                modeOverride ?? _mode,
                _channel,
                _accumulate,
                mask);
        }

        internal void SetSampledColor(Color32 color)
        {
            _color = color;
            SaveSettings();
        }

        internal void SaveSettings()
        {
            Normalize();
            Save(true);
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        internal void Normalize()
        {
            _adjustmentSensitivity = BrushAdjustmentSession.NormalizeSensitivity(_adjustmentSensitivity);
            _radius = Mathf.Max(0.0001f, _radius);
            _strength = Mathf.Clamp01(_strength);
            _flow = Mathf.Clamp01(_flow);
            _normalDirection = _normalDirection.sqrMagnitude > 0.000001f
                ? _normalDirection.normalized
                : Vector3.up;
            _normalAngle = Mathf.Clamp(_normalAngle, 0f, 180f);
            _submeshMaskIndex = Mathf.Max(0, _submeshMaskIndex);
            _thresholdRange = new Vector2(
                Mathf.Clamp01(Mathf.Min(_thresholdRange.x, _thresholdRange.y)),
                Mathf.Clamp01(Mathf.Max(_thresholdRange.x, _thresholdRange.y)));
            _falloff = _falloff ?? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
    }
}
