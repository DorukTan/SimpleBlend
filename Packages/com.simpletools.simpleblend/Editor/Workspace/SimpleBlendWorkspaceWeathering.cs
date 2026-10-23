using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed partial class SimpleBlendWorkspace
    {
        [SerializeField] private bool _weatheringExpanded;
        [SerializeField] private TextureWeatheringSettings _weatheringSettings = new TextureWeatheringSettings();
        private readonly TextureWeatheringSession _weathering = new TextureWeatheringSession();

        private void DrawWeathering(TexturePaintTarget target)
        {
            if (_weathering.Active && !_weathering.Matches(target, _textureSurface, _textureErase)) _weathering.Cancel();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _weatheringExpanded = EditorGUILayout.Foldout(_weatheringExpanded, "Weathering", true);
                if (EditorGUI.EndChangeCheck() && !_weatheringExpanded) _weathering.Cancel();
                if (!_weatheringExpanded) return;
                if (_weatheringSettings == null) _weatheringSettings = new TextureWeatheringSettings();
                var settings = _weatheringSettings;
                EditorGUILayout.LabelField((_textureErase ? "Erase · " : "Paint · ") + target.Paint.SurfaceName(_textureSurface)
                    + " · Section " + target.MaterialSection, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.BeginChangeCheck();
                settings.Source = (TextureWeatheringSource)EditorGUILayout.Popup("Source", (int)settings.Source,
                    new[] { "Noise", "Mesh borders", "UV seams" });
                using (new EditorGUILayout.HorizontalScope())
                {
                    settings.Seed = EditorGUILayout.IntField("Seed", settings.Seed);
                    if (GUILayout.Button("Randomize", GUILayout.Width(78)))
                    { _weathering.Cancel(); settings.Seed = TextureWeatheringMask.NextSeed(settings.Seed); }
                }
                settings.NoiseScale = EditorGUILayout.Slider(new GUIContent("Noise scale", "Inverse local mesh length. Object transforms do not move the pattern inside the mesh."), settings.NoiseScale, 0.1f, 32);
                settings.Coverage = EditorGUILayout.Slider("Coverage (%)", settings.Coverage * 100, 0, 100) / 100;
                if (settings.Source != TextureWeatheringSource.Noise)
                    settings.Width = EditorGUILayout.FloatField(new GUIContent("Band width (local)", "Distance from mesh borders or original UV0 seams in local mesh units, not world metres."), settings.Width);
                settings.Softness = EditorGUILayout.Slider("Softness (%)", settings.Softness * 100, 0, 100) / 100;
                settings.Strength = EditorGUILayout.Slider("Strength (%)", settings.Strength * 100, 0, 100) / 100;
                if (EditorGUI.EndChangeCheck()) _weathering.Cancel();
                EditorGUILayout.HelpBox("Whole selected section, including hidden faces. Noise and edge widths use local mesh space. Coverage changes the noise threshold, not an exact painted percentage.", MessageType.None);
                if (_weathering.Active)
                    EditorGUILayout.HelpBox($"Preview: {_weathering.ChangedTexels:N0} texels would change. The Scene highlight shows affected texels, not the final material. Saved paint is unchanged; brush input is paused. Apply or Cancel to resume (Esc cancels).", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_weathering.Active ? "Refresh preview" : "Preview")) GenerateWeathering(target);
                    using (new EditorGUI.DisabledScope(!_weathering.Active))
                    {
                        if (GUILayout.Button("Apply"))
                        {
                            try
                            {
                                var count = _weathering.Apply();
                                _operationMessage = $"Weathering applied: {count:N0} texels changed. Undo restores the whole operation.";
                                _operationType = MessageType.Info;
                            }
                            catch (Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
                        }
                        if (GUILayout.Button("Cancel")) _weathering.Cancel();
                    }
                }
            }
        }

        private void GenerateWeathering(TexturePaintTarget target)
        {
            var completed = 0; var total = 0;
            try
            {
                _weathering.Begin(target, _textureSurface, _textureErase, _weatheringSettings,
                    () => EditorUtility.DisplayCancelableProgressBar("Weathering preview",
                        completed == 0 ? "Preparing geometry and mask texels…" : $"Mask texels: {completed:N0} / {total:N0}",
                        total == 0 ? 0 : (float)completed / total),
                    (done, count) => { completed = done; total = count; });
                _operationMessage = null;
            }
            catch (OperationCanceledException) { _operationMessage = "Weathering preview cancelled. Saved paint was not changed."; _operationType = MessageType.Info; }
            catch (Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
