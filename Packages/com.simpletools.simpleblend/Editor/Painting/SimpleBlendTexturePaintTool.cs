using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    [EditorTool("SimpleBlend Texture Paint")]
    public sealed class SimpleBlendTexturePaintTool : EditorTool
    {
        internal static TexturePaintTarget Target { get; private set; }
        internal static int Surface { get; private set; }
        internal static bool Erase { get; private set; }
        internal static string LastStroke { get; private set; }
        internal static TexturePaintAsset LastPaint { get; private set; }
        internal static event Action Changed;
        private readonly TexturePaintStroke _stroke = new TexturePaintStroke();
        private readonly VertexStrokeSampler _sampler = new VertexStrokeSampler();
        private readonly List<Vector3> _samples = new List<Vector3>();
        private TexturePaintAtlas _atlas;
        private int _control;
        private SceneView _view;
        private Vector3 _lastPoint;
        private bool _hasPoint;
        private VertexBrushCommand _command;
        private static SimpleBlendTexturePaintTool _active;

        public override GUIContent toolbarIcon => SimpleBlendEditorIcons.Content("Paint", "", "Paint texture surfaces");
        public override bool IsAvailable() => string.IsNullOrEmpty(TexturePaintStroke.Problem(Target));

        internal static void Start(TexturePaintTarget target, int surface, bool erase)
        {
            var problem = TexturePaintStroke.Problem(target);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            if (IsActiveFor(target)) { Configure(target, surface, erase); return; }
            Stop();
            Target = target;
            Surface = Mathf.Clamp(surface, 0, target.Paint.SurfaceCount - 1);
            Erase = erase;
            LastStroke = null;
            LastPaint = null;
            ToolManager.SetActiveTool<SimpleBlendTexturePaintTool>();
        }

        internal static void Stop()
        {
            if (ToolManager.activeToolType == typeof(SimpleBlendTexturePaintTool)) ToolManager.RestorePreviousTool();
        }

        internal static bool IsActiveFor(TexturePaintTarget target)
            => target != null && _active != null && Target == target && ToolManager.activeToolType == typeof(SimpleBlendTexturePaintTool);

        internal static void FinishCurrentStroke() => _active?.Finish();

        internal static void Configure(TexturePaintTarget target, int surface, bool erase)
        {
            if (!IsActiveFor(target)) return;
            _active.Finish();
            Surface = Mathf.Clamp(surface, 0, target.Paint.SurfaceCount - 1);
            Erase = erase;
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        internal static bool KeepTargetForSelection(TexturePaintTarget target, Object[] selection)
        {
            if (target == null || selection == null || selection.Length != 1) return false;
            var selected = selection[0];
            if (selected == null) return false;
            if (EditorUtility.IsPersistent(selected)) return true;
            var gameObject = selected is GameObject go ? go : (selected as Component)?.gameObject;
            return gameObject == target.gameObject;
        }

        private void OnSelectionChanged()
        {
            if (!KeepTargetForSelection(Target, Selection.objects)) Stop();
        }

        internal static VertexBrushCommand Command()
        {
            var settings = SimpleBlendBrushSettings.instance;
            return new VertexBrushCommand(Vector3.zero, Mathf.Clamp(settings.Radius, 0.001f, 100), settings.Strength,
                settings.Flow, new Color32(), settings.Falloff, VertexPaintBrushMode.Paint,
                VertexPaintChannel.RGB, settings.Accumulate, VertexBrushMask.None);
        }

        internal static int Fill(TexturePaintTarget target, int surface)
        {
            var problem = TexturePaintStroke.Problem(target);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            FinishCurrentStroke();
            var stroke = new TexturePaintStroke();
            var atlas = IsActiveFor(target) ? _active._atlas : new TexturePaintAtlas(target.Paint.Mesh, target.MaterialSection, target.Paint.Mask.width);
            stroke.Begin(target, atlas, surface, false, Command());
            stroke.Dab(Vector3.zero, Vector3.up, true);
            var count = stroke.End();
            LastStroke = $"Fill: {count:N0} mask texels changed.";
            LastPaint = target.Paint;
            Changed?.Invoke();
            SceneView.RepaintAll();
            return count;
        }

        public override void OnActivated()
        {
            _atlas = new TexturePaintAtlas(Target.Paint.Mesh, Target.MaterialSection, Target.Paint.Mask.width);
            _active = this;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += CheckFocus;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += OnUndo;
        }

        public override void OnWillBeDeactivated()
        {
            Finish();
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= CheckFocus;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Undo.undoRedoPerformed -= OnUndo;
            _atlas = null;
            _active = null;
            Target = null;
            Changed?.Invoke();
        }

        private void CheckFocus()
        {
            if (_view != null && EditorWindow.focusedWindow != _view) Finish();
            if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target))) Stop();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => Stop();
        private void OnUndo()
        {
            Finish();
            if (Target != null && Target.Paint != null) Surface = Mathf.Clamp(Surface, 0, Target.Paint.SurfaceCount - 1);
            LastStroke = null;
            Changed?.Invoke();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view) || _atlas == null) return;
            if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target))) { Finish(); return; }
            var e = Event.current;
            var id = GUIUtility.GetControlID(0x53425458, FocusType.Passive);
            if (e.type == EventType.Layout && !e.alt && !EditorGUIUtility.editingTextField) HandleUtility.AddDefaultControl(id);
            if (EditorWindow.focusedWindow != view || EditorGUIUtility.editingTextField)
            {
                if (_view == view) Finish();
                return;
            }
            if (e.alt || e.type == EventType.MouseLeaveWindow || e.type == EventType.Ignore) { Finish(); return; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Finish(); Stop(); e.Use(); return; }
            _atlas.SetTransform(Target.transform.localToWorldMatrix);
            var hasHit = _atlas.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit);
            if (e.type == EventType.Repaint && hasHit)
            {
                using (new Handles.DrawingScope(Erase ? new Color(1, 0.35f, 0.3f) : Color.yellow))
                    Handles.DrawWireDisc(hit.Point, hit.Normal, Command().Radius);
            }
            if (e.type == EventType.MouseMove) view.Repaint();
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && hasHit && GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id:
                    _command = Command();
                    _stroke.Begin(Target, _atlas, Surface, Erase, _command);
                    GUIUtility.hotControl = _control = id;
                    _view = view;
                    _sampler.Reset();
                    _hasPoint = false;
                    Append(hit);
                    e.Use();
                    break;
                case EventType.MouseDrag when e.button == 0 && _control == id && _view == view:
                    if (hasHit) Append(hit);
                    else { _sampler.Reset(); _hasPoint = false; }
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0 && _control == id && _view == view:
                    Finish();
                    e.Use();
                    break;
            }
        }

        private void Append(MeshRaycastHit hit)
        {
            // Do not bridge a large jump between disconnected surfaces or create an unbounded dab list.
            if (_hasPoint && Vector3.Distance(_lastPoint, hit.Point) > _command.Radius * 100) _sampler.Reset();
            _sampler.Append(hit.Point, _command.Radius, _samples);
            foreach (var sample in _samples)
            {
                // Reproject resampled positions onto the actual section instead of painting through empty space.
                var ray = new Ray(sample + hit.Normal * _command.Radius, -hit.Normal);
                if (_atlas.Raycast(ray, out var projected) && Vector3.Distance(projected.Point, sample) <= _command.Radius * 0.25f)
                    _stroke.Dab(projected.Point, projected.Normal);
            }
            _lastPoint = hit.Point;
            _hasPoint = true;
            SceneView.RepaintAll();
        }

        private void Finish()
        {
            if (_stroke.Active)
            {
                var changed = _stroke.End();
                LastStroke = changed > 0 ? $"Last stroke: {changed:N0} mask texels changed."
                    : "Last stroke: no mask texels changed. Check radius, strength/flow, selected surface and projection reach.";
                LastPaint = Target == null ? null : Target.Paint;
                Changed?.Invoke();
            }
            if (_control != 0 && GUIUtility.hotControl == _control) GUIUtility.hotControl = 0;
            _control = 0;
            _view = null;
            _sampler.Reset();
            _hasPoint = false;
        }
    }
}
