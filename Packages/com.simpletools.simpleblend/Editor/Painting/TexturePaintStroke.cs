using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class TexturePaintStroke
    {
        private TexturePaintAsset _paint;
        private TexturePaintAtlas _atlas;
        private Color32[] _baseline;
        private float[] _maximum;
        private readonly List<int> _candidates = new List<int>();
        private int _surface;
        private bool _erase;
        private VertexBrushCommand _command;
        private int _undoGroup;
        private bool _recorded;
        private TextureBrushTip _tip;
        private float[] _coverage;
        private bool _needsUpload;
        private TexturePaintVisibility _visibility;
        private bool _throughPaint;
        private int _component = -1;

        internal bool Active => _paint != null;

        internal void Begin(TexturePaintTarget target, TexturePaintAtlas atlas, int surface, bool erase, VertexBrushCommand command,
            TextureBrushTip tip = null, TexturePaintVisibility visibility = null, int? materialSection = null, bool throughPaint = false,
            int? connectedTriangle = null)
        {
            if (Active) throw new InvalidOperationException("Finish the current texture stroke first.");
            var problem = Problem(target, materialSection);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            var section = materialSection ?? target.MaterialSection;
            var paint = target.PaintForSection(section);
            if (atlas == null) throw new ArgumentNullException(nameof(atlas));
            if (atlas.Mesh != paint.Mesh || atlas.MaterialSection != section || atlas.Resolution != paint.Mask.width)
                throw new ArgumentException("The paint atlas must match the prepared mesh section and mask resolution.", nameof(atlas));
            if (surface < 0 || surface >= paint.SurfaceCount) throw new ArgumentOutOfRangeException(nameof(surface));
            var component = connectedTriangle.HasValue ? atlas.ComponentForTriangle(connectedTriangle.Value) : -1;
            if (connectedTriangle.HasValue && component < 0)
                throw new ArgumentException("Start connected painting on a triangle in the prepared section.", nameof(connectedTriangle));
            _paint = paint;
            _atlas = atlas;
            _baseline = (Color32[])_paint.Pixels.Clone();
            _maximum = new float[_baseline.Length];
            _atlas.SetTransform(target.transform.localToWorldMatrix);
            _surface = surface;
            _erase = erase;
            _command = command;
            _recorded = false;
            _tip = tip;
            _coverage = command.Accumulate || (tip != null && tip.LimitOpacity) ? new float[_baseline.Length] : null;
            _needsUpload = false;
            _visibility = visibility;
            _throughPaint = throughPaint;
            _component = component;
        }

        internal bool AllowsHit(MeshRaycastHit hit) => Active && hit.SubmeshIndex == _atlas.MaterialSection
            && (_component < 0 || _atlas.ComponentForTriangle(hit.TriangleIndex) == _component);

        internal int Dab(Vector3 center, Vector3 normal, bool fill = false, Vector3? preferredRight = null, bool upload = true)
        {
            if (!Active) return 0;
            var normalLength = normal.magnitude;
            if (!fill && normalLength < 0.00001f) return 0;
            normal /= Mathf.Max(normalLength, 0.00001f);
            TextureBrushTip.Frame(normal, preferredRight ?? Vector3.right, out var right, out var up);
            var target = _surface == 1 ? new Color32(255, 0, 0, 0)
                : _surface == 2 ? new Color32(0, 255, 0, 0)
                : _surface == 3 ? new Color32(0, 0, 255, 0) : new Color32();
            var channel = _surface == 1 ? VertexPaintChannel.Red : _surface == 2 ? VertexPaintChannel.Green
                : _surface == 3 ? VertexPaintChannel.Blue : VertexPaintChannel.RGB;
            var changed = 0;
            var pixels = _paint.Pixels;
            var bounds = _tip == null ? 1.031f : Mathf.Sqrt(_tip.BoundingRadius * _tip.BoundingRadius + 0.0625f);
            if (!fill) _atlas.Query(center, _command.Radius * bounds, _candidates);
            var candidateCount = fill ? _atlas.Pixels.Count : _candidates.Count;
            for (var candidate = 0; candidate < candidateCount; candidate++)
            {
                var index = fill ? candidate : _candidates[candidate];
                var i = _atlas.Pixel(index);
                if (!fill && _component >= 0 && _atlas.ComponentAt(index) != _component) continue;
                var influence = _command.Strength * _command.Flow;
                if (!fill)
                {
                    var delta = _atlas.WorldPosition(index) - center;
                    var depth = Vector3.Dot(delta, normal);
                    if (Mathf.Abs(depth) > _command.Radius * 0.25f
                        || Vector3.Dot(_atlas.WorldNormal(index), normal) < 0.25f) continue;
                    influence = _tip == null ? _command.EvaluateWeight((delta - normal * depth).magnitude)
                        : _tip.Evaluate(new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, up)) / _command.Radius, _command.Falloff)
                            * _command.Strength * _command.Flow;
                }
                if (!float.IsFinite(influence) || influence <= 0) continue;
                if (!fill && !_throughPaint && _visibility != null && !_visibility.Allows(index)) continue;
                if (_coverage != null && (_command.Accumulate || (!fill && _tip != null && _tip.LimitOpacity)))
                {
                    var limited = !fill && _tip != null && _tip.LimitOpacity;
                    var ink = limited ? influence / Mathf.Max(_command.Strength, 0.000001f) : influence;
                    // Retain sub-byte contributions instead of repeatedly blending rounded mask bytes.
                    _coverage[i] = _command.Accumulate ? 1 - (1 - _coverage[i]) * (1 - ink) : Mathf.Max(_coverage[i], ink);
                    influence = limited ? _command.Strength * _coverage[i] : _coverage[i];
                }
                if (!_command.Accumulate && influence <= _maximum[i]) continue;
                var source = _baseline[i];
                var result = VertexSurfaceWeightUtility.Blend(source, target, channel, influence, _paint.WeightChannels, _erase);
                _maximum[i] = Mathf.Max(_maximum[i], influence);
                if (pixels[i].Equals(result)) continue;
                if (!_recorded)
                {
                    Undo.IncrementCurrentGroup();
                    _undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Paint texture surfaces");
                    Undo.RegisterCompleteObjectUndo(_paint, "Paint texture surfaces");
                    _recorded = true;
                }
                pixels[i] = result;
                changed++;
            }
            if (changed > 0)
            {
                _needsUpload = true;
                if (upload) Upload();
            }
            return changed;
        }

        internal int End()
        {
            if (!Active) return 0;
            Upload();
            var changed = 0;
            for (var i = 0; i < _baseline.Length; i++)
                if (!_baseline[i].Equals(_paint.Pixels[i]) && _atlas.Coverage[i] == i) changed++;
            if (_recorded) Undo.CollapseUndoOperations(_undoGroup);
            if (_recorded) TexturePaintSaveQueue.Enqueue(_paint);
            _paint = null;
            _atlas = null;
            _baseline = null;
            _maximum = null;
            _coverage = null;
            _tip = null;
            _visibility = null;
            _throughPaint = false;
            _component = -1;
            return changed;
        }

        internal void Upload()
        {
            if (!Active || !_needsUpload) return;
            _paint.CommitPixels();
            EditorUtility.SetDirty(_paint);
            EditorUtility.SetDirty(_paint.Mask);
            _needsUpload = false;
        }

        internal static string Problem(TexturePaintTarget target, int? materialSection = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Leave Play mode before painting.";
            if (target == null || !target.gameObject.activeInHierarchy || EditorUtility.IsPersistent(target)) return "Select an active scene texture paint object.";
            var section = materialSection ?? target.MaterialSection;
            var paint = target.PaintForSection(section);
            if (paint == null) return "Prepare this material section before painting it.";
            if (paint.Mesh == null || paint.Mask == null || paint.Material == null) return "The texture paint asset is incomplete. Restore its missing references.";
            if (!paint.Mesh.isReadable || !paint.Mask.isReadable || paint.Pixels.Length != paint.Mask.width * paint.Mask.height)
                return "The paint mesh and mask must remain readable with matching stored pixels.";
            if (paint.SurfaceCount < 1 || paint.SurfaceCount > 4) return "The paint asset needs one to four surfaces, including Base.";
            if (target.GetComponent<MeshFilter>()?.sharedMesh != paint.Mesh) return "Restore this object's owned paint mesh before painting.";
            var renderer = target.GetComponent<MeshRenderer>();
            var materials = renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials;
            if (renderer == null || !renderer.enabled || section < 0 || section >= paint.Mesh.subMeshCount
                || section >= materials.Length || materials[section] != paint.Material)
                return "Enable the renderer and restore its painted material slot before painting.";
            if (!paint.Material.HasProperty("_UseTextureWeights") || paint.Material.GetFloat("_UseTextureWeights") < 0.5f
                || paint.Material.GetTexture("_BlendMask") != paint.Mask) return "Restore the painted material's texture-weight mode and blend mask.";
            return string.Empty;
        }
    }

    [InitializeOnLoad]
    internal static class TexturePaintUndo
    {
        static TexturePaintUndo() => Undo.undoRedoPerformed += RebuildLoadedMasks;

        internal static void RebuildLoadedMasks()
        {
            foreach (var paint in Resources.FindObjectsOfTypeAll<TexturePaintAsset>())
            {
                if (paint.RebuildMask())
                {
                    EditorUtility.SetDirty(paint);
                    if (paint.Mask != null) EditorUtility.SetDirty(paint.Mask);
                }
                if (EditorUtility.IsDirty(paint) || (paint.Mask != null && EditorUtility.IsDirty(paint.Mask))
                    || (paint.Material != null && EditorUtility.IsDirty(paint.Material))) TexturePaintSaveQueue.Enqueue(paint);
            }
            SceneView.RepaintAll();
        }
    }
}
