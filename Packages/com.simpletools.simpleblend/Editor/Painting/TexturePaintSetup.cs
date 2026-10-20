using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    internal static class TexturePaintSetup
    {
        internal static string Problem(GameObject source, SurfacePalette palette, int section, bool requirePalette = true)
        {
            if (source == null || EditorUtility.IsPersistent(source) || !source.scene.IsValid())
                return "Select one scene mesh object.";
            if (!source.activeInHierarchy) return "Enable the selected scene object before setting up painting.";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Leave Play mode before setting up painting.";
            if (source.GetComponent<TexturePaintTarget>() != null) return "This object already has texture paint.";
            if (source.GetComponent<SkinnedMeshRenderer>() != null) return "Texture painting currently supports static meshes only.";
            var renderer = source.GetComponent<MeshRenderer>();
            var mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
            if (renderer == null || mesh == null || !mesh.isReadable) return "Choose a readable MeshFilter mesh with a MeshRenderer.";
            if (mesh.vertexCount == 0 || mesh.vertexCount > 100000 || mesh.subMeshCount == 0 || mesh.GetIndexCount(0) == 0)
                return "Choose a non-empty mesh with at most 100,000 vertices for texture setup.";
            for (var i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) != MeshTopology.Triangles) return "Texture setup requires triangle mesh sections.";
            if (section < 0 || section >= mesh.subMeshCount || section >= renderer.sharedMaterials.Length)
                return "Choose a section with an existing material slot.";
            if (palette == null) return requirePalette ? "Create or choose a surface palette before setting up texture painting." : string.Empty;
            return palette.Problem;
        }

        internal static TexturePaintTarget CreateAutomatic(GameObject source, int section = 0, int resolution = 512,
            SurfacePalette palette = null, string directory = "Assets/SimpleBlendPaint")
        {
            var problem = Problem(source, palette, section, false);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            if (resolution != 256 && resolution != 512 && resolution != 1024) throw new ArgumentOutOfRangeException(nameof(resolution));
            if (string.IsNullOrEmpty(directory) || !directory.StartsWith("Assets/", StringComparison.Ordinal)
                || directory.Contains("..") || directory.Contains("\\")) throw new ArgumentException("Choose a folder inside Assets.");
            SurfacePalette temporaryPalette = null;
            Material temporaryMaterial = null;
            var minimalDefaults = false;
            try
            {
                if (palette == null)
                {
                    var sourceMaterial = source.GetComponent<MeshRenderer>().sharedMaterials[section];
                    var supported = sourceMaterial != null && sourceMaterial.shader != null
                        && (sourceMaterial.shader.name == SimpleBlendLayerBlendContract.LayerBlend3ShaderName
                            || sourceMaterial.shader.name == SimpleBlendLayerBlendContract.LayerBlend4ShaderName);
                    var shader = supported ? sourceMaterial.shader : Shader.Find(SimpleBlendLayerBlendContract.LayerBlend3ShaderName);
                    if (shader == null) throw new InvalidOperationException("The SimpleBlend layer shader is unavailable.");
                    temporaryMaterial = supported ? new Material(sourceMaterial) : new Material(shader);
                    var count = shader.name == SimpleBlendLayerBlendContract.LayerBlend4ShaderName ? 4 : 3;
                    temporaryMaterial.SetFloat("_LayerCount", count);
                    if (!supported)
                    {
                        minimalDefaults = true;
                        CopyBase(sourceMaterial, temporaryMaterial);
                        temporaryMaterial.SetColor("_Layer1Color", new Color(0.4f, 0.65f, 0.35f));
                        temporaryMaterial.SetColor("_Layer2Color", new Color(0.65f, 0.45f, 0.3f));
                    }
                    temporaryPalette = ScriptableObject.CreateInstance<SurfacePalette>();
                    temporaryPalette.Configure(temporaryMaterial, count == 4
                        ? new[] { "Base", "Surface 1", "Surface 2", "Surface 3" } : new[] { "Base", "Surface 1", "Surface 2" });
                    palette = temporaryPalette;
                }
                var parts = directory.Split('/');
                var parent = "Assets";
                for (var i = 1; i < parts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(parts[i])) throw new ArgumentException("Choose a valid Assets subfolder.");
                    var child = parent + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
                    parent = child;
                }
                var name = source.name;
                foreach (var invalid in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                if (name.Length > 64) name = name.Substring(0, 64);
                name = name.Trim().TrimEnd('.');
                if (string.IsNullOrEmpty(name)) name = "Object";
                var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + name + " Paint.asset");
                var target = Create(source, palette, section, resolution, path);
                if (minimalDefaults)
                {
                    // Start with Base and one overlay; unused shader channels remain zero until a surface is added.
                    target.Paint.SetSurfaceNames(new[] { "Base", "Surface 1" });
                    EditorUtility.SetDirty(target.Paint);
                    AssetDatabase.SaveAssetIfDirty(target.Paint);
                }
                return target;
            }
            finally
            {
                if (temporaryPalette != null) Object.DestroyImmediate(temporaryPalette);
                if (temporaryMaterial != null) Object.DestroyImmediate(temporaryMaterial);
            }
        }

        private static void CopyBase(Material source, Material destination)
        {
            if (source == null) return;
            var textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (textureProperty != null)
            {
                destination.SetTexture("_Layer0Albedo", source.GetTexture(textureProperty));
                destination.SetTextureScale("_Layer0Albedo", source.GetTextureScale(textureProperty));
                destination.SetTextureOffset("_Layer0Albedo", source.GetTextureOffset(textureProperty));
            }
            if (source.HasProperty("_BaseColor")) destination.SetColor("_Layer0Color", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color")) destination.SetColor("_Layer0Color", source.GetColor("_Color"));
            if (source.HasProperty("_BumpMap")) destination.SetTexture("_Layer0Normal", source.GetTexture("_BumpMap"));
            if (source.HasProperty("_BumpScale")) destination.SetFloat("_Layer0NormalScale", source.GetFloat("_BumpScale"));
            if (source.HasProperty("_Metallic")) destination.SetFloat("_Layer0Metallic", source.GetFloat("_Metallic"));
            if (source.HasProperty("_Smoothness")) destination.SetFloat("_Layer0Smoothness", source.GetFloat("_Smoothness"));
        }

        internal static TexturePaintTarget Create(GameObject source, SurfacePalette palette, int section, int resolution, string path)
        {
            var problem = Problem(source, palette, section);
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            if (resolution != 256 && resolution != 512 && resolution != 1024) throw new ArgumentOutOfRangeException(nameof(resolution));
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || !AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'))
                || System.IO.File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("Choose a new .asset path inside Assets. Existing assets are never overwritten.", nameof(path));

            var mesh = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
            TexturePaintAsset paint = null;
            Material material = null;
            Texture2D mask = null;
            GameObject copy = null;
            var assetCreated = false;
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var wasEnabled = sourceRenderer.enabled;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up texture painting");
            try
            {
                mesh.name = source.name + " Paint Mesh";
                if (mesh.normals.Length != mesh.vertexCount) mesh.RecalculateNormals();
                // Preserve static lightmap UVs through unwrap's vertex splits. UV4 is owned by this paint copy.
                mesh.uv4 = mesh.uv2;
                UnwrapParam.SetDefaults(out var unwrap);
                unwrap.packMargin = 8f / resolution;
                if (!Unwrapping.GenerateSecondaryUVSet(mesh, unwrap)) throw new InvalidOperationException("Could not generate a unique paint atlas.");
                var paintUv = mesh.uv2;
                mesh.uv2 = mesh.uv4;
                mesh.uv4 = paintUv;
                if (paintUv.Length != mesh.vertexCount) throw new InvalidOperationException("The generated paint atlas is incomplete.");

                mask = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
                {
                    name = source.name + " Blend Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                material = new Material(palette.Material) { name = source.name + " Painted Surfaces" };
                material.SetFloat("_UseTextureWeights", 1);
                material.SetFloat("_HeightBlendStrength", 0);
                material.SetTexture("_BlendMask", mask);
                paint = ScriptableObject.CreateInstance<TexturePaintAsset>();
                paint.name = source.name + " Texture Paint";
                var names = new string[palette.SurfaceCount];
                for (var i = 0; i < names.Length; i++) names[i] = palette.SurfaceName(i);
                paint.Configure(mesh, material, mask, names);
                AssetDatabase.CreateAsset(paint, path);
                if (AssetDatabase.LoadMainAssetAtPath(path) != paint) throw new InvalidOperationException("The texture paint asset could not be saved.");
                assetCreated = true;
                AssetDatabase.AddObjectToAsset(mesh, paint);
                AssetDatabase.AddObjectToAsset(material, paint);
                AssetDatabase.AddObjectToAsset(mask, paint);
                EditorUtility.SetDirty(paint);
                AssetDatabase.SaveAssetIfDirty(paint);

                copy = new GameObject(source.name + " Painted");
                SceneManager.MoveGameObjectToScene(copy, source.scene);
                copy.transform.SetParent(source.transform.parent, false);
                copy.transform.localPosition = source.transform.localPosition;
                copy.transform.localRotation = source.transform.localRotation;
                copy.transform.localScale = source.transform.localScale;
                copy.layer = source.layer;
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                EditorUtility.CopySerialized(sourceRenderer, renderer);
                renderer.additionalVertexStreams = null;
                var materials = sourceRenderer.sharedMaterials;
                materials[section] = material;
                renderer.sharedMaterials = materials;
                renderer.enabled = true;
                var target = copy.AddComponent<TexturePaintTarget>();
                target.Configure(paint, section);
                Undo.RegisterCreatedObjectUndo(copy, "Create texture paint object");
                Undo.RecordObject(sourceRenderer, "Hide source renderer");
                sourceRenderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sourceRenderer);
                Undo.CollapseUndoOperations(group);
                return target;
            }
            catch
            {
                if (copy != null) Object.DestroyImmediate(copy);
                sourceRenderer.enabled = wasEnabled;
                // Only remove the asset created by this call; pre-existing paths were rejected above.
                if (assetCreated) AssetDatabase.DeleteAsset(path);
                foreach (var item in new Object[] { paint, material, mask, mesh })
                    if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
                throw;
            }
        }
    }
}
