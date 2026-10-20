using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    [Serializable]
    internal sealed class SurfaceDefinition
    {
        [SerializeField] internal string Name;
        [SerializeField] internal Color Tint = Color.white;
        [SerializeField] internal Texture2D Albedo;
        [SerializeField] internal Texture2D Normal;
        [SerializeField] internal Texture2D Mask;
        [SerializeField] internal Texture2D Height;
    }

    internal static class SurfacePaletteAuthoring
    {
        internal static SurfacePalette Create(string palettePath, SurfaceDefinition[] surfaces)
        {
            if (surfaces == null || (surfaces.Length != 3 && surfaces.Length != 4))
                throw new ArgumentException("Choose three or four surfaces, including Base.");
            var names = new string[surfaces.Length];
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                if (surface == null || string.IsNullOrWhiteSpace(surface.Name) || surface.Name.Trim().Length > 64)
                    throw new ArgumentException("Give every surface a name of 1–64 characters.");
                names[i] = surface.Name.Trim();
                if (!unique.Add(names[i])) throw new ArgumentException("Surface names must be distinct.");
                if (!Finite(surface.Tint.r) || !Finite(surface.Tint.g) || !Finite(surface.Tint.b) || !Finite(surface.Tint.a))
                    throw new ArgumentException("Surface tints must contain finite values.");
                foreach (var texture in new[] { surface.Albedo, surface.Normal, surface.Mask, surface.Height })
                    if (texture != null && !AssetDatabase.Contains(texture))
                        throw new ArgumentException("Choose saved texture assets so the palette can be reopened and shared.");
            }
            ValidateNewPath(palettePath, ".asset");
            var materialPath = Path.ChangeExtension(palettePath, ".mat").Replace('\\', '/');
            ValidateNewPath(materialPath, ".mat");
            var shader = Shader.Find(surfaces.Length == 3
                ? SimpleBlendLayerBlendContract.LayerBlend3ShaderName : SimpleBlendLayerBlendContract.LayerBlend4ShaderName);
            if (shader == null) throw new InvalidOperationException("The SimpleBlend layer shader is unavailable. Check the package import first.");

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
            var palette = ScriptableObject.CreateInstance<SurfacePalette>();
            palette.name = Path.GetFileNameWithoutExtension(palettePath);
            try
            {
                material.SetFloat("_LayerCount", surfaces.Length);
                var hasHeight = false;
                foreach (var surface in surfaces) hasHeight |= surface.Height != null;
                material.SetFloat("_HeightBlendStrength", hasHeight ? 1 : 0);
                for (var i = 0; i < surfaces.Length; i++)
                {
                    var prefix = $"_Layer{i}";
                    material.SetColor(prefix + "Color", surfaces[i].Tint);
                    SetTexture(material, prefix + "Albedo", surfaces[i].Albedo);
                    SetTexture(material, prefix + "Normal", surfaces[i].Normal);
                    SetTexture(material, prefix + "Mask", surfaces[i].Mask);
                    SetTexture(material, prefix + "Height", surfaces[i].Height);
                }
                palette.Configure(material, names);
                // Recheck immediately before creation; never use CreateAsset as an overwrite operation.
                ValidateNewPath(materialPath, ".mat");
                AssetDatabase.CreateAsset(material, materialPath);
                if (AssetDatabase.LoadMainAssetAtPath(materialPath) != material)
                    throw new InvalidOperationException("The new material could not be saved. Check the destination's write permissions.");
                ValidateNewPath(palettePath, ".asset");
                AssetDatabase.CreateAsset(palette, palettePath);
                if (AssetDatabase.LoadMainAssetAtPath(palettePath) != palette)
                    throw new InvalidOperationException("The new palette could not be saved. Check the destination's write permissions.");
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.SaveAssetIfDirty(palette);
                return palette;
            }
            catch
            {
                RemoveCreatedAsset(palettePath, palette);
                RemoveCreatedAsset(materialPath, material);
                throw;
            }
        }

        internal static string AssignmentProblem(Renderer renderer, int slot, SurfacePalette palette)
        {
            if (palette == null)
                return "Choose a palette with a SimpleBlend Layer Blend 3 or 4 material.";
            if (!string.IsNullOrEmpty(palette.Problem)) return palette.Problem;
            if (!AssetDatabase.Contains(palette) || !AssetDatabase.Contains(palette.Material))
                return "Save the palette and its material as assets before assignment.";
            if (renderer == null) return "Select a paintable renderer first.";
            var target = PaintWorkspaceTarget.Inspect(new Object[] { renderer });
            if (!target.CanPaint) return target.Message;
            if (slot < 0 || slot >= target.Mesh.subMeshCount) return "Choose an existing mesh section.";
            return string.Empty;
        }

        internal static void Assign(Renderer renderer, int slot, SurfacePalette palette)
        {
            var problem = AssignmentProblem(renderer, slot, palette);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            var materials = renderer.sharedMaterials;
            if (slot < materials.Length && materials[slot] == palette.Material) return;
            if (materials.Length <= slot) Array.Resize(ref materials, slot + 1);
            materials[slot] = palette.Material;
            Undo.RegisterCompleteObjectUndo(renderer, "Assign SimpleBlend Surface Material");
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }

        internal static bool IsAssigned(Renderer renderer, int slot, SurfacePalette palette)
        {
            if (renderer == null || palette == null || !string.IsNullOrEmpty(palette.Problem) || slot < 0) return false;
            var materials = renderer.sharedMaterials;
            return slot < materials.Length && materials[slot] == palette.Material;
        }

        internal static void SetHeightContrast(SurfacePalette palette, bool enabled)
        {
            if (palette == null || !string.IsNullOrEmpty(palette.Problem))
                throw new ArgumentException("Choose a valid surface palette first.");
            var material = palette.Material;
            var value = enabled ? 1f : 0f;
            if (material.GetFloat("_HeightBlendStrength") == value) return;
            Undo.RegisterCompleteObjectUndo(material, "Change SimpleBlend Surface Blending");
            material.SetFloat("_HeightBlendStrength", value);
            EditorUtility.SetDirty(material);
        }

        internal static void SelectSurface(SurfacePalette palette, int surface, int section, SimpleBlendBrushSettings settings)
        {
            if (palette == null || !string.IsNullOrEmpty(palette.Problem) || surface < 0 || surface >= palette.SurfaceCount || section < 0)
                throw new ArgumentOutOfRangeException(nameof(surface), "Choose a valid surface and mesh section.");
            settings.Channel = ChannelFor(surface);
            settings.SurfaceWeightChannels = palette.SurfaceCount - 1;
            settings.Color = surface == 0 ? new Color(0, 0, 0, 1)
                : surface == 1 ? Color.red : surface == 2 ? Color.green : Color.blue;
            settings.Mode = VertexPaintBrushMode.Paint;
            settings.PreviewMode = VertexPaintPreviewMode.Material;
            settings.PreviewSectionOnly = true;
            settings.SubmeshMaskEnabled = true;
            settings.SubmeshMaskIndex = section;
        }

        internal static bool Matches(SurfacePalette palette, int surface, int section, SimpleBlendBrushSettings settings)
        {
            if (palette == null || !string.IsNullOrEmpty(palette.Problem) || surface < 0 || surface >= palette.SurfaceCount
                || settings.SurfaceWeightChannels != palette.SurfaceCount - 1 || !settings.SubmeshMaskEnabled
                || settings.SubmeshMaskIndex != section || settings.Channel != ChannelFor(surface)) return false;
            if (settings.Mode != VertexPaintBrushMode.Paint && settings.Mode != VertexPaintBrushMode.Fill) return true;
            var color = settings.Color;
            return surface == 0 ? color.r == 0 && color.g == 0 && color.b == 0
                : surface == 1 ? color.r == 1 && color.g == 0 && color.b == 0
                : surface == 2 ? color.r == 0 && color.g == 1 && color.b == 0
                : color.r == 0 && color.g == 0 && color.b == 1;
        }

        private static VertexPaintChannel ChannelFor(int surface)
        {
            switch (surface)
            {
                case 0: return VertexPaintChannel.RGB;
                case 1: return VertexPaintChannel.Red;
                case 2: return VertexPaintChannel.Green;
                case 3: return VertexPaintChannel.Blue;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateNewPath(string path, string extension)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.Contains("\\") || path.Contains("..") || !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')))
                throw new ArgumentException("Choose a new asset path in an existing folder inside Assets.");
            if (File.Exists(path) || Directory.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException($"An asset already exists at {path}. Choose another name; nothing is overwritten.");
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (texture != null) material.SetTexture(property, texture);
        }

        private static void RemoveCreatedAsset(string path, Object created)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == created && created != null)
                AssetDatabase.DeleteAsset(path);
            else if (created != null && !AssetDatabase.Contains(created)) Object.DestroyImmediate(created);
        }
    }
}
