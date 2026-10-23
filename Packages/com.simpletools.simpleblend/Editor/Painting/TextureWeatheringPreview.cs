using System;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    /// <summary>A detached CPU preview. Only Apply writes paint, registers Undo or queues saving.</summary>
    internal sealed class TextureWeatheringPreview : IDisposable
    {
        private readonly TexturePaintTarget _target;
        private readonly TexturePaintAsset _paint;
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly Texture2D _mask;
        private readonly TexturePaintAtlas _atlas;
        private readonly Color32[] _baseline;
        private readonly string[] _names;
        private readonly string _materialState;
        private readonly ulong _geometry;
        private readonly int _section, _surface, _resolution;
        private readonly bool _erase;
        private Color32[] _preview;
        private bool _closed;
        internal int ChangedTexels { get; private set; }
        internal TexturePaintAsset Paint => _paint;
        internal int Section => _section;

        internal TextureWeatheringPreview(TexturePaintTarget target, int surface, bool erase, int? materialSection = null)
        {
            var problem = TexturePaintStroke.Problem(target, materialSection);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            _section = materialSection ?? target.MaterialSection;
            _target = target; _paint = target.PaintForSection(_section);
            if (surface < 0 || surface >= _paint.SurfaceCount) throw new ArgumentOutOfRangeException(nameof(surface));
            _mesh = _paint.Mesh; _material = _paint.Material; _mask = _paint.Mask;
            _surface = surface; _erase = erase;
            _resolution = _mask.width;
            _baseline = (Color32[])_paint.Pixels.Clone(); _materialState = EditorJsonUtility.ToJson(_material);
            _names = new string[_paint.SurfaceCount];
            for (var i = 0; i < _names.Length; i++) _names[i] = _paint.SurfaceName(i);
            _geometry = GeometryKey(_mesh);
            _atlas = new TexturePaintAtlas(_mesh, _section, _mask.width);
        }

        internal void Generate(TextureWeatheringSettings settings, Func<bool> cancelled = null, Action<int, int> progress = null)
        {
            Validate();
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var snapshot = settings.Snapshot();
            progress?.Invoke(0, _atlas.Pixels.Count);
            TextureWeatheringEdges.CheckCancellation(cancelled);
            var edges = snapshot.Source == TextureWeatheringSource.Noise ? null : new TextureWeatheringEdges(_mesh, _section, snapshot.Source, cancelled);
            var pixels = (Color32[])_baseline.Clone(); var influences = new float[pixels.Length]; var changed = 0;
            var target = _surface == 1 ? new Color32(255, 0, 0, 0) : _surface == 2 ? new Color32(0, 255, 0, 0)
                : _surface == 3 ? new Color32(0, 0, 255, 0) : new Color32();
            var channel = _surface == 1 ? VertexPaintChannel.Red : _surface == 2 ? VertexPaintChannel.Green
                : _surface == 3 ? VertexPaintChannel.Blue : VertexPaintChannel.RGB;
            for (var i = 0; i < _atlas.Pixels.Count; i++)
            {
                if ((i & 255) == 0) { progress?.Invoke(i, _atlas.Pixels.Count); TextureWeatheringEdges.CheckCancellation(cancelled); }
                var pixel = _atlas.Pixel(i);
                if (_atlas.Coverage[pixel] != pixel) continue;
                var point = _atlas.Positions[pixel];
                var distance = edges == null ? 0 : edges.Distance(point, snapshot.Width);
                var weight = TextureWeatheringMask.Evaluate(point, snapshot, distance);
                if (weight <= 0) continue;
                influences[pixel] = weight;
                pixels[pixel] = VertexSurfaceWeightUtility.Blend(_baseline[pixel], target, channel, weight, _paint.WeightChannels, _erase);
                if (!pixels[pixel].Equals(_baseline[pixel])) changed++;
            }
            foreach (var pixel in _atlas.Pixels)
            {
                var source = _atlas.Coverage[pixel];
                if (source != pixel && !pixels[source].Equals(_baseline[source]))
                    pixels[pixel] = VertexSurfaceWeightUtility.Blend(_baseline[pixel], target, channel, influences[source], _paint.WeightChannels, _erase);
            }
            TextureWeatheringEdges.CheckCancellation(cancelled);
            progress?.Invoke(_atlas.Pixels.Count, _atlas.Pixels.Count);
            _preview = pixels; ChangedTexels = changed;
        }

        internal void CheckCurrent() => Validate();

        internal Color32[] CopyPixels()
        {
            if (_closed || _preview == null) throw new InvalidOperationException("Generate a weathering preview first.");
            return (Color32[])_preview.Clone();
        }

        internal int Apply()
        {
            SimpleBlendTexturePaintTool.FinishCurrentStroke();
            Validate();
            if (_preview == null) throw new InvalidOperationException("Generate a weathering preview before applying.");
            var changed = ChangedTexels;
            // A no-op must not create Undo entries or repair unrelated pre-existing gutter bytes.
            if (changed > 0)
            {
                Undo.IncrementCurrentGroup(); var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply texture weathering"); Undo.RegisterCompleteObjectUndo(_paint, "Apply texture weathering");
                Array.Copy(_preview, _paint.Pixels, _preview.Length); _paint.CommitPixels();
                EditorUtility.SetDirty(_paint); EditorUtility.SetDirty(_mask);
                Undo.CollapseUndoOperations(group); TexturePaintSaveQueue.Enqueue(_paint); SceneView.RepaintAll();
            }
            Dispose(); return changed;
        }

        private void Validate()
        {
            if (_closed) throw new InvalidOperationException("This weathering preview is closed. Create a new preview.");
            var problem = TexturePaintStroke.Problem(_target, _section);
            var valid = string.IsNullOrEmpty(problem) && _target.PaintForSection(_section) == _paint
                && _paint.Mesh == _mesh && _paint.Material == _material && _paint.Mask == _mask && _paint.SurfaceCount == _names.Length
                && _mask.width == _resolution && _mask.height == _resolution && _paint.Pixels.Length == _baseline.Length;
            if (valid)
            {
                for (var i = 0; i < _names.Length; i++) if (_paint.SurfaceName(i) != _names[i]) { valid = false; break; }
                for (var i = 0; valid && i < _baseline.Length; i++) if (!_baseline[i].Equals(_paint.Pixels[i])) valid = false;
                valid &= EditorJsonUtility.ToJson(_material) == _materialState && GeometryKey(_mesh) == _geometry;
            }
            if (!valid) throw new InvalidOperationException("Paint, surfaces, geometry or target changed. Cancel and create a fresh weathering preview before applying.");
        }

        internal void Cancel() => Dispose();
        public void Dispose() { _preview = null; _closed = true; ChangedTexels = 0; }

        private static ulong GeometryKey(Mesh mesh)
        {
            var hash = 14695981039346656037ul;
            void Add(int value) { unchecked { hash = (hash ^ (uint)value) * 1099511628211ul; } }
            void AddFloat(float value) => Add(BitConverter.SingleToInt32Bits(value));
            var vertices = mesh.vertices; Add(vertices.Length);
            foreach (var point in vertices) { AddFloat(point.x); AddFloat(point.y); AddFloat(point.z); }
            foreach (var coordinates in new[] { mesh.uv, mesh.uv4 })
            { Add(coordinates.Length); foreach (var uv in coordinates) { AddFloat(uv.x); AddFloat(uv.y); } }
            Add(mesh.subMeshCount);
            for (var section = 0; section < mesh.subMeshCount; section++)
            { Add((int)mesh.GetTopology(section)); var indices = mesh.GetIndices(section); Add(indices.Length); foreach (var index in indices) Add(index); }
            return hash;
        }
    }
}
