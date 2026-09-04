using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleBlend.Editor
{
    [Overlay(typeof(SceneView), OverlayId, "SimpleBlend Brush", defaultDisplay = true)]
    internal sealed class SimpleBlendBrushOverlay : Overlay
    {
        internal const string OverlayId = "SimpleBlend.BrushOverlay";

        public override VisualElement CreatePanelContent()
        {
            return new IMGUIContainer(DrawSettings)
            {
                name = "SimpleBlend Brush Settings"
            };
        }

        private static void DrawSettings()
        {
            var settings = SimpleBlendBrushSettings.instance;
            EditorGUI.BeginChangeCheck();
            settings.Mode = (VertexPaintBrushMode)EditorGUILayout.EnumPopup("Mode", settings.Mode);
            settings.Channel = (VertexPaintChannel)EditorGUILayout.EnumPopup("Channel", settings.Channel);
            settings.PreviewMode = (VertexPaintPreviewMode)EditorGUILayout.EnumPopup(
                "Preview",
                settings.PreviewMode);

            if (settings.Mode == VertexPaintBrushMode.Paint
                || settings.Mode == VertexPaintBrushMode.Fill
                || settings.Mode == VertexPaintBrushMode.Sample)
            {
                settings.Color = EditorGUILayout.ColorField("Color", settings.Color);
            }

            if (settings.Mode == VertexPaintBrushMode.Fill)
            {
                settings.FillScope = (VertexPaintFillScope)EditorGUILayout.EnumPopup(
                    "Fill Scope",
                    settings.FillScope);
            }
            else if (settings.Mode != VertexPaintBrushMode.Sample)
            {
                settings.Radius = EditorGUILayout.FloatField("Radius", settings.Radius);
                settings.Falloff = EditorGUILayout.CurveField("Falloff", settings.Falloff);
                settings.Accumulate = EditorGUILayout.Toggle("Accumulate", settings.Accumulate);
            }

            if (settings.Mode != VertexPaintBrushMode.Sample)
            {
                settings.Strength = EditorGUILayout.Slider("Strength", settings.Strength, 0f, 1f);
                settings.Flow = EditorGUILayout.Slider("Flow", settings.Flow, 0f, 1f);
            }

            EditorGUILayout.Space(3f);
            settings.NormalMaskEnabled = EditorGUILayout.Toggle("Normal Mask", settings.NormalMaskEnabled);
            if (settings.NormalMaskEnabled)
            {
                settings.NormalDirection = EditorGUILayout.Vector3Field("Direction", settings.NormalDirection);
                settings.NormalAngle = EditorGUILayout.Slider("Max Angle", settings.NormalAngle, 0f, 180f);
            }

            settings.SubmeshMaskEnabled = EditorGUILayout.Toggle("Submesh Mask", settings.SubmeshMaskEnabled);
            if (settings.SubmeshMaskEnabled)
            {
                settings.SubmeshMaskIndex = EditorGUILayout.IntField("Submesh", settings.SubmeshMaskIndex);
            }

            settings.ThresholdMaskEnabled = EditorGUILayout.Toggle(
                "Value Threshold",
                settings.ThresholdMaskEnabled);
            if (settings.ThresholdMaskEnabled)
            {
                settings.ThresholdChannel = (VertexPaintChannel)EditorGUILayout.EnumPopup(
                    "Threshold Channel",
                    settings.ThresholdChannel);
                var minimum = settings.ThresholdRange.x;
                var maximum = settings.ThresholdRange.y;
                EditorGUILayout.MinMaxSlider("Value Range", ref minimum, ref maximum, 0f, 1f);
                settings.ThresholdRange = new Vector2(minimum, maximum);
                settings.ThresholdInclusive = EditorGUILayout.Toggle(
                    "Inclusive Bounds",
                    settings.ThresholdInclusive);
            }

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }
    }
}
