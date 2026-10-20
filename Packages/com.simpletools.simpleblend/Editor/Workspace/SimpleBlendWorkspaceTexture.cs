using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed partial class SimpleBlendWorkspace
    {
        [SerializeField] private bool _texturePainting = true;
        [SerializeField] private int _textureResolution = 512;
        [SerializeField] private int _textureSurface = 1;
        [SerializeField] private bool _textureErase;

        private bool DrawPaintBackend()
        {
            EditorGUI.BeginChangeCheck();
            _texturePainting = EditorGUILayout.Popup("Painting method", _texturePainting ? 0 : 1,
                new[] { "Texture paint", "Vertex paint" }) == 0;
            if (EditorGUI.EndChangeCheck()) StopForSurfaceConfiguration();
            if (!_texturePainting) return false;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                var source = Selection.objects.Length == 1 ? Selection.activeGameObject : null;
                var target = source == null ? null : source.GetComponent<TexturePaintTarget>();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Paint object", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(source, typeof(GameObject), true);
                    EditorGUILayout.HelpBox("Texture paint writes a blend mask, not mesh vertices. Small strokes keep their shape on low-poly meshes.", MessageType.Info);
                    if (target == null) DrawTextureSetup(source);
                    else DrawTextureTarget(target);
                }
                if (!string.IsNullOrEmpty(_operationMessage)) EditorGUILayout.HelpBox(_operationMessage, _operationType);
            }
            return true;
        }

        private void DrawTextureSetup(GameObject source)
        {
            _palette = (SurfacePalette)EditorGUILayout.ObjectField("Surface palette", _palette, typeof(SurfacePalette), false);
            if (GUILayout.Button("Create surface palette")) SurfacePaletteCreator.Open();
            var mesh = source == null ? null : source.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null && mesh.subMeshCount > 0)
            {
                var sections = new string[mesh.subMeshCount];
                for (var i = 0; i < sections.Length; i++) sections[i] = $"Section {i} · material slot {i}";
                _materialSection = EditorGUILayout.Popup("Mesh section", Mathf.Clamp(_materialSection, 0, sections.Length - 1), sections);
            }
            var index = _textureResolution == 256 ? 0 : _textureResolution == 1024 ? 2 : 1;
            index = EditorGUILayout.Popup("Mask resolution", index, new[] { "256 × 256", "512 × 512", "1024 × 1024" });
            _textureResolution = index == 0 ? 256 : index == 2 ? 1024 : 512;
            var problem = TexturePaintSetup.Problem(source, _palette, _materialSection);
            if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Info);
            EditorGUILayout.HelpBox("Setup creates a renderer-only sibling and hides the original renderer. The new asset owns a unique UV4 paint atlas, mask and copied palette material. Children, scripts, colliders and vertex paint stay on the original. Undo restores its renderer; the saved paint asset remains reusable.", MessageType.None);
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(problem)))
            {
                if (!GUILayout.Button("Create texture paint copy", GUILayout.Height(28))) return;
                var path = EditorUtility.SaveFilePanelInProject("Save texture paint", "TexturePaint", "asset", "Choose a new asset for this object's mesh, material and mask.");
                if (string.IsNullOrEmpty(path)) return;
                try
                {
                    StopForSurfaceConfiguration();
                    SimpleBlendPaintTool.UnlockTarget();
                    var created = TexturePaintSetup.Create(source, _palette, _materialSection, _textureResolution, path);
                    Selection.activeGameObject = created.gameObject;
                    _operationMessage = "Texture painting is ready. Choose a surface, then Start painting. The original renderer is hidden, not deleted.";
                    _operationType = MessageType.Info;
                }
                catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
                GUIUtility.ExitGUI();
            }
        }

        private void DrawTextureTarget(TexturePaintTarget target)
        {
            var problem = TexturePaintStroke.Problem(target);
            if (!string.IsNullOrEmpty(problem)) { EditorGUILayout.HelpBox(problem, MessageType.Error); return; }
            var paint = target.Paint;
            EditorGUILayout.LabelField($"{paint.Mask.width} × {paint.Mask.height} mask · Section {target.MaterialSection}");
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Paint asset", paint, typeof(TexturePaintAsset), false);
            EditorGUILayout.LabelField("Choose a surface", EditorStyles.boldLabel);
            _textureSurface = Mathf.Clamp(_textureSurface, 0, paint.SurfaceCount - 1);
            for (var i = 0; i < paint.SurfaceCount; i++)
            {
                var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                var label = i == 0 ? "Base · " + paint.SurfaceName(i) : "Overlay " + i + " · " + paint.SurfaceName(i);
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(38, 6, 3, 3) };
                if (GUI.Toggle(rect, i == _textureSurface, label, style) && i != _textureSurface)
                { SimpleBlendTexturePaintTool.Stop(); _textureSurface = i; }
                var color = GUI.color;
                GUI.color = paint.Material.GetColor($"_Layer{i}Color");
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 24, 24),
                    paint.Material.GetTexture($"_Layer{i}Albedo") ?? Texture2D.whiteTexture, ScaleMode.ScaleToFit);
                GUI.color = color;
            }
            EditorGUILayout.HelpBox("Selected: " + paint.SurfaceName(_textureSurface)
                + (_textureErase ? (_textureSurface == 0 ? " · erase all overlays toward Base." : " · erase this overlay toward Base; other overlay weights stay unchanged.")
                    : " · paint toward this surface and reduce competing weights. These are blended weights, not an ordered layer stack."), MessageType.None);
            EditorGUI.BeginChangeCheck();
            _textureErase = EditorGUILayout.Popup("Brush action", _textureErase ? 1 : 0, new[] { "Paint", "Erase" }) == 1;
            if (EditorGUI.EndChangeCheck()) SimpleBlendTexturePaintTool.Stop();
            var settings = SimpleBlendBrushSettings.instance;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                settings.Radius = EditorGUILayout.Slider("Brush radius (m)", settings.Radius, 0.001f, 10);
                settings.Strength = EditorGUILayout.Slider("Strength", settings.Strength, 0, 1);
                settings.Flow = EditorGUILayout.Slider("Flow", settings.Flow, 0, 1);
                settings.Falloff = EditorGUILayout.CurveField("Falloff", settings.Falloff);
                settings.Accumulate = EditorGUILayout.Popup("Stroke behavior", settings.Accumulate ? 1 : 0,
                    new[] { "Capped per stroke", "Build up along stroke" }) == 1;
                if (EditorGUI.EndChangeCheck()) { SimpleBlendTexturePaintTool.Stop(); settings.SaveSettings(); }
                EditorGUILayout.HelpBox("The ring is the projected radius. Falloff softens its edge. Projection reaches a quarter-radius in depth and excludes opposing faces; it is not a visibility or image-decal tool.", MessageType.None);
            }
            var active = ToolManager.activeToolType == typeof(SimpleBlendTexturePaintTool) && SimpleBlendTexturePaintTool.Target == target;
            if (GUILayout.Button(active ? "Stop painting" : "Start painting", GUILayout.Height(28)))
            {
                try
                {
                    if (active) SimpleBlendTexturePaintTool.Stop();
                    else
                    {
                        if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
                        SimpleBlendTexturePaintTool.Start(target, _textureSurface, _textureErase);
                    }
                }
                catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
            }
            if (GUILayout.Button("Fill section with selected surface"))
                SimpleBlendTexturePaintTool.Fill(target, _textureSurface);
            if (SimpleBlendTexturePaintTool.LastPaint == paint && !string.IsNullOrEmpty(SimpleBlendTexturePaintTool.LastStroke))
                EditorGUILayout.HelpBox(SimpleBlendTexturePaintTool.LastStroke, MessageType.Info);
            if (GUILayout.Button("Save painted asset"))
            {
                SimpleBlendTexturePaintTool.Stop();
                AssetDatabase.SaveAssetIfDirty(paint);
                _operationMessage = "Paint asset saved. Save the scene or prefab too to retain its object references.";
                _operationType = MessageType.Info;
            }
            EditorGUILayout.HelpBox("Undo restores a whole stroke. Save the painted asset and scene before closing. Material edits belong to this painted copy; the source palette is unchanged. Duplicating this object shares its paint asset.", MessageType.None);
        }
    }
}
