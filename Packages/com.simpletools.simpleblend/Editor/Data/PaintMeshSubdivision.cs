using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal static class PaintMeshSubdivision
    {
        internal const int MaximumTriangles = 200000;

        internal static string UnsupportedReason(Mesh mesh)
        {
            if (mesh == null) return "Select a mesh first.";
            if (!mesh.isReadable) return "Subdivision requires a readable mesh. Enable Read/Write on a separate model copy if needed.";
            if (mesh.blendShapeCount != 0 || mesh.HasVertexAttribute(VertexAttribute.BlendWeight)
                || mesh.HasVertexAttribute(VertexAttribute.BlendIndices))
                return "Subdivision currently supports static meshes without skinning or blend shapes.";
            if (mesh.subMeshCount == 0) return "The mesh has no triangles.";
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    return "Subdivision requires triangle topology in every submesh.";
            for (var channel = 0; channel < 8; channel++)
                if (mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel)) == 1)
                    return "One-component UV channels are not supported by subdivision.";
            return string.Empty;
        }

        internal static Mesh Create(Mesh source, Color32[] paint, int levels)
        {
            var reason = UnsupportedReason(source);
            if (!string.IsNullOrEmpty(reason)) throw new ArgumentException(reason);
            if (levels < 1 || levels > 4) throw new ArgumentOutOfRangeException(nameof(levels));
            if (paint != null && paint.Length != source.vertexCount)
                throw new ArgumentException("Paint must match the source vertex count.", nameof(paint));

            long triangles = 0;
            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
                triangles += source.GetIndexCount(submesh) / 3;
            if (triangles == 0) throw new ArgumentException("The mesh has no triangles.");
            for (var level = 0; level < levels; level++) triangles *= 4;
            if (triangles > MaximumTriangles)
                throw new ArgumentException($"The result exceeds the {MaximumTriangles:N0}-triangle safety limit. Use fewer subdivision levels.");

            var positions = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            var tangents = new List<Vector4>(source.tangents);
            var colors = new List<Color32>(paint ?? source.colors32);
            var uvs = new List<Vector4>[8];
            var dimensions = new int[8];
            for (var channel = 0; channel < 8; channel++)
            {
                uvs[channel] = new List<Vector4>();
                source.GetUVs(channel, uvs[channel]);
                dimensions[channel] = source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel));
            }

            var indices = new List<int>[source.subMeshCount];
            for (var submesh = 0; submesh < indices.Length; submesh++)
                indices[submesh] = new List<int>(source.GetTriangles(submesh));

            for (var level = 0; level < levels; level++)
            {
                // Index-based sharing keeps UV seams and hard-normal splits separate.
                var midpoints = new Dictionary<ulong, int>();
                int Midpoint(int a, int b)
                {
                    var key = ((ulong)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                    if (midpoints.TryGetValue(key, out var existing)) return existing;
                    var index = positions.Count;
                    positions.Add((positions[a] + positions[b]) * 0.5f);
                    if (normals.Count != 0) normals.Add((normals[a] + normals[b]).normalized);
                    if (tangents.Count != 0)
                    {
                        var direction = ((Vector3)tangents[a] + (Vector3)tangents[b]).normalized;
                        tangents.Add(new Vector4(direction.x, direction.y, direction.z, tangents[a].w));
                    }
                    if (colors.Count != 0) colors.Add(Color32.Lerp(colors[a], colors[b], 0.5f));
                    for (var channel = 0; channel < 8; channel++)
                        if (uvs[channel].Count != 0) uvs[channel].Add((uvs[channel][a] + uvs[channel][b]) * 0.5f);
                    midpoints.Add(key, index);
                    return index;
                }

                for (var submesh = 0; submesh < indices.Length; submesh++)
                {
                    var previous = indices[submesh];
                    var next = new List<int>(previous.Count * 4);
                    for (var triangle = 0; triangle < previous.Count; triangle += 3)
                    {
                        var a = previous[triangle];
                        var b = previous[triangle + 1];
                        var c = previous[triangle + 2];
                        var ab = Midpoint(a, b);
                        var bc = Midpoint(b, c);
                        var ca = Midpoint(c, a);
                        next.Add(a); next.Add(ab); next.Add(ca);
                        next.Add(ab); next.Add(b); next.Add(bc);
                        next.Add(ca); next.Add(bc); next.Add(c);
                        next.Add(ab); next.Add(bc); next.Add(ca);
                    }
                    indices[submesh] = next;
                }
            }

            var result = new Mesh
            {
                name = source.name + " Subdivided",
                indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            try
            {
                result.SetVertices(positions);
                if (normals.Count != 0) result.SetNormals(normals);
                if (tangents.Count != 0) result.SetTangents(tangents);
                if (colors.Count != 0) result.SetColors(colors);
                for (var channel = 0; channel < 8; channel++)
                {
                    if (dimensions[channel] == 2) result.SetUVs(channel, uvs[channel].ConvertAll(uv => (Vector2)uv));
                    else if (dimensions[channel] == 3) result.SetUVs(channel, uvs[channel].ConvertAll(uv => (Vector3)uv));
                    else if (dimensions[channel] == 4) result.SetUVs(channel, uvs[channel]);
                }
                result.subMeshCount = indices.Length;
                for (var submesh = 0; submesh < indices.Length; submesh++) result.SetTriangles(indices[submesh], submesh);
                if (normals.Count == 0) result.RecalculateNormals();
                result.RecalculateBounds();
                return result;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(result);
                throw;
            }
        }

        internal static GameObject CreatePaintableCopy(VertexPaintData original, int levels, string assetPath)
        {
            if (original == null || original.GetComponent<MeshRenderer>() == null
                || original.GetComponent<SkinnedMeshRenderer>() != null)
                throw new ArgumentException("Select a static MeshRenderer with initialized paint data.");
            if (!original.gameObject.scene.IsValid() || EditorUtility.IsPersistent(original))
                throw new ArgumentException("Open the prefab in Prefab Mode or select a scene instance before creating a copy.");
            if (!VertexPaintValidator.Validate(original, MeshAssetIdentity.GetStableIdentity(original.SourceMesh)).IsValid)
                throw new ArgumentException("Repair the source paint data before creating a copy.");
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || assetPath.Contains("..") || AssetDatabase.LoadMainAssetAtPath(assetPath) != null
                || System.IO.File.Exists(assetPath))
                throw new ArgumentException("Choose a new .asset path inside Assets. Existing assets are never overwritten.");

            var mesh = Create(original.SourceMesh, original.SerializedColors, levels);
            GameObject copy = null;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            const string undoName = "Create SimpleBlend Subdivided Copy";
            Undo.SetCurrentGroupName(undoName);
            try
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                copy = new GameObject(original.name + " Subdivided");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, original.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(copy, undoName);
                copy.transform.SetParent(original.transform.parent, false);
                copy.transform.localPosition = original.transform.localPosition;
                copy.transform.localRotation = original.transform.localRotation;
                copy.transform.localScale = original.transform.localScale;
                copy.layer = original.gameObject.layer;
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                EditorUtility.CopySerialized(original.GetComponent<MeshRenderer>(), renderer);
                renderer.additionalVertexStreams = null;
                var result = VertexPaintSetupUtility.Enable(renderer, null, out _);
                if (!result.IsValid) throw new InvalidOperationException(result.Message);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                return copy;
            }
            catch
            {
                if (copy != null) Undo.RevertAllDownToGroup(group);
                if (!AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                // Saved assets are retained for recovery, including after scene Undo.
                throw;
            }
        }
    }
}
