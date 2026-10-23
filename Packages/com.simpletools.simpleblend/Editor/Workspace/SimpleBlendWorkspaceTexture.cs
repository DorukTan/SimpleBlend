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
        [SerializeField] private bool _textureSetupOptions;
        [SerializeField] private bool _textureBrushOptions;
        [SerializeField] private bool _textureAssetDetails;
        [SerializeField] private SimpleBlendBrushPreset _texturePreset;
        private int _textureSurfaceEditor = -1;
        private TexturePaintAsset _textureEditingPaint;
        private readonly TextureBrushPreview _textureStrokePreview = new TextureBrushPreview();
        private bool _textureShapeOptions;
        [SerializeField] private int _textureSection = -1;
        [SerializeField] private bool _textureAutomaticSections = true;
        private TexturePaintTarget _textureSectionTarget;

        private bool DrawPaintBackend()
        {
            EditorGUI.BeginChangeCheck();
            _texturePainting = EditorGUILayout.Popup("Painting method", _texturePainting ? 0 : 1,
                new[] { "Texture paint", "Vertex paint" }) == 0;
            if (EditorGUI.EndChangeCheck()) { _weathering.Cancel(); StopForSurfaceConfiguration(); }
            if (!_texturePainting) return false;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                var source = SimpleBlendTexturePaintTool.IsActiveFor(SimpleBlendTexturePaintTool.Target)
                    ? SimpleBlendTexturePaintTool.Target.gameObject : _weathering.Target != null ? _weathering.Target.gameObject
                    : Selection.objects.Length == 1 ? Selection.activeGameObject : null;
                var target = source == null ? null : source.GetComponent<TexturePaintTarget>();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Paint object", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(source, typeof(GameObject), true);
                    if (target == null) DrawTextureSetup(source);
                    else DrawTextureTarget(target);
                }
                if (!string.IsNullOrEmpty(_operationMessage)) EditorGUILayout.HelpBox(_operationMessage, _operationType);
            }
            return true;
        }

        private void DrawTextureSetup(GameObject source)
        {
            var mesh = source == null ? null : source.GetComponent<MeshFilter>()?.sharedMesh;
            _materialSection = mesh == null ? 0 : Mathf.Clamp(_materialSection, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            _textureSetupOptions = EditorGUILayout.Foldout(_textureSetupOptions, "Advanced setup", true);
            if (_textureSetupOptions)
            {
                _palette = (SurfacePalette)EditorGUILayout.ObjectField("Optional palette", _palette, typeof(SurfacePalette), false);
                if (GUILayout.Button("Create surface palette")) SurfacePaletteCreator.Open();
                if (mesh != null && mesh.subMeshCount > 0)
                {
                    var sections = new string[mesh.subMeshCount];
                    for (var i = 0; i < sections.Length; i++) sections[i] = $"Section {i} · material slot {i}";
                    _materialSection = EditorGUILayout.Popup("Mesh section", _materialSection, sections);
                }
                var index = _textureResolution == 256 ? 0 : _textureResolution == 1024 ? 2 : 1;
                index = EditorGUILayout.Popup("Mask resolution", index, new[] { "256 × 256", "512 × 512", "1024 × 1024" });
                _textureResolution = index == 0 ? 256 : index == 2 ? 1024 : 512;
            }
            EditorGUILayout.LabelField($"Section {_materialSection} · {_textureResolution} × {_textureResolution} mask");
            EditorGUILayout.LabelField(_palette == null ? "Surfaces: from source material" : "Palette: " + _palette.name);
            var problem = TexturePaintSetup.Problem(source, _palette, _materialSection, false);
            if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Info);
            EditorGUILayout.HelpBox("Start creates a paint copy, hides the original renderer and saves its assets under Assets/SimpleBlendPaint. Base starts from common source-material properties unless you choose a palette. The original mesh, children, scripts and colliders stay intact. Undo restores the original renderer.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(problem)))
            {
                if (!GUILayout.Button("Start painting", GUILayout.Height(28))) return;
                try
                {
                    StopForSurfaceConfiguration();
                    SimpleBlendPaintTool.UnlockTarget();
                    var created = TexturePaintSetup.CreateAutomatic(source, _materialSection, _textureResolution, _palette);
                    Selection.activeGameObject = created.gameObject;
                    _textureSurface = Mathf.Min(1, created.Paint.SurfaceCount - 1);
                    _textureErase = false;
                    _textureSectionTarget = created; _textureSection = created.MaterialSection;
                    SimpleBlendTexturePaintTool.Start(created, _textureSurface, false, _textureSection, _textureAutomaticSections);
                    _operationMessage = "Ready to paint. The original renderer is hidden, not deleted. Paint assets save automatically; save your scene too.";
                    _operationType = MessageType.Info;
                }
                catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
                GUIUtility.ExitGUI();
            }
        }

        private void DrawTextureTarget(TexturePaintTarget target)
        {
            if (_textureSectionTarget != target)
            {
                _textureSectionTarget = target; _textureSection = target.MaterialSection;
                _textureSurface = Mathf.Min(1, target.Paint == null ? 0 : target.Paint.SurfaceCount - 1);
            }
            if (SimpleBlendTexturePaintTool.IsActiveFor(target))
            {
                _textureSection = SimpleBlendTexturePaintTool.Section;
                _textureSurface = SimpleBlendTexturePaintTool.Surface;
                _textureAutomaticSections = SimpleBlendTexturePaintTool.AutomaticSections;
            }
            if (!DrawTextureSections(target)) return;
            var problem = TexturePaintStroke.Problem(target, _textureSection);
            if (!string.IsNullOrEmpty(problem)) { _weathering.Cancel(); EditorGUILayout.HelpBox(problem, MessageType.Error); return; }
            var paint = target.PaintForSection(_textureSection);
            if (_textureEditingPaint != paint) { _textureSurfaceEditor = -1; _textureEditingPaint = paint; }
            EditorGUILayout.LabelField($"{paint.Mask.width} × {paint.Mask.height} mask · Section {_textureSection}");
            EditorGUILayout.LabelField("Choose a surface", EditorStyles.boldLabel);
            _textureSurface = Mathf.Clamp(_textureSurface, 0, paint.SurfaceCount - 1);
            for (var i = 0; i < paint.SurfaceCount; i++)
            {
                var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                var label = i == 0 && paint.SurfaceName(i) != "Base" ? "Base · " + paint.SurfaceName(i) : paint.SurfaceName(i);
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(38, 6, 3, 3) };
                var selectRect = new Rect(rect.x, rect.y, Mathf.Max(0, rect.width - 56), rect.height);
                if (GUI.Toggle(selectRect, i == _textureSurface, label, style) && i != _textureSurface)
                { _weathering.Cancel(); _textureSurface = i; SimpleBlendTexturePaintTool.Configure(target, i, _textureErase); }
                if (GUI.Button(new Rect(rect.xMax - 52, rect.y, 52, rect.height), _textureSurfaceEditor == i ? "Close" : "Edit"))
                    _textureSurfaceEditor = _textureSurfaceEditor == i ? -1 : i;
                var color = GUI.color;
                GUI.color = paint.Material.GetColor($"_Layer{i}Color");
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 24, 24),
                    paint.Material.GetTexture($"_Layer{i}Albedo") ?? Texture2D.whiteTexture, ScaleMode.ScaleToFit);
                GUI.color = color;
                if (_textureSurfaceEditor == i) DrawTextureSurfaceEditor(target, paint, i);
            }
            using (new EditorGUI.DisabledScope(paint.SurfaceCount >= 4))
                if (GUILayout.Button("+ Add surface"))
                {
                    RunTextureEdit(() =>
                    {
                        SimpleBlendTexturePaintTool.FinishCurrentStroke();
                        _textureSurface = TextureSurfaceAuthoring.Add(paint);
                        _textureSurfaceEditor = _textureSurface;
                        SimpleBlendTexturePaintTool.Configure(target, _textureSurface, _textureErase);
                    });
                    GUIUtility.ExitGUI();
                }
            EditorGUILayout.HelpBox("Selected: " + paint.SurfaceName(_textureSurface)
                + (_textureErase ? (_textureSurface == 0 ? " · erase all overlays toward Base." : " · erase this overlay toward Base; other overlay weights stay unchanged.")
                    : " · paint toward this surface and reduce competing weights. These are blended weights, not an ordered layer stack."), MessageType.None);
            EditorGUI.BeginChangeCheck();
            _textureErase = EditorGUILayout.Popup("Brush action", _textureErase ? 1 : 0, new[] { "Paint", "Erase" }) == 1;
            if (EditorGUI.EndChangeCheck()) { _weathering.Cancel(); SimpleBlendTexturePaintTool.Configure(target, _textureSurface, _textureErase); }
            var settings = SimpleBlendBrushSettings.instance;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                DrawTextureBrushPresets(settings);
                EditorGUI.BeginChangeCheck();
                settings.Radius = EditorGUILayout.Slider("Brush radius (m)", settings.Radius, 0.001f, 10);
                settings.Strength = EditorGUILayout.Slider(new GUIContent("Opacity (%)", "Per-dab strength, or a maximum per stroke when Limit stroke to opacity is enabled."), settings.Strength * 100, 0, 100) / 100;
                settings.Flow = EditorGUILayout.Slider("Flow (%)", settings.Flow * 100, 0, 100) / 100;
                var shape = settings.TextureBrush;
                shape.Tip = (TextureBrushTipKind)EditorGUILayout.EnumPopup("Brush tip", shape.Tip);
                if (shape.Tip == TextureBrushTipKind.Custom)
                {
                    shape.Texture = (Texture2D)EditorGUILayout.ObjectField("Tip texture", shape.Texture, typeof(Texture2D), false);
                    shape.MaskMode = (TextureBrushMaskMode)EditorGUILayout.EnumPopup("Tip mask", shape.MaskMode);
                    shape.Invert = EditorGUILayout.Toggle(new GUIContent("Invert tip",
                        shape.MaskMode == TextureBrushMaskMode.DarkPixels
                            ? "Paint light pixels instead of dark pixels. Transparent areas stay empty."
                            : "Reverse alpha: transparent areas inside the tip paint; opaque areas stay empty."), shape.Invert);
                }
                _textureShapeOptions = EditorGUILayout.Foldout(_textureShapeOptions, "Brush shape", true);
                if (_textureShapeOptions)
                {
                    shape.Hardness = EditorGUILayout.Slider("Hardness (%)", shape.Hardness * 100, 0, 100) / 100;
                    shape.Roundness = EditorGUILayout.Slider("Roundness (%)", shape.Roundness * 100, 5, 100) / 100;
                    shape.Angle = EditorGUILayout.Slider("Angle", shape.Angle, -180, 180);
                    settings.Falloff = EditorGUILayout.CurveField("Falloff", settings.Falloff);
                }
                settings.Accumulate = EditorGUILayout.Popup("Stroke behavior", settings.Accumulate ? 1 : 0,
                    new[] { "Capped per stroke", "Build up along stroke" }) == 1;
                _textureBrushOptions = EditorGUILayout.Foldout(_textureBrushOptions, "Stroke settings", true);
                if (_textureBrushOptions)
                {
                    shape.Spacing = EditorGUILayout.Slider(new GUIContent("Spacing (%)", "Distance between dabs, as a percentage of brush diameter."), shape.Spacing * 100, 1, 200) / 100;
                    shape.Smoothing = EditorGUILayout.Slider("Smoothing (%)", shape.Smoothing * 100, 0, 100) / 100;
                    shape.LimitOpacity = EditorGUILayout.Toggle("Limit stroke to opacity", shape.LimitOpacity);
                    EditorGUILayout.HelpBox("The ring is the projected radius. Paint checks visibility within this mesh from the Scene camera, stays in the selected section and excludes opposing faces. Fill covers the whole section. Other scene objects do not block this brush.", MessageType.None);
                }
                if (EditorGUI.EndChangeCheck()) { SimpleBlendTexturePaintTool.FinishCurrentStroke(); settings.SaveSettings(); }
                _textureStrokePreview.Draw(settings);
            }
            DrawWeathering(target);
            var active = ToolManager.activeToolType == typeof(SimpleBlendTexturePaintTool) && SimpleBlendTexturePaintTool.Target == target;
            if (GUILayout.Button(active ? "Stop painting" : "Start painting", GUILayout.Height(28)))
            {
                try
                {
                    _weathering.Cancel();
                    if (active) SimpleBlendTexturePaintTool.Stop();
                    else
                    {
                        if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
                        SimpleBlendTexturePaintTool.Start(target, _textureSurface, _textureErase, _textureSection, _textureAutomaticSections);
                    }
                }
                catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
            }
            if (GUILayout.Button("Fill section with selected surface"))
            { _weathering.Cancel(); SimpleBlendTexturePaintTool.Fill(target, _textureSurface, _textureSection); }
            if (SimpleBlendTexturePaintTool.LastPaint == paint && !string.IsNullOrEmpty(SimpleBlendTexturePaintTool.LastStroke))
                EditorGUILayout.HelpBox(SimpleBlendTexturePaintTool.LastStroke, MessageType.Info);
            if (GUILayout.Button("Save painted asset"))
            {
                SimpleBlendTexturePaintTool.FinishCurrentStroke();
                AssetDatabase.SaveAssetIfDirty(paint);
                _operationMessage = "Paint asset saved. Save the scene or prefab too to retain its object references.";
                _operationType = MessageType.Info;
            }
            EditorGUILayout.HelpBox("Paint assets save automatically. Save your scene to keep its objects. Undo restores a whole stroke. Objects sharing this paint asset also share surface edits and paint.", MessageType.None);
            _textureAssetDetails = EditorGUILayout.Foldout(_textureAssetDetails, "Asset details", true);
            if (_textureAssetDetails)
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Paint asset", paint, typeof(TexturePaintAsset), false);
        }

        private bool DrawTextureSections(TexturePaintTarget target)
        {
            var mesh = target.GetComponent<MeshFilter>()?.sharedMesh;
            var materials = target.GetComponent<MeshRenderer>()?.sharedMaterials;
            var count = mesh == null || materials == null ? 0 : Mathf.Min(mesh.subMeshCount, materials.Length);
            if (count == 0) { _weathering.Cancel(); EditorGUILayout.HelpBox("Restore the paint mesh and renderer material slots.", MessageType.Error); return false; }
            _textureSection = Mathf.Clamp(_textureSection, 0, count - 1);
            var labels = new string[count];
            for (var i = 0; i < count; i++) labels[i] = $"Section {i} · " + (target.PaintForSection(i) == null ? "Not prepared" : "Ready");
            EditorGUI.BeginChangeCheck();
            var section = EditorGUILayout.Popup("Section", _textureSection, labels);
            if (EditorGUI.EndChangeCheck())
            {
                _weathering.Cancel(); SimpleBlendTexturePaintTool.FinishCurrentStroke();
                if (Selection.activeGameObject != target.gameObject) Selection.activeGameObject = target.gameObject;
                _textureSection = section; _textureSurfaceEditor = -1;
                if (SimpleBlendTexturePaintTool.IsActiveFor(target))
                {
                    if (string.IsNullOrEmpty(TexturePaintStroke.Problem(target, section))) SimpleBlendTexturePaintTool.SelectSection(target, section);
                    else SimpleBlendTexturePaintTool.StopForSectionSetup(target);
                }
                var paint = target.PaintForSection(section);
                _textureSurface = SimpleBlendTexturePaintTool.IsActiveFor(target) ? SimpleBlendTexturePaintTool.Surface
                    : Mathf.Min(1, paint == null ? 0 : paint.SurfaceCount - 1);
            }
            EditorGUI.BeginChangeCheck();
            _textureAutomaticSections = EditorGUILayout.Popup("Section targeting", _textureAutomaticSections ? 0 : 1,
                new[] { "Automatic", "Lock to section" }) == 0;
            if (EditorGUI.EndChangeCheck()) { _weathering.Cancel(); SimpleBlendTexturePaintTool.SetSectionMode(target, _textureAutomaticSections); }
            EditorGUILayout.HelpBox(_textureAutomaticSections
                ? "Automatic chooses a prepared section when a stroke starts. Each stroke stays on that section until release. Fill and surface edits use the displayed section."
                : "Brush strokes stay on the displayed section. Other sections block painting through to it.", MessageType.None);
            if (SimpleBlendTexturePaintTool.IsActiveFor(target) && !string.IsNullOrEmpty(SimpleBlendTexturePaintTool.PointerStatus))
                EditorGUILayout.HelpBox(SimpleBlendTexturePaintTool.PointerStatus, MessageType.None);
            if (target.PaintForSection(_textureSection) != null) return true;
            _weathering.Cancel();
            EditorGUILayout.HelpBox($"Prepare section {_textureSection} to paint it. This copies the primary section's surfaces into a separate mask and material, using the same mesh and UVs. Earlier paint stays unchanged. Assets save beside the primary paint asset.", MessageType.Info);
            if (GUILayout.Button("Prepare section"))
            {
                RunTextureEdit(() =>
                {
                    var paint = TexturePaintSetup.PrepareSectionFromPrimary(target, _textureSection);
                    _textureSurface = Mathf.Min(1, paint.SurfaceCount - 1);
                    SimpleBlendTexturePaintTool.Start(target, _textureSurface, _textureErase, _textureSection, _textureAutomaticSections);
                    _operationMessage = "Section prepared. Paint assets save automatically; save your scene too.";
                    _operationType = MessageType.Info;
                });
                GUIUtility.ExitGUI();
            }
            return false;
        }

        private void DrawTextureSurfaceEditor(TexturePaintTarget target, TexturePaintAsset paint, int index)
        {
            var surface = TextureSurfaceAuthoring.Read(paint, index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                surface.Name = EditorGUILayout.DelayedTextField("Name", surface.Name);
                surface.Tint = EditorGUILayout.ColorField("Tint", surface.Tint);
                surface.Albedo = EditorGUILayout.ObjectField("Albedo", surface.Albedo, typeof(Texture2D), false) as Texture2D;
                surface.Normal = EditorGUILayout.ObjectField("Normal", surface.Normal, typeof(Texture2D), false) as Texture2D;
                surface.Mask = EditorGUILayout.ObjectField(new GUIContent("Packed mask", "R: metallic, G: occlusion, A: smoothness"), surface.Mask, typeof(Texture2D), false) as Texture2D;
                surface.Height = EditorGUILayout.ObjectField("Height", surface.Height, typeof(Texture2D), false) as Texture2D;
                surface.NormalScale = EditorGUILayout.Slider("Normal strength", surface.NormalScale, 0, 2);
                surface.Metallic = EditorGUILayout.Slider("Metallic", surface.Metallic, 0, 1);
                surface.Smoothness = EditorGUILayout.Slider("Smoothness", surface.Smoothness, 0, 1);
                surface.Occlusion = EditorGUILayout.Slider("Occlusion", surface.Occlusion, 0, 1);
                surface.Tiling = EditorGUILayout.Vector2Field("Tiling", surface.Tiling);
                surface.Offset = EditorGUILayout.Vector2Field("Offset", surface.Offset);
                if (EditorGUI.EndChangeCheck()) RunTextureEdit(() =>
                {
                    SimpleBlendTexturePaintTool.FinishCurrentStroke();
                    TextureSurfaceAuthoring.Edit(paint, index, surface);
                    SimpleBlendTexturePaintTool.Configure(target, _textureSurface, _textureErase);
                });
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("Remove surface"))
                    {
                        SimpleBlendTexturePaintTool.FinishCurrentStroke();
                        if (!EditorUtility.DisplayDialog("Remove surface", "Remove " + paint.SurfaceName(index)
                            + "? Its painted weight returns to Base. Other surfaces keep their weights. This changes every object sharing this paint asset and supports Undo.", "Remove", "Cancel")) return;
                        RunTextureEdit(() =>
                        {
                            TextureSurfaceAuthoring.Remove(paint, index);
                            if (_textureSurface == index) _textureSurface = 0;
                            else if (_textureSurface > index) _textureSurface--;
                            _textureSurfaceEditor = -1;
                            SimpleBlendTexturePaintTool.Configure(target, _textureSurface, _textureErase);
                        });
                        GUIUtility.ExitGUI();
                    }
            }
        }

        private void DrawTextureBrushPresets(SimpleBlendBrushSettings settings)
        {
            var preset = EditorGUILayout.Popup("Quick preset", 0, SimpleBlendBrushPreset.BuiltInNames);
            if (preset > 0)
            {
                SimpleBlendTexturePaintTool.FinishCurrentStroke();
                SimpleBlendBrushPreset.ApplyBuiltIn(preset, settings);
                settings.SaveSettings();
            }
            EditorGUI.BeginChangeCheck();
            _texturePreset = (SimpleBlendBrushPreset)EditorGUILayout.ObjectField("Saved preset", _texturePreset, typeof(SimpleBlendBrushPreset), false);
            if (EditorGUI.EndChangeCheck() && _texturePreset != null)
            {
                SimpleBlendTexturePaintTool.FinishCurrentStroke();
                _texturePreset.Apply(settings);
                settings.SaveSettings();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_texturePreset == null))
                    if (GUILayout.Button("Apply saved preset"))
                    {
                        SimpleBlendTexturePaintTool.FinishCurrentStroke();
                        _texturePreset.Apply(settings);
                        settings.SaveSettings();
                    }
                if (GUILayout.Button("Save preset…"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Save brush preset", "BrushPreset", "asset", "Save a reusable brush preset without overwriting an existing asset.");
                    if (!string.IsNullOrEmpty(path)) RunTextureEdit(() => _texturePreset = SimpleBlendBrushPreset.SaveNew(path, settings));
                }
            }
        }

        private void RunTextureEdit(System.Action action)
        {
            try { _weathering.Cancel(); _operationMessage = null; action(); }
            catch (System.Exception exception) { _operationMessage = exception.Message; _operationType = MessageType.Error; }
        }
    }
}
