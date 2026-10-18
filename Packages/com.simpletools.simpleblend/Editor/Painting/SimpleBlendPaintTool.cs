using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    [EditorTool("SimpleBlend Paint")]
    public sealed class SimpleBlendPaintTool : EditorTool
    {
        private const int ControlHash = 0x53424C44;
        private static readonly GUIContent ToolIcon = new GUIContent(
            "SB",
            "SimpleBlend Paint\nPaint, erase, smooth, fill, or sample vertex colors.");

        private readonly MeshPaintCache _cache = new MeshPaintCache();
        private readonly VertexColorPreviewRenderer _preview = new VertexColorPreviewRenderer();
        private readonly VertexPaintStrokeSession _stroke = new VertexPaintStrokeSession();
        private int _ownedControl;
        private bool _gestureErase;
        private SceneView _inputView;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (ToolIcon.image == null)
                {
                    ToolIcon.image = SimpleBlendEditorIcons.Load("Paint");
                }

                ToolIcon.text = ToolIcon.image == null ? "SB" : string.Empty;
                return ToolIcon;
            }
        }

        public override bool IsAvailable()
        {
            return TryGetData(out var data) && IsSupportedTarget(data);
        }

        public override void OnActivated()
        {
            SimpleBlendPaintInput.ClearTemporaryErase();
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged += OnSelectionChanged;
            SimpleBlendTargetLock.Changed += OnTargetLockChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            _cache.InvalidateTopology();
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            ReleaseInput();
            SimpleBlendPaintInput.ClearTemporaryErase();
            EditorApplication.projectChanged -= OnProjectChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            SimpleBlendTargetLock.Changed -= OnTargetLockChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            SimpleBlendTargetLock.Unlock();
            _cache.Dispose();
            _preview.Dispose();
            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView)) return;
            if (!TryGetData(out var data) || !IsSupportedTarget(data))
            {
                ReleaseInput();
                return;
            }

            var currentEvent = Event.current;
            var settings = SimpleBlendBrushSettings.instance;
            var controlId = GUIUtility.GetControlID(ControlHash, FocusType.Passive);
            if (currentEvent.type == EventType.Layout && !currentEvent.alt && !EditorGUIUtility.editingTextField)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            var cacheResult = _cache.Ensure(data);
            if (!cacheResult.IsValid)
            {
                ReleaseInput();
                DrawValidationMessage(data, cacheResult.Message);
                return;
            }

            _preview.Draw(_cache, data, settings.PreviewMode,
                settings.PreviewSectionOnly && settings.SubmeshMaskEnabled ? settings.SubmeshMaskIndex : -1);
            if (EditorWindow.focusedWindow != sceneView || EditorGUIUtility.editingTextField)
            {
                if (_inputView == sceneView) ReleaseInput();
                if (!(EditorWindow.focusedWindow is SceneView) || EditorGUIUtility.editingTextField)
                    SimpleBlendPaintInput.ClearTemporaryErase();
                return;
            }

            var worldRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            var hasHit = _cache.TryRaycast(worldRay, out var hit);
            if (currentEvent.type == EventType.Repaint && hasHit)
            {
                DrawBrushDisc(hit, settings);
            }

            if (currentEvent.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }

            if (currentEvent.alt)
            {
                ReleaseInput();
                SimpleBlendPaintInput.ClearTemporaryErase();
                return;
            }

            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when currentEvent.button == 0 && hasHit:
                    if (settings.Mode == VertexPaintBrushMode.Sample)
                    {
                        SampleAt(data, hit, settings);
                    }
                    else if (settings.Mode == VertexPaintBrushMode.Fill)
                    {
                        FillAt(data, hit, settings);
                    }
                    else
                    {
                        _ownedControl = controlId;
                        _inputView = sceneView;
                        GUIUtility.hotControl = controlId;
                        _gestureErase = SimpleBlendPaintInput.EraseAtStrokeStart(settings.Mode);
                        if (_stroke.Begin(data))
                        {
                            PaintAt(hit, settings);
                        }
                        else ReleaseInput();
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseDrag when currentEvent.button == 0 && GUIUtility.hotControl == controlId:
                    if (hasHit)
                    {
                        PaintAt(hit, settings);
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseUp when currentEvent.button == 0 && GUIUtility.hotControl == controlId:
                    ReleaseInput();
                    currentEvent.Use();
                    break;
                case EventType.KeyDown when currentEvent.keyCode == KeyCode.Escape:
                    ReleaseInput();
                    currentEvent.Use();
                    ToolManager.RestorePreviousTool();
                    break;
            }
        }

        internal static bool IsSupportedTarget(VertexPaintData data)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || data == null || !data.enabled
                || !data.gameObject.activeInHierarchy || EditorUtility.IsPersistent(data)
                || data.SourceMesh == null || data.ValidationFailure != VertexPaintValidationFailure.None)
            {
                return false;
            }

            if (data.GetComponent<SkinnedMeshRenderer>() != null)
            {
                return data.StorageMode == VertexPaintStorageMode.ClonedMesh;
            }

            return data.GetComponent<MeshRenderer>() != null
                   && data.GetComponent<MeshFilter>() != null;
        }

        private void PaintAt(MeshRaycastHit hit, SimpleBlendBrushSettings settings)
        {
            _stroke.Paint(_cache, settings.CreateCommand(hit.Point, _gestureErase ? VertexPaintBrushMode.Erase : (VertexPaintBrushMode?)null));
        }

        private void FillAt(
            VertexPaintData data,
            MeshRaycastHit hit,
            SimpleBlendBrushSettings settings)
        {
            if (!_stroke.Begin(data))
            {
                return;
            }

            var hitSubmesh = settings.FillScope == VertexPaintFillScope.HitSubmesh
                ? hit.SubmeshIndex
                : -1;
            _stroke.Fill(_cache, settings.CreateCommand(hit.Point), hitSubmesh);
            FinishStroke();
        }

        private void SampleAt(
            VertexPaintData data,
            MeshRaycastHit hit,
            SimpleBlendBrushSettings settings)
        {
            var sampled = VertexPaintColorSampler.SampleTriangle(
                data.SerializedColors,
                _cache.Snapshot.Triangles,
                hit.TriangleIndex,
                hit.BarycentricCoordinate);
            settings.SetSampledColor(sampled);
        }

        private void FinishStroke()
        {
            _stroke.End();
        }

        private void ReleaseInput()
        {
            if (_ownedControl != 0 && GUIUtility.hotControl == _ownedControl) GUIUtility.hotControl = 0;
            _ownedControl = 0;
            _inputView = null;
            FinishStroke();
            _gestureErase = false;
        }

        internal static void UnlockTarget()
        {
            if (ToolManager.activeToolType == typeof(SimpleBlendPaintTool)) ToolManager.RestorePreviousTool();
            SimpleBlendTargetLock.Unlock();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            ReleaseInput();
            SimpleBlendPaintInput.ClearTemporaryErase();
            UnlockTarget();
        }

        private void OnEditorUpdate()
        {
            if (ShouldReleaseForFocusLoss(_ownedControl != 0, _inputView != null && EditorWindow.focusedWindow == _inputView))
                ReleaseInput();
        }

        internal static bool ShouldReleaseForFocusLoss(bool ownsGesture, bool ownerFocused) => ownsGesture && !ownerFocused;

        private void DrawBrushDisc(MeshRaycastHit hit, SimpleBlendBrushSettings settings)
        {
            var radius = settings.Mode == VertexPaintBrushMode.Fill
                         || settings.Mode == VertexPaintBrushMode.Sample
                ? HandleUtility.GetHandleSize(hit.Point) * 0.08f
                : settings.Radius;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            using (new Handles.DrawingScope(_stroke.IsActive
                       ? new Color(1f, 0.18f, 0.12f, 1f)
                       : new Color(1f, 0.72f, 0.15f, 1f)))
            {
                Handles.DrawWireDisc(hit.Point, hit.Normal, radius, 2f);
                Handles.DrawLine(
                    hit.Point,
                    hit.Point + hit.Normal * Mathf.Min(radius * 0.25f, 0.25f),
                    2f);
            }

            Handles.zTest = previousZTest;
        }

        private static void DrawValidationMessage(VertexPaintData data, string message)
        {
            if (Event.current.type != EventType.Repaint || data == null)
            {
                return;
            }

            Handles.Label(data.transform.position, $"SimpleBlend: {message}");
        }

        private bool TryGetData(out VertexPaintData data)
        {
            data = SimpleBlendTargetLock.Resolve(Selection.objects);
            return data != null;
        }

        private void OnProjectChanged()
        {
            _cache.InvalidateTopology();
            SceneView.RepaintAll();
        }

        private void OnSelectionChanged()
        {
            if (SimpleBlendTargetLock.IsLocked) return;
            OnTargetLockChanged();
        }

        private void OnTargetLockChanged()
        {
            ReleaseInput();
            SimpleBlendPaintInput.ClearTemporaryErase();
            _cache.Dispose();
            _preview.Dispose();
            SceneView.RepaintAll();
        }
    }
}
