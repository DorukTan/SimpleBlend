using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    [EditorTool("SimpleBlend Paint", typeof(VertexPaintData))]
    public sealed class SimpleBlendPaintTool : EditorTool
    {
        private const int ControlHash = 0x53424C44;
        private static readonly GUIContent ToolIcon = new GUIContent(
            "SB",
            "SimpleBlend Paint\nPaint, erase, smooth, fill, or sample vertex colors.");

        private readonly MeshPaintCache _cache = new MeshPaintCache();
        private readonly VertexColorPreviewRenderer _preview = new VertexColorPreviewRenderer();
        private readonly VertexPaintStrokeSession _stroke = new VertexPaintStrokeSession();

        public override GUIContent toolbarIcon => ToolIcon;

        public override bool IsAvailable()
        {
            return TryGetData(out var data) && IsSupportedTarget(data);
        }

        public override void OnActivated()
        {
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged += OnSelectionChanged;
            _cache.InvalidateTopology();
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            if (_stroke.IsActive)
            {
                GUIUtility.hotControl = 0;
            }

            FinishStroke();
            EditorApplication.projectChanged -= OnProjectChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            _cache.Dispose();
            _preview.Dispose();
            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView)
                || !TryGetData(out var data)
                || !IsSupportedTarget(data))
            {
                return;
            }

            var currentEvent = Event.current;
            var settings = SimpleBlendBrushSettings.instance;
            var controlId = GUIUtility.GetControlID(ControlHash, FocusType.Passive);
            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            var cacheResult = _cache.Ensure(data);
            if (!cacheResult.IsValid)
            {
                DrawValidationMessage(data, cacheResult.Message);
                return;
            }

            _preview.Draw(_cache, data);

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
                if (_stroke.IsActive)
                {
                    GUIUtility.hotControl = 0;
                    FinishStroke();
                }

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
                        GUIUtility.hotControl = controlId;
                        if (_stroke.Begin(data))
                        {
                            PaintAt(hit, settings);
                        }
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
                    GUIUtility.hotControl = 0;
                    FinishStroke();
                    currentEvent.Use();
                    break;
                case EventType.KeyDown when currentEvent.keyCode == KeyCode.Escape && _stroke.IsActive:
                    GUIUtility.hotControl = 0;
                    FinishStroke();
                    currentEvent.Use();
                    break;
            }
        }

        internal static bool IsSupportedTarget(VertexPaintData data)
        {
            if (data == null || data.SourceMesh == null || data.ValidationFailure != VertexPaintValidationFailure.None)
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
            _stroke.Paint(_cache, settings.CreateCommand(hit.Point));
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
            data = target as VertexPaintData;
            if (data != null)
            {
                return true;
            }

            var selected = Selection.activeGameObject;
            data = selected == null ? null : selected.GetComponent<VertexPaintData>();
            return data != null;
        }

        private void OnProjectChanged()
        {
            _cache.InvalidateTopology();
            SceneView.RepaintAll();
        }

        private void OnSelectionChanged()
        {
            if (_stroke.IsActive)
            {
                GUIUtility.hotControl = 0;
            }

            FinishStroke();
            _cache.Dispose();
            _preview.Dispose();
            SceneView.RepaintAll();
        }
    }
}
