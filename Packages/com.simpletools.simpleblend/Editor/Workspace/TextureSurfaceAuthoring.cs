using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    internal sealed class TextureSurfaceSettings
    {
        internal string Name;
        internal Color Tint = Color.white;
        internal Texture Albedo, Normal, Mask, Height;
        internal Vector2 Tiling = Vector2.one, Offset;
        internal float NormalScale = 1, Metallic, Smoothness = 0.5f, Occlusion = 1;
    }

    internal static class TextureSurfaceAuthoring
    {
        internal static TextureSurfaceSettings Read(TexturePaintAsset paint, int index)
        {
            Validate(paint, index);
            var p = "_Layer" + index;
            var material = paint.Material;
            return new TextureSurfaceSettings
            {
                Name = paint.SurfaceName(index), Tint = material.GetColor(p + "Color"),
                Albedo = material.GetTexture(p + "Albedo"), Normal = material.GetTexture(p + "Normal"),
                Mask = material.GetTexture(p + "Mask"), Height = material.GetTexture(p + "Height"),
                Tiling = material.GetTextureScale(p + "Albedo"), Offset = material.GetTextureOffset(p + "Albedo"),
                NormalScale = material.GetFloat(p + "NormalScale"), Metallic = material.GetFloat(p + "Metallic"),
                Smoothness = material.GetFloat(p + "Smoothness"), Occlusion = material.GetFloat(p + "OcclusionStrength")
            };
        }

        internal static void Edit(TexturePaintAsset paint, int index, TextureSurfaceSettings surface)
        {
            Validate(paint, index);
            ValidateSettings(paint, index, surface);
            var nameChanged = surface.Name.Trim() != paint.SurfaceName(index);
            Record(paint, nameChanged, "Edit painted surface");
            var names = Names(paint);
            names[index] = surface.Name.Trim();
            paint.SetSurfaceNames(names);
            Write(paint.Material, index, surface);
            Changed(paint);
        }

        internal static int Add(TexturePaintAsset paint)
        {
            Validate(paint, 0);
            if (paint.SurfaceCount >= 4) throw new InvalidOperationException("A painted material supports up to four surfaces, including Base.");
            var names = Names(paint);
            var existingNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var newName = "Surface 1";
            for (var n = 1; existingNames.Contains(newName); n++) newName = "Surface " + (n + 1);
            var index = names.Length;
            Array.Resize(ref names, index + 1);
            names[index] = newName;
            var shader = LayerShader(names.Length);
            Record(paint, true, "Add painted surface");
            var pixels = paint.Pixels;
            for (var i = 0; i < pixels.Length; i++)
            {
                var color = pixels[i];
                if (index == 1) color.r = 0;
                else if (index == 2) color.g = 0;
                else color.b = 0;
                pixels[i] = color;
            }
            paint.SetSurfaceNames(names);
            paint.Material.shader = shader;
            paint.Material.SetFloat("_LayerCount", names.Length > 3 ? 4 : 3);
            Write(paint.Material, index, new TextureSurfaceSettings { Name = newName, Tint = new Color(0.4f, 0.65f, 0.35f) });
            paint.CommitPixels();
            Changed(paint);
            return index;
        }

        internal static void Remove(TexturePaintAsset paint, int index)
        {
            Validate(paint, index);
            if (index == 0) throw new InvalidOperationException("Base cannot be removed. Its textures and tint can be edited.");
            var names = new List<string>(Names(paint));
            names.RemoveAt(index);
            var shader = LayerShader(names.Count);
            var survivors = new List<TextureSurfaceSettings>();
            for (var i = 0; i < paint.SurfaceCount; i++) if (i != index) survivors.Add(Read(paint, i));
            Record(paint, true, "Remove painted surface");
            var pixels = paint.Pixels;
            var oldCount = paint.SurfaceCount;
            for (var i = 0; i < pixels.Length; i++) pixels[i] = RemoveWeight(pixels[i], index, oldCount);
            paint.SetSurfaceNames(names.ToArray());
            paint.Material.shader = shader;
            paint.Material.SetFloat("_LayerCount", names.Count > 3 ? 4 : 3);
            for (var i = 0; i < survivors.Count; i++) Write(paint.Material, i, survivors[i]);
            for (var i = survivors.Count; i < 4; i++) Write(paint.Material, i, new TextureSurfaceSettings());
            paint.CommitPixels();
            Changed(paint);
        }

        internal static Color32 RemoveWeight(Color32 color, int surface, int oldCount)
        {
            var result = new Color32(0, 0, 0, color.a);
            var destination = 0;
            for (var old = 1; old < oldCount; old++)
            {
                if (old == surface) continue;
                var weight = old == 1 ? color.r : old == 2 ? color.g : color.b;
                if (destination == 0) result.r = weight;
                else if (destination == 1) result.g = weight;
                else result.b = weight;
                destination++;
            }
            return result;
        }

        private static Shader LayerShader(int count)
        {
            var shader = Shader.Find(count > 3 ? SimpleBlendLayerBlendContract.LayerBlend4ShaderName : SimpleBlendLayerBlendContract.LayerBlend3ShaderName);
            if (shader == null) throw new InvalidOperationException("The SimpleBlend layer shader is unavailable.");
            return shader;
        }

        private static string[] Names(TexturePaintAsset paint)
        {
            var names = new string[paint.SurfaceCount];
            for (var i = 0; i < names.Length; i++) names[i] = paint.SurfaceName(i);
            return names;
        }

        private static void Validate(TexturePaintAsset paint, int index)
        {
            if (paint == null || paint.Material == null || paint.Mask == null || paint.SurfaceCount < 1 || paint.SurfaceCount > 4)
                throw new InvalidOperationException("Choose a complete texture paint asset.");
            if (index < 0 || index >= paint.SurfaceCount) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static void ValidateSettings(TexturePaintAsset paint, int index, TextureSurfaceSettings surface)
        {
            if (surface == null || string.IsNullOrWhiteSpace(surface.Name) || surface.Name.Trim().Length > 64)
                throw new ArgumentException("Give the surface a name of 1–64 characters.");
            for (var i = 0; i < paint.SurfaceCount; i++)
                if (i != index && string.Equals(paint.SurfaceName(i), surface.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Surface names must be distinct.");
            foreach (var texture in new[] { surface.Albedo, surface.Normal, surface.Mask, surface.Height })
                if (texture != null && (!(texture is Texture2D) || !AssetDatabase.Contains(texture)))
                    throw new ArgumentException("Choose saved 2D texture assets.");
            foreach (var value in new[] { surface.Tint.r, surface.Tint.g, surface.Tint.b, surface.Tint.a,
                surface.Tiling.x, surface.Tiling.y, surface.Offset.x, surface.Offset.y, surface.NormalScale,
                surface.Metallic, surface.Smoothness, surface.Occlusion })
                if (!float.IsFinite(value)) throw new ArgumentException("Surface settings must contain finite values.");
        }

        private static void Write(Material material, int index, TextureSurfaceSettings surface)
        {
            var p = "_Layer" + index;
            material.SetColor(p + "Color", surface.Tint);
            material.SetTexture(p + "Albedo", surface.Albedo);
            material.SetTexture(p + "Normal", surface.Normal);
            material.SetTexture(p + "Mask", surface.Mask);
            material.SetTexture(p + "Height", surface.Height);
            material.SetTextureScale(p + "Albedo", surface.Tiling);
            material.SetTextureOffset(p + "Albedo", surface.Offset);
            material.SetFloat(p + "NormalScale", Mathf.Clamp(surface.NormalScale, 0, 2));
            material.SetFloat(p + "Metallic", Mathf.Clamp01(surface.Metallic));
            material.SetFloat(p + "Smoothness", Mathf.Clamp01(surface.Smoothness));
            material.SetFloat(p + "OcclusionStrength", Mathf.Clamp01(surface.Occlusion));
        }

        private static void Changed(TexturePaintAsset paint)
        {
            EditorUtility.SetDirty(paint);
            EditorUtility.SetDirty(paint.Material);
            EditorUtility.SetDirty(paint.Mask);
            TexturePaintSaveQueue.Enqueue(paint);
            SceneView.RepaintAll();
        }

        private static void Record(TexturePaintAsset paint, bool includePixels, string label)
        {
            SimpleBlendTexturePaintTool.FinishCurrentStroke();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(includePixels ? new Object[] { paint, paint.Material } : new Object[] { paint.Material }, label);
        }
    }
}
