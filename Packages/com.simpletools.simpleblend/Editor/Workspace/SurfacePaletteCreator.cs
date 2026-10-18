using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class SurfacePaletteCreator : EditorWindow
    {
        internal static event Action<SurfacePalette> Created;
        [SerializeField] private SurfaceDefinition[] _surfaces;
        [SerializeField] private int _countIndex;
        private Vector2 _scroll;
        private bool _textureDetails;
        private string _message;
        private MessageType _messageType;

        [MenuItem("Window/SimpleBlend/Create Surface Palette")]
        internal static void Open() => GetWindow<SurfacePaletteCreator>().Show();

        private void OnEnable()
        {
            titleContent = SimpleBlendEditorIcons.Content("Materials", "Surface Palette", "Create reusable named surfaces.");
            minSize = new Vector2(320, 360);
            _countIndex = Mathf.Clamp(_countIndex, 0, 1);
            if (_surfaces == null || _surfaces.Length != 4)
                _surfaces = new[]
                {
                    new SurfaceDefinition { Name = "Base", Tint = new Color(0.55f, 0.55f, 0.55f) },
                    new SurfaceDefinition { Name = "Surface 1", Tint = new Color(0.3f, 0.6f, 0.25f) },
                    new SurfaceDefinition { Name = "Surface 2", Tint = new Color(0.35f, 0.25f, 0.15f) },
                    new SurfaceDefinition { Name = "Surface 3", Tint = new Color(0.3f, 0.5f, 0.7f) }
                };
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Create surface palette", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create a new palette and matching material for URP. Existing assets and renderers stay unchanged; assignment is a separate workspace action.", MessageType.Info);
            _countIndex = EditorGUILayout.Popup("Surfaces", _countIndex, new[] { "3 — Base + 2 overlays", "4 — Base + 3 overlays" });
            _textureDetails = EditorGUILayout.Toggle("Extra texture inputs", _textureDetails);
            for (var i = 0; i < _countIndex + 3; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(i == 0 ? "Base · clear RGB" : i == 1 ? "Overlay · Red" : i == 2 ? "Overlay · Green" : "Overlay · Blue", EditorStyles.boldLabel);
                    var surface = _surfaces[i];
                    surface.Name = EditorGUILayout.TextField("Name", surface.Name);
                    surface.Tint = EditorGUILayout.ColorField("Tint", surface.Tint);
                    surface.Albedo = (Texture2D)EditorGUILayout.ObjectField("Albedo", surface.Albedo, typeof(Texture2D), false);
                    if (_textureDetails)
                    {
                        surface.Normal = (Texture2D)EditorGUILayout.ObjectField("Normal", surface.Normal, typeof(Texture2D), false);
                        surface.Mask = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Mask", SimpleBlendLayerBlendContract.MaskConvention), surface.Mask, typeof(Texture2D), false);
                        surface.Height = (Texture2D)EditorGUILayout.ObjectField("Height", surface.Height, typeof(Texture2D), false);
                    }
                }
            }
            EditorGUILayout.HelpBox("Names and tints are editable defaults. Textures are optional and used as-is; configure normal maps and data color space separately. Import settings and the active render pipeline are never changed.", MessageType.None);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);
            if (GUILayout.Button("Create palette assets", GUILayout.Height(28)))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save surface palette", "SurfacePalette", "asset", "Creates a new .asset and matching .mat. Existing assets cannot be overwritten.");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        var definitions = new SurfaceDefinition[_countIndex + 3];
                        Array.Copy(_surfaces, definitions, definitions.Length);
                        var palette = SurfacePaletteAuthoring.Create(path, definitions);
                        _message = "Palette created. In the workspace, choose a mesh section and explicitly assign its material.";
                        _messageType = MessageType.Info;
                        EditorGUIUtility.PingObject(palette);
                        Created?.Invoke(palette);
                    }
                    catch (Exception exception) { _message = exception.Message; _messageType = MessageType.Error; }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
