using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    // Both authoring surfaces edit the same preferences; foldout state belongs to each surface.
    internal sealed class SimpleBlendBrushControls
    {
        private bool _advanced;

        internal void Draw()
        {
            var settings = SimpleBlendBrushSettings.instance;
            EditorGUI.BeginChangeCheck();
            settings.Mode = (VertexPaintBrushMode)EditorGUILayout.EnumPopup("Operation", settings.Mode);
            settings.Channel = (VertexPaintChannel)EditorGUILayout.EnumPopup(
                new GUIContent("Paint channel", "Writes vertex values. The material determines their visible surface or tint."), settings.Channel);
            settings.PreviewMode = (VertexPaintPreviewMode)EditorGUILayout.EnumPopup("View", settings.PreviewMode);
            if (settings.Mode == VertexPaintBrushMode.Paint || settings.Mode == VertexPaintBrushMode.Fill || settings.Mode == VertexPaintBrushMode.Sample)
                settings.Color = EditorGUILayout.ColorField(new GUIContent("Channel values", "RGBA values stored on vertices. Alpha here is a stored channel, not brush strength."), settings.Color);
            if (settings.Mode == VertexPaintBrushMode.Fill)
                settings.FillScope = (VertexPaintFillScope)EditorGUILayout.EnumPopup("Fill scope", settings.FillScope);
            else if (settings.Mode != VertexPaintBrushMode.Sample)
                settings.Radius = EditorGUILayout.FloatField(new GUIContent("Brush size (m)", "World-space radius in meters, not diameter."), settings.Radius);
            if (settings.Mode != VertexPaintBrushMode.Sample)
                settings.Strength = EditorGUILayout.Slider(new GUIContent("Strength", "Brush influence, separate from stored vertex alpha."), settings.Strength, 0f, 1f);

            _advanced = EditorGUILayout.Foldout(_advanced, "More paint options", true);
            if (!_advanced && (settings.NormalMaskEnabled || settings.SubmeshMaskEnabled || settings.ThresholdMaskEnabled))
            {
                var masks = new System.Collections.Generic.List<string>();
                if (settings.NormalMaskEnabled) masks.Add("Normal Mask");
                if (settings.SubmeshMaskEnabled) masks.Add($"Submesh {settings.SubmeshMaskIndex}");
                if (settings.ThresholdMaskEnabled) masks.Add("Value Threshold");
                EditorGUILayout.HelpBox("Active masks: " + string.Join(", ", masks), MessageType.Info);
            }
            if (_advanced)
            {
                if (settings.Mode != VertexPaintBrushMode.Sample)
                    settings.Flow = EditorGUILayout.Slider("Flow", settings.Flow, 0f, 1f);
                if (settings.Mode != VertexPaintBrushMode.Fill && settings.Mode != VertexPaintBrushMode.Sample)
                {
                    settings.Falloff = EditorGUILayout.CurveField("Falloff", settings.Falloff);
                    settings.Accumulate = EditorGUILayout.Toggle(new GUIContent("Accumulate", "On: repeated dabs build continuously. Off: each stroke is capped relative to its starting colors."), settings.Accumulate);
                }
                settings.NormalMaskEnabled = EditorGUILayout.Toggle("Normal Mask", settings.NormalMaskEnabled);
                if (settings.NormalMaskEnabled)
                {
                    settings.NormalDirection = EditorGUILayout.Vector3Field("Direction", settings.NormalDirection);
                    settings.NormalAngle = EditorGUILayout.Slider("Max Angle", settings.NormalAngle, 0f, 180f);
                }
                settings.SubmeshMaskEnabled = EditorGUILayout.Toggle("Submesh Mask", settings.SubmeshMaskEnabled);
                if (settings.SubmeshMaskEnabled)
                    settings.SubmeshMaskIndex = EditorGUILayout.IntField("Submesh", settings.SubmeshMaskIndex);
                settings.ThresholdMaskEnabled = EditorGUILayout.Toggle("Value Threshold", settings.ThresholdMaskEnabled);
                if (settings.ThresholdMaskEnabled)
                {
                    settings.ThresholdChannel = (VertexPaintChannel)EditorGUILayout.EnumPopup("Threshold Channel", settings.ThresholdChannel);
                    var minimum = settings.ThresholdRange.x;
                    var maximum = settings.ThresholdRange.y;
                    EditorGUILayout.MinMaxSlider("Value Range", ref minimum, ref maximum, 0f, 1f);
                    settings.ThresholdRange = new Vector2(minimum, maximum);
                    settings.ThresholdInclusive = EditorGUILayout.Toggle("Inclusive Bounds", settings.ThresholdInclusive);
                }
            }
            if (EditorGUI.EndChangeCheck()) settings.SaveSettings();
        }
    }
}
