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
        internal static int Section { get; private set; }
        internal static bool AutomaticSections { get; private set; }
        internal static string PointerStatus { get; private set; }
        internal static TexturePaintAsset Paint => Target == null ? null : Target.PaintForSection(Section);
        private static readonly Dictionary<int, string> SurfaceChoices = new Dictionary<int, string>();
        internal static string LastStroke { get; private set; }
        internal static TexturePaintAsset LastPaint { get; private set; }
        internal static event Action Changed;
        private readonly TexturePaintStroke _stroke = new TexturePaintStroke();
        private readonly TextureStrokeSampler _sampler = new TextureStrokeSampler();
        private readonly TextureBrushTipCache _tipCache = new TextureBrushTipCache();
        private readonly Vector3[] _outline = new Vector3[65];
        private readonly List<Vector3> _samples = new List<Vector3>();
        private TexturePaintAtlas _atlas;
        private int _control;
        private SceneView _view;
        private Vector3 _lastPoint;
        private bool _hasPoint;
        private VertexBrushCommand _command;
        private static SimpleBlendTexturePaintTool _active;
        private TextureBrushTip _strokeTip;
        private Vector3 _strokeRight;
        private TexturePaintView _strokeView;
        private TexturePaintVisibility _visibility;
        private Matrix4x4 _strokeTransform;
        private TexturePaintAsset _strokePaint;
        private readonly Dictionary<int, TexturePaintAtlas> _atlases = new Dictionary<int, TexturePaintAtlas>();

        public override GUIContent toolbarIcon => SimpleBlendEditorIcons.Content("Paint", "", "Paint texture surfaces");
        public override bool IsAvailable() => string.IsNullOrEmpty(TexturePaintStroke.Problem(Target, Section));

        internal static void Start(TexturePaintTarget target, int surface, bool erase, int? materialSection = null, bool automatic = false)
        {
            var problem = TexturePaintStroke.Problem(target, materialSection);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            var section = materialSection ?? target.MaterialSection;
            if (IsActiveFor(target)) { SelectSection(target, section, surface); SetSectionMode(target, automatic); Configure(target, surface, erase); return; }
            Stop();
            Target = target;
            Section = section;
            AutomaticSections = automatic;
            SurfaceChoices.Clear();
            Surface = Mathf.Clamp(surface, 0, target.PaintForSection(section).SurfaceCount - 1);
            RememberSurface(section, Surface);
            PointerStatus = null;
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

        internal static void StopForSectionSetup(TexturePaintTarget target)
        {
            if (!IsActiveFor(target)) return;
            Stop();
            Selection.activeGameObject = target.gameObject;
        }

        internal static void FinishCurrentStroke() => _active?.Finish();

        internal static void Configure(TexturePaintTarget target, int surface, bool erase)
        {
            if (!IsActiveFor(target)) return;
            if (Surface != surface || Erase != erase) TextureWeatheringSession.CancelFor(target);
            _active.Finish();
            Surface = Mathf.Clamp(surface, 0, Paint.SurfaceCount - 1);
            RememberSurface(Section, Surface);
            Erase = erase;
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        internal static void SelectSection(TexturePaintTarget target, int section, int? surface = null)
        {
            if (!IsActiveFor(target)) return;
            var problem = TexturePaintStroke.Problem(target, section);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            _active.Finish();
            TextureWeatheringSession.CancelFor(target);
            RememberSurface(Section, Surface);
            Section = section;
            var remembered = SurfaceChoices.TryGetValue(section, out var name) ? FindSurface(Paint, name) : -1;
            Surface = Mathf.Clamp(surface ?? (remembered >= 0 ? remembered : Mathf.Min(1, Paint.SurfaceCount - 1)), 0, Paint.SurfaceCount - 1);
            RememberSurface(section, Surface);
            _active._atlas = _active.Atlas(section);
            PointerStatus = null;
            Changed?.Invoke(); SceneView.RepaintAll();
        }

        internal static void SetSectionMode(TexturePaintTarget target, bool automatic)
        {
            if (!IsActiveFor(target) || AutomaticSections == automatic) return;
            _active.Finish(); TextureWeatheringSession.CancelFor(target);
            AutomaticSections = automatic; PointerStatus = null;
            Changed?.Invoke(); SceneView.RepaintAll();
        }

        internal static bool ResolveHit(TexturePaintTarget target, TexturePaintAtlas atlas, Ray ray, bool automatic,
            int lockedSection, int currentSurface, IReadOnlyDictionary<int, string> choices,
            out MeshRaycastHit hit, out int surface, out string problem)
        {
            surface = 0; problem = null; hit = default;
            if (target == null || atlas == null) return false;
            atlas.SetTransform(target.transform.localToWorldMatrix);
            if (!atlas.RaycastAnySection(ray, out hit)) return false;
            var section = hit.SubmeshIndex;
            if (!automatic && section != lockedSection)
            { problem = $"Section {lockedSection} is locked for this stroke. Section {section} blocks the pointer."; return false; }
            problem = TexturePaintStroke.Problem(target, section);
            if (!string.IsNullOrEmpty(problem)) { problem = $"Section {section}: {problem}"; return false; }
            problem = null;
            var paint = target.PaintForSection(section);
            if (choices != null && choices.TryGetValue(section, out var remembered))
            {
                surface = FindSurface(paint, remembered);
                if (surface >= 0) return true;
                problem = $"Section {section}'s remembered surface is unavailable. Select that section and choose a surface in the Workspace.";
                return false;
            }
            var source = target.PaintForSection(atlas.MaterialSection);
            if (currentSurface == 0) { surface = 0; return true; }
            if (source != null && currentSurface > 0 && currentSurface < source.SurfaceCount)
            {
                surface = FindSurface(paint, source.SurfaceName(currentSurface));
                if (surface >= 0) return true;
            }
            problem = $"Section {section} has different surfaces. Select that section and choose a surface in the Workspace.";
            return false;
        }

        private static int FindSurface(TexturePaintAsset paint, string name)
        {
            if (name == null) return 0;
            var match = -1;
            for (var i = 1; i < paint.SurfaceCount; i++)
                if (string.Equals(paint.SurfaceName(i), name, StringComparison.Ordinal))
                { if (match >= 0) return -1; match = i; }
            return match;
        }

        private static void RememberSurface(int section, int surface)
        {
            var paint = Target == null ? null : Target.PaintForSection(section);
            if (paint == null || surface < 0 || surface >= paint.SurfaceCount) { SurfaceChoices.Remove(section); return; }
            SurfaceChoices[section] = surface == 0 ? null : paint.SurfaceName(surface);
        }

        private TexturePaintAtlas Atlas(int section)
        {
            var paint = Target.PaintForSection(section);
            if (!_atlases.TryGetValue(section, out var atlas) || atlas.Mesh != paint.Mesh || atlas.Resolution != paint.Mask.width)
            {
                // Keep the current atlas and one recent destination, not a full-resolution atlas for every section.
                if (_atlases.Count >= 2)
                {
                    var remove = -1;
                    foreach (var key in _atlases.Keys)
                        if (_atlas == null || key != _atlas.MaterialSection) { remove = key; break; }
                    if (remove >= 0) _atlases.Remove(remove);
                }
                _atlases[section] = atlas = new TexturePaintAtlas(paint.Mesh, section, paint.Mask.width);
            }
            return atlas;
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

        internal static int Fill(TexturePaintTarget target, int surface, int? materialSection = null)
        {
            var section = materialSection ?? (IsActiveFor(target) ? Section : target.MaterialSection);
            var problem = TexturePaintStroke.Problem(target, section);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            TextureWeatheringSession.CancelFor(target);
            FinishCurrentStroke();
            var stroke = new TexturePaintStroke();
            var paint = target.PaintForSection(section);
            var atlas = IsActiveFor(target) ? _active.Atlas(section) : new TexturePaintAtlas(paint.Mesh, section, paint.Mask.width);
            stroke.Begin(target, atlas, surface, false, Command(), materialSection: section);
            stroke.Dab(Vector3.zero, Vector3.up, true);
            var count = stroke.End();
            LastStroke = $"Fill · Section {section}: {count:N0} mask texels changed.";
            LastPaint = paint;
            Changed?.Invoke();
            SceneView.RepaintAll();
            return count;
        }

        public override void OnActivated()
        {
            _atlases.Clear();
            _atlas = Atlas(Section);
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
            _atlases.Clear(); SurfaceChoices.Clear(); PointerStatus = null;
            _active = null;
            Target = null;
            Changed?.Invoke();
        }

        private void CheckFocus()
        {
            if (_view != null && EditorWindow.focusedWindow != _view) Finish();
            if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target, Section))) Stop();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) => Stop();
        private void OnUndo()
        {
            Finish();
            if (Target != null)
            {
                if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target, Section))) Section = Target.MaterialSection;
                if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target, Section))) { Stop(); return; }
                Surface = SurfaceChoices.TryGetValue(Section, out var remembered) ? Mathf.Max(0, FindSurface(Paint, remembered)) : 0;
                RememberSurface(Section, Surface);
                _atlases.Clear(); _atlas = Atlas(Section);
            }
            PointerStatus = null;
            LastStroke = null;
            Changed?.Invoke();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view) || _atlas == null) return;
            if (!string.IsNullOrEmpty(TexturePaintStroke.Problem(Target, Section))) { Finish(); return; }
            if (TextureWeatheringSession.BlocksPaintInput) return;
            var e = Event.current;
            if (_stroke.Active && _view == view
                && (!_strokeView.Matches(view.camera) || Target.transform.localToWorldMatrix != _strokeTransform)) Finish();
            var id = GUIUtility.GetControlID(0x53425458, FocusType.Passive);
            if (e.type == EventType.Layout && !e.alt && !EditorGUIUtility.editingTextField) HandleUtility.AddDefaultControl(id);
            if (EditorWindow.focusedWindow != view || EditorGUIUtility.editingTextField)
            {
                if (_view == view) Finish();
                return;
            }
            if (e.alt || e.type == EventType.MouseLeaveWindow || e.type == EventType.Ignore) { Finish(); return; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Finish(); Stop(); e.Use(); return; }
            var hasHit = ResolveHit(Target, _atlas, HandleUtility.GUIPointToWorldRay(e.mousePosition),
                AutomaticSections && !_stroke.Active, Section, Surface, SurfaceChoices, out var hit, out var hitSurface, out var problem);
            if (hasHit && _stroke.Active && !_stroke.AllowsHit(hit))
            { hasHit = false; problem = "This stroke stays on its starting connected part. Release to paint another part."; }
            var status = hasHit ? $"Under pointer: Section {hit.SubmeshIndex} · {Target.PaintForSection(hit.SubmeshIndex).SurfaceName(hitSurface)}" : problem;
            if (PointerStatus != status) { PointerStatus = status; Changed?.Invoke(); }
            if (e.type == EventType.Repaint && hasHit)
            {
                if (_tipCache.TryGet(SimpleBlendBrushSettings.instance.TextureBrush, out var previewTip, out _))
                {
                    TextureBrushTip.Frame(hit.Normal, _stroke.Active ? _strokeRight : view.camera.transform.right, out var right, out var up);
                    for (var i = 0; i < _outline.Length; i++)
                    {
                        var point = previewTip.Boundary(i * Mathf.PI * 2 / (_outline.Length - 1));
                        _outline[i] = hit.Point + (right * point.x + up * point.y) * Command().Radius;
                    }
                    using (new Handles.DrawingScope(Erase ? new Color(1, 0.35f, 0.3f) : Color.yellow)) Handles.DrawAAPolyLine(_outline);
                }
            }
            if (e.type == EventType.MouseMove) view.Repaint();
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && hasHit && GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id:
                    if (Section != hit.SubmeshIndex) SelectSection(Target, hit.SubmeshIndex, hitSurface);
                    _command = Command();
                    if (!_tipCache.TryGet(SimpleBlendBrushSettings.instance.TextureBrush, out _strokeTip, out var error))
                    { LastStroke = error; LastPaint = Paint; Changed?.Invoke(); e.Use(); break; }
                    _strokeRight = view.camera.transform.right;
                    _strokeView = new TexturePaintView(view.camera);
                    _strokeTransform = Target.transform.localToWorldMatrix;
                    _visibility = new TexturePaintVisibility(_atlas, _strokeView);
                    _strokePaint = Paint;
                    _stroke.Begin(Target, _atlas, Surface, Erase, _command, _strokeTip, _visibility, Section,
                        SimpleBlendBrushSettings.instance.TextureThroughPaint,
                        SimpleBlendBrushSettings.instance.TextureConnectedOnly ? hit.TriangleIndex : (int?)null);
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
                    if (hasHit && _hasPoint && Vector3.Distance(_lastPoint, hit.Point) <= _command.Radius * 100)
                    {
                        Append(hit);
                        _sampler.CatchUp(_command.Radius, _strokeTip.Spacing, _samples);
                        PaintSamples(hit);
                    }
                    Finish();
                    e.Use();
                    break;
            }
        }

        private void Append(MeshRaycastHit hit)
        {
            // Do not bridge a large jump between disconnected surfaces or create an unbounded dab list.
            if (_hasPoint && Vector3.Distance(_lastPoint, hit.Point) > _command.Radius * 100) _sampler.Reset();
            _sampler.Append(hit.Point, _command.Radius, _strokeTip.Spacing, _strokeTip.Smoothing, _samples);
            PaintSamples(hit);
            _lastPoint = hit.Point;
            _hasPoint = true;
            SceneView.RepaintAll();
        }

        private void PaintSamples(MeshRaycastHit hit)
        {
            foreach (var sample in _samples)
            {
                // Reproject resampled positions onto the actual section instead of painting through empty space.
                if (_visibility.Project(sample, out var projected) && _stroke.AllowsHit(projected)
                    && Vector3.Distance(projected.Point, sample) <= _command.Radius * 0.25f)
                    _stroke.Dab(projected.Point, projected.Normal, preferredRight: _strokeRight, upload: false);
            }
            _stroke.Upload();
        }

        private void Finish()
        {
            if (_stroke.Active)
            {
                var changed = _stroke.End();
                LastStroke = changed > 0 ? $"Last stroke · Section {Section}: {changed:N0} mask texels changed."
                    : "Last stroke: no mask texels changed. Check radius, strength/flow, selected surface and projection reach.";
                LastPaint = _strokePaint;
                Changed?.Invoke();
            }
            if (_control != 0 && GUIUtility.hotControl == _control) GUIUtility.hotControl = 0;
            _control = 0;
            _view = null;
            _sampler.Reset();
            _hasPoint = false;
            _strokeTip = null;
            _visibility = null;
            _strokePaint = null;
        }
    }
}
