using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    /// <summary>Owns only temporary Scene preview resources; never assigns renderer state.</summary>
    internal sealed class TextureWeatheringSession : IDisposable
    {
        internal const string ShaderName = "Hidden/SimpleBlend/Weathering Changes";
        private static TextureWeatheringSession _active;
        private TextureWeatheringPreview _preview;
        private Texture2D _changes;
        private Material _material;
        private TexturePaintTarget _target;
        private int _surface;
        private bool _erase;
        private double _nextValidation;
        internal TexturePaintTarget Target => _target;
        internal Texture2D Changes => _changes;
        internal Material PreviewMaterial => _material;
        internal int ChangedTexels => _preview?.ChangedTexels ?? 0;
        internal bool Active => _preview != null;
        internal event Action Changed;

        internal static bool BlocksBrush(TexturePaintTarget target) => _active != null && _active.Active && _active._target == target;

        internal static void CancelFor(TexturePaintTarget target)
        {
            if (_active != null && _active._target == target) _active.Cancel();
        }

        internal bool Matches(TexturePaintTarget target, int surface, bool erase)
            => Active && _target == target && _surface == surface && _erase == erase;

        internal void Begin(TexturePaintTarget target, int surface, bool erase, TextureWeatheringSettings settings,
            Func<bool> cancelled = null, Action<int, int> progress = null)
        {
            Cancel(); _active?.Cancel();
            SimpleBlendTexturePaintTool.FinishCurrentStroke();
            TextureWeatheringPreview preview = null; Texture2D changes = null; Material material = null;
            try
            {
                preview = new TextureWeatheringPreview(target, surface, erase);
                preview.Generate(settings, cancelled, progress);
                var shader = Shader.Find(ShaderName);
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("The weathering preview shader is unavailable. Saved paint was not changed.");
                var result = preview.CopyPixels(); var baseline = target.Paint.Pixels;
                var pixels = new Color32[result.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var delta = Math.Max(Math.Abs(result[i].r - baseline[i].r), Math.Abs(result[i].g - baseline[i].g));
                    if (target.Paint.WeightChannels == 3) delta = Math.Max(delta, Math.Abs(result[i].b - baseline[i].b));
                    pixels[i] = new Color32((byte)delta, 0, 0, 255);
                }
                changes = new Texture2D(target.Paint.Mask.width, target.Paint.Mask.height, TextureFormat.RGBA32, false, true)
                { name = "SimpleBlend Weathering Changes", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                changes.SetPixels32(pixels); changes.Apply(false, false);
                material = new Material(shader) { name = "SimpleBlend Weathering Preview", hideFlags = HideFlags.HideAndDontSave };
                material.SetTexture("_ChangeMask", changes); material.SetColor("_PreviewColor", Handles.selectedColor);
                preview.CheckCurrent();
                _preview = preview; _changes = changes; _material = material; _target = target; _surface = surface; _erase = erase;
                _active = this; Subscribe(); Changed?.Invoke(); SceneView.RepaintAll();
            }
            catch
            {
                if (_preview == preview && preview != null) Cancel();
                else
                {
                    preview?.Dispose();
                    if (material != null) Object.DestroyImmediate(material);
                    if (changes != null) Object.DestroyImmediate(changes);
                }
                throw;
            }
        }

        internal int Apply()
        {
            if (!Active) throw new InvalidOperationException("Preview weathering before applying it.");
            try { return _preview.Apply(); }
            finally { Cancel(); }
        }

        private void Subscribe()
        {
            SceneView.duringSceneGui += Draw;
            Selection.selectionChanged += CheckSelection;
            Undo.undoRedoPerformed += Cancel;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
            ToolManager.activeToolChanged += Cancel;
        }

        private void Unsubscribe()
        {
            SceneView.duringSceneGui -= Draw;
            Selection.selectionChanged -= CheckSelection;
            Undo.undoRedoPerformed -= Cancel;
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            EditorApplication.quitting -= Cancel;
            ToolManager.activeToolChanged -= Cancel;
        }

        private void CheckSelection()
        {
            if (!SimpleBlendTexturePaintTool.KeepTargetForSelection(_target, Selection.objects)) Cancel();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => Cancel();

        internal void CheckCurrent()
        {
            if (!Active) return;
            try { _preview.CheckCurrent(); }
            catch (InvalidOperationException) { Cancel(); }
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextValidation) return;
            _nextValidation = EditorApplication.timeSinceStartup + 0.25;
            CheckCurrent();
        }

        private void Draw(SceneView view)
        {
            if (!Active || _target == null || _changes == null || _material == null) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { Cancel(); Event.current.Use(); return; }
            if (Event.current.type != EventType.Repaint || !string.IsNullOrEmpty(TexturePaintStroke.Problem(_target))) return;
            if (_material.SetPass(0)) Graphics.DrawMeshNow(_target.Paint.Mesh, _target.transform.localToWorldMatrix, _target.MaterialSection);
        }

        internal void Cancel()
        {
            var wasActive = Active;
            Unsubscribe(); _preview?.Dispose(); _preview = null;
            if (_material != null) Object.DestroyImmediate(_material);
            if (_changes != null) Object.DestroyImmediate(_changes);
            _material = null; _changes = null; _target = null;
            if (_active == this) _active = null;
            if (wasActive) { Changed?.Invoke(); SceneView.RepaintAll(); }
        }

        public void Dispose() => Cancel();
    }
}
