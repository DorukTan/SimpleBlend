using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [InitializeOnLoad]
    internal static class SimpleBlendPaintFeedback
    {
        private static VertexPaintData _target;
        private static int _section = -1;
        private static string _hover;
        private static string _result;
        private static string _stroke;
        private static readonly List<Material> Materials = new List<Material>();
        internal static event Action Changed;

        static SimpleBlendPaintFeedback()
        {
            Undo.undoRedoPerformed += Clear;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        internal static void Clear()
        {
            if (ReferenceEquals(_target, null) && _hover == null && _result == null && _stroke == null) return;
            _target = null;
            _section = -1;
            _hover = null;
            _result = null;
            _stroke = null;
            Materials.Clear();
            Notify();
        }

        internal static void SetHover(VertexPaintData data, int section)
        {
            if (data == null) { Clear(); return; }
            var changedTarget = _target != data;
            var hover = section < 0 ? "Point at the paint target to identify a section." : DescribeSection(data, section);
            if (!changedTarget && section == _section && _hover == hover) return;
            if (changedTarget) { _result = null; _stroke = null; }
            _target = data;
            _section = section;
            _hover = hover;
            Notify();
        }

        internal static void SetResult(VertexPaintData data, VertexPaintStrokeResult result)
        {
            if (data == null) return;
            if (result == null)
            {
                if (_target == data && _stroke != null) { _stroke = null; Notify(); }
                return;
            }
            if (_target != data) { _hover = null; _section = -1; }
            _target = data;
            _stroke = null;
            _result = DescribeResult(result);
            Notify();
        }

        internal static void SetActive(VertexPaintData data, VertexBrushCommand command)
        {
            if (data == null) return;
            var text = $"Current stroke: {command.Mode} · {command.Channel}";
            if (_target == data && _stroke == text) return;
            if (_target != data) { _hover = null; _section = -1; }
            _target = data;
            _result = null;
            _stroke = text;
            Notify();
        }

        internal static string DescribeSection(VertexPaintData data, int section)
        {
            if (data == null || data.SourceMesh == null || section < 0 || section >= data.SourceMesh.subMeshCount)
                return "Section unavailable.";
            var renderer = data.GetComponent<Renderer>();
            if (renderer == null) return "Section unavailable.";
            renderer.GetSharedMaterials(Materials);
            var label = section >= Materials.Count ? "no matching material slot"
                : Materials[section] == null ? "unassigned material slot" : Materials[section].name;
            var description = $"Under pointer: section {section} · {label}";
            Materials.Clear();
            return description;
        }

        internal static string DescribeBrush(SimpleBlendBrushSettings settings)
        {
            if (settings.Mode == VertexPaintBrushMode.Sample) return "Brush: Sample · RGBA · hit triangle (brush masks do not apply)";
            var scope = settings.Mode == VertexPaintBrushMode.Fill
                ? settings.FillScope == VertexPaintFillScope.HitSubmesh ? "hit section" : "whole mesh"
                : "brush radius";
            var channel = settings.Channel == VertexPaintChannel.Alpha ? "Alpha (stored vertex channel)" : settings.Channel.ToString();
            var summary = $"Brush: {settings.Mode} · {channel} · {scope}";
            if (settings.SubmeshMaskEnabled)
                summary += $" · section mask {settings.SubmeshMaskIndex}\nShared vertices can also affect neighboring sections.";
            return summary;
        }

        internal static string DescribeResult(VertexPaintStrokeResult result)
        {
            if (result.NoRadiusCoverage)
                return "Last stroke: no vertex colors changed — no vertices were inside the brush radius. Increase Brush size (m), or open Mesh detail to make a more detailed paint copy. Fill can work here because it ignores brush radius.";
            if (result.ChangedVertexCount == 0)
                return "Last stroke: no vertex colors changed. Check brush reach, mesh detail, masks, strength/flow or existing values.";
            var text = new StringBuilder($"Last stroke: {result.ChangedVertexCount:N0} distinct vertices changed");
            if (result.Sections.Count > 0)
            {
                text.Append(" · sections ");
                for (var index = 0; index < Math.Min(8, result.Sections.Count); index++)
                {
                    if (index > 0) text.Append(", ");
                    text.Append(result.Sections[index]);
                }
                if (result.Sections.Count > 8) text.Append($" (+{result.Sections.Count - 8} more)");
            }
            return text.ToString();
        }

        internal static string GetMessage(VertexPaintData data, SimpleBlendBrushSettings settings)
        {
            var text = DescribeBrush(settings);
            if (data == null || _target != data)
                return text + "\nStart painting in the Scene view to see section context.";
            text += "\n" + (_hover ?? "Point at the paint target to identify a section.");
            if (_stroke != null) text += "\n" + _stroke;
            if (_result != null) text += "\n" + _result;
            return text;
        }

        internal static void Draw(VertexPaintData data)
        {
            EditorGUILayout.HelpBox(GetMessage(data, SimpleBlendBrushSettings.instance), MessageType.None);
        }

        private static void Notify()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }
    }
}
