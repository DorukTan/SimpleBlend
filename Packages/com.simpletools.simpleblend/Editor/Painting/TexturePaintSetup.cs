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
        internal static string Problem(GameObject source, SurfacePalette palette, int section)
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
            if (palette == null) return "Create or choose a surface palette before setting up texture painting.";
            return palette.Problem;
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
