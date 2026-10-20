using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class VertexPaintStrokeResult
    {
        internal int ChangedVertexCount { get; }
        internal IReadOnlyList<int> Sections { get; }
        internal bool NoRadiusCoverage { get; }

        private VertexPaintStrokeResult(int count, int[] sections, bool noRadiusCoverage)
        {
            ChangedVertexCount = count;
            Sections = Array.AsReadOnly(sections);
            NoRadiusCoverage = noRadiusCoverage;
        }

        internal static VertexPaintStrokeResult Create(Color32[] baseline, Color32[] colors,
            HashSet<int> touched, VertexPaintTopology topology, bool noRadiusCoverage = false)
        {
            if (baseline == null || colors == null || baseline.Length != colors.Length
                || topology == null || topology.VertexCount != colors.Length) return null;
            var changed = new HashSet<int>();
            foreach (var index in touched)
                if (!baseline[index].Equals(colors[index])) changed.Add(index);

            var sections = new List<int>();
            if (changed.Count != 0 && topology != null)
            {
                for (var section = 0; section < topology.SubmeshCount; section++)
                {
                    foreach (var index in topology.GetSubmeshVertices(section))
                    {
                        if (!changed.Contains(index)) continue;
                        sections.Add(section);
                        break;
                    }
                }
            }
            return new VertexPaintStrokeResult(changed.Count, sections.ToArray(), noRadiusCoverage && changed.Count == 0);
        }
    }
}
