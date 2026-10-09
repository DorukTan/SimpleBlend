using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal static class TerrainBindingProperties
    {
        internal const string Version = "_SimpleBlendTerrainBindingVersion";
        internal const string Control = "_TerrainControl";
        internal const string Origin = "_TerrainOrigin";
        internal const string Size = "_TerrainSize";
        internal const string LayerCount = "_TerrainLayerCount";
        internal static readonly string[] TextureSuffixes = { "Diffuse", "Normal", "Mask" };
        internal static readonly string[] VectorSuffixes =
        {
            "TileSizeOffset", "Surface", "Flags",
            "DiffuseRemapMin", "DiffuseRemapMax", "MaskRemapMin", "MaskRemapMax"
        };

        internal static string Layer(int index, string suffix) => "_TerrainLayer" + index + suffix;
    }

    internal static class TerrainMaterialBinder
    {
        internal static bool Validate(Terrain terrain, Material material, out string message)
        {
            return TryRead(terrain, material, out _, out message);
        }

        internal static bool TryBind(Terrain terrain, Material material, out string message)
        {
            if (!TryRead(terrain, material, out var input, out message))
                return false;

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            const string undoName = "Bind SimpleBlend Terrain";
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(material, undoName);
            material.SetTexture(TerrainBindingProperties.Control, input.Control);
            material.SetVector(TerrainBindingProperties.Origin, input.Origin);
            material.SetVector(TerrainBindingProperties.Size, input.Size);
            material.SetFloat(TerrainBindingProperties.LayerCount, input.Layers.Length);
            for (var index = 0; index < 4; index++)
            {
                var layer = index < input.Layers.Length ? input.Layers[index] : LayerInput.Empty;
                material.SetTexture(TerrainBindingProperties.Layer(index, "Diffuse"), layer.Diffuse);
                material.SetTexture(TerrainBindingProperties.Layer(index, "Normal"), layer.Normal);
                material.SetTexture(TerrainBindingProperties.Layer(index, "Mask"), layer.Mask);
                material.SetVector(TerrainBindingProperties.Layer(index, "TileSizeOffset"), layer.TileSizeOffset);
                material.SetVector(TerrainBindingProperties.Layer(index, "Surface"), layer.Surface);
                material.SetVector(TerrainBindingProperties.Layer(index, "Flags"), layer.Flags);
                material.SetVector(TerrainBindingProperties.Layer(index, "DiffuseRemapMin"), layer.DiffuseRemapMin);
                material.SetVector(TerrainBindingProperties.Layer(index, "DiffuseRemapMax"), layer.DiffuseRemapMax);
                material.SetVector(TerrainBindingProperties.Layer(index, "MaskRemapMin"), layer.MaskRemapMin);
                material.SetVector(TerrainBindingProperties.Layer(index, "MaskRemapMax"), layer.MaskRemapMax);
            }
            EditorUtility.SetDirty(material);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            message = "Bound " + input.Layers.Length + " terrain layers to " + material.name
                + ". Save the material to persist the binding.";
            return true;
        }

        private static bool TryRead(Terrain terrain, Material material, out BindingInput input, out string message)
        {
            input = null;
            if (terrain == null)
                return Fail("Select a Terrain.", out message);
            var data = terrain.terrainData;
            if (data == null)
                return Fail("The selected Terrain has no TerrainData.", out message);
            if (material == null)
                return Fail("Select a destination material.", out message);
            var path = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.IsSubAsset(material)
                    || !AssetDatabase.IsOpenForEdit(material)
                    || !WritableFile(path))
                    return Fail("Use an editable standalone material in Assets. Duplicate or extract the material first.",
                        out message);
            }
            if (!ValidateShader(material.shader, out message))
                return false;
            if (Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.001f
                || (terrain.transform.lossyScale - Vector3.one).sqrMagnitude > 0.00000001f)
                return Fail("Terrain matching requires zero world rotation and unit world scale.", out message);
            var origin = terrain.transform.position;
            var size = data.size;
            if (!Finite(origin) || !Finite(size) || size.x <= 0f || size.z <= 0f)
                return Fail("Terrain origin and size must be finite, with positive X and Z size.", out message);

            var layers = data.terrainLayers;
            if (layers == null || layers.Length == 0)
                return Fail("Assign at least one Terrain Layer.", out message);
            if (layers.Length > 4)
                return Fail("Terrain matching supports at most four assigned Terrain Layers. No partial binding was applied.",
                    out message);
            if (data.alphamapTextureCount != 1)
                return Fail("The TerrainData must expose one control texture for its supported layers.", out message);
            var control = data.GetAlphamapTexture(0);
            if (control == null)
                return Fail("The TerrainData has no control texture 0.", out message);
            if (!string.IsNullOrEmpty(path)
                && (!EditorUtility.IsPersistent(data) || !EditorUtility.IsPersistent(control)))
                return Fail("Save the TerrainData as an asset before binding a material asset.", out message);

            var inputs = new LayerInput[layers.Length];
            for (var index = 0; index < layers.Length; index++)
            {
                var layer = layers[index];
                if (layer == null)
                    return Fail("Terrain Layer " + index + " is missing. Assign it before binding.", out message);
                var tileSize = layer.tileSize;
                var tileOffset = layer.tileOffset;
                var surface = new Vector4(layer.normalScale, layer.metallic, layer.smoothness, 0f);
                var tiling = new Vector4(tileSize.x, tileSize.y, tileOffset.x, tileOffset.y);
                if (!string.IsNullOrEmpty(path)
                    && (!SerializableTexture(layer.diffuseTexture)
                        || !SerializableTexture(layer.normalMapTexture)
                        || !SerializableTexture(layer.maskMapTexture)))
                    return Fail("Terrain Layer " + index + " has an unsaved texture. Save its textures as assets first.",
                        out message);
                if (!Finite(tiling) || tileSize.x == 0f || tileSize.y == 0f)
                    return Fail("Terrain Layer " + index + " needs finite tiling/offset values and nonzero tile size.",
                        out message);
                if (!Finite(surface) || !Finite(layer.diffuseRemapMin) || !Finite(layer.diffuseRemapMax)
                    || !Finite(layer.maskMapRemapMin) || !Finite(layer.maskMapRemapMax))
                    return Fail("Terrain Layer " + index + " has non-finite surface or remap values.", out message);
                inputs[index] = new LayerInput
                {
                    Diffuse = layer.diffuseTexture != null ? layer.diffuseTexture : Texture2D.whiteTexture,
                    Normal = layer.normalMapTexture != null ? layer.normalMapTexture : Texture2D.normalTexture,
                    Mask = layer.maskMapTexture != null ? layer.maskMapTexture : Texture2D.whiteTexture,
                    TileSizeOffset = tiling,
                    Surface = surface,
                    Flags = new Vector4(layer.diffuseTexture != null ? 1f : 0f,
                        layer.normalMapTexture != null ? 1f : 0f, layer.maskMapTexture != null ? 1f : 0f, 0f),
                    DiffuseRemapMin = layer.diffuseRemapMin,
                    DiffuseRemapMax = layer.diffuseRemapMax,
                    MaskRemapMin = layer.maskMapRemapMin,
                    MaskRemapMax = layer.maskMapRemapMax
                };
            }
            input = new BindingInput
            {
                Control = control,
                Origin = new Vector4(origin.x, origin.y, origin.z, 0f),
                Size = new Vector4(size.x, size.y, size.z, 0f),
                Layers = inputs
            };
            message = "Ready to bind " + layers.Length + " terrain layers.";
            return true;
        }

        private static bool ValidateShader(Shader shader, out string message)
        {
            if (shader == null)
                return Fail("The destination material has no shader.", out message);
            var versionIndex = shader.FindPropertyIndex(TerrainBindingProperties.Version);
            if (versionIndex < 0 || !IsFloat(shader.GetPropertyType(versionIndex))
                || shader.GetPropertyDefaultFloatValue(versionIndex) != 1f)
                return Fail("Choose a material whose shader implements SimpleBlend terrain binding contract v1.",
                    out message);
            if (!Property(shader, TerrainBindingProperties.Control, ShaderPropertyType.Texture, out message)
                || !Property(shader, TerrainBindingProperties.Origin, ShaderPropertyType.Vector, out message)
                || !Property(shader, TerrainBindingProperties.Size, ShaderPropertyType.Vector, out message)
                || !Property(shader, TerrainBindingProperties.LayerCount, ShaderPropertyType.Float, out message))
                return false;
            for (var index = 0; index < 4; index++)
            {
                foreach (var suffix in TerrainBindingProperties.TextureSuffixes)
                    if (!Property(shader, TerrainBindingProperties.Layer(index, suffix),
                        ShaderPropertyType.Texture, out message))
                        return false;
                foreach (var suffix in TerrainBindingProperties.VectorSuffixes)
                    if (!Property(shader, TerrainBindingProperties.Layer(index, suffix),
                        ShaderPropertyType.Vector, out message))
                        return false;
            }
            message = string.Empty;
            return true;
        }

        private static bool Property(Shader shader, string name, ShaderPropertyType expected, out string message)
        {
            var index = shader.FindPropertyIndex(name);
            if (index < 0)
                return Fail("The shader is missing required terrain property " + name + ".", out message);
            var actual = shader.GetPropertyType(index);
            if (actual != expected && !(expected == ShaderPropertyType.Float && IsFloat(actual)))
                return Fail("Terrain property " + name + " must be " + expected + ".", out message);
            if (expected == ShaderPropertyType.Texture && shader.GetPropertyTextureDimension(index) != TextureDimension.Tex2D)
                return Fail("Terrain property " + name + " must be a 2D texture.", out message);
            message = string.Empty;
            return true;
        }

        private static bool IsFloat(ShaderPropertyType type) =>
            type == ShaderPropertyType.Float || type == ShaderPropertyType.Range;

        private static bool WritableFile(string assetPath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            try
            {
                return File.Exists(fullPath)
                    && (File.GetAttributes(fullPath) & FileAttributes.ReadOnly) == 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool SerializableTexture(Texture2D texture) => texture == null
            || EditorUtility.IsPersistent(texture) || texture == Texture2D.whiteTexture
            || texture == Texture2D.blackTexture || texture == Texture2D.grayTexture
            || texture == Texture2D.normalTexture;

        private static bool Fail(string reason, out string message)
        {
            message = reason;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

        private sealed class BindingInput
        {
            internal Texture2D Control;
            internal Vector4 Origin;
            internal Vector4 Size;
            internal LayerInput[] Layers;
        }

        private sealed class LayerInput
        {
            internal Texture2D Diffuse;
            internal Texture2D Normal;
            internal Texture2D Mask;
            internal Vector4 TileSizeOffset;
            internal Vector4 Surface;
            internal Vector4 Flags;
            internal Vector4 DiffuseRemapMin;
            internal Vector4 DiffuseRemapMax;
            internal Vector4 MaskRemapMin;
            internal Vector4 MaskRemapMax;

            internal static LayerInput Empty => new LayerInput
            {
                Diffuse = Texture2D.whiteTexture,
                Normal = Texture2D.normalTexture,
                Mask = Texture2D.whiteTexture,
                TileSizeOffset = new Vector4(1f, 1f, 0f, 0f),
                Surface = new Vector4(1f, 0f, 0f, 0f),
                DiffuseRemapMax = Vector4.one,
                MaskRemapMax = Vector4.one
            };
        }
    }
}
