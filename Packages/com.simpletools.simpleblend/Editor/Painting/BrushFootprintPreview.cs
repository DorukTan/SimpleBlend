using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal readonly struct BrushInfluencePoint
    {
        internal readonly Vector3 Position;
        internal readonly float Weight;
        internal BrushInfluencePoint(Vector3 position, float weight) { Position = position; Weight = weight; }
    }

    internal sealed class BrushFootprintPreview
    {
        private readonly List<int> _candidates = new List<int>();
        private readonly List<BrushInfluencePoint> _points = new List<BrushInfluencePoint>();
        internal IReadOnlyList<BrushInfluencePoint> Points => _points;
        internal bool IsComplete { get; private set; }

        internal void Evaluate(MeshPaintCache cache, VertexPaintData data, VertexBrushCommand command,
            IReadOnlyList<Color32> maskColors = null, int markerBudget = 256)
        {
            _points.Clear();
            IsComplete = true;
            if (cache?.Snapshot == null || data == null || command.Mode == VertexPaintBrushMode.Fill
                || command.Mode == VertexPaintBrushMode.Sample) return;
            if (float.IsNaN(command.Radius) || float.IsInfinity(command.Radius)) { IsComplete = false; return; }
            var colors = maskColors ?? data.SerializedColors;
            if (colors.Count != cache.WorldVertices.Count) return;
            IsComplete = cache.QueryPreviewVertices(command.Center, command.Radius, _candidates);
            foreach (var index in _candidates)
            {
                if (!command.Mask.Allows(index, cache.WorldNormals[index], colors[index], cache.Snapshot.Topology)) continue;
                var weight = command.EvaluateWeight(Vector3.Distance(cache.WorldVertices[index], command.Center));
                if (float.IsNaN(weight) || float.IsInfinity(weight)) { IsComplete = false; continue; }
                if (weight <= 0) continue;
                if (_points.Count >= markerBudget) { IsComplete = false; continue; }
                _points.Add(new BrushInfluencePoint(cache.WorldVertices[index], weight));
            }
        }
    }
}
