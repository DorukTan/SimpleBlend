using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal static class GeneratedMeshWriter
    {
        private const string FallbackFolder = "Assets/SimpleBlendGenerated";

        internal static VertexPaintValidationResult TryCreate(
            Mesh source,
            Color32[] colors,
            string ownerName,
            out Mesh generatedMesh,
            out string assetPath)
        {
            generatedMesh = null;
            assetPath = string.Empty;

            if (source == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            if (colors == null || colors.Length != source.vertexCount)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.ColorCountMismatch);
            }

            var cloneResult = TryCreateEditableClone(source, colors, out generatedMesh);
            if (!cloneResult.IsValid)
            {
                return cloneResult;
            }

            try
            {
                var folder = ResolveOutputFolder(source);
                EnsureAssetFolder(folder);
                var owner = SanitizeFileName(string.IsNullOrWhiteSpace(ownerName) ? "Renderer" : ownerName);
                var meshName = SanitizeFileName(string.IsNullOrWhiteSpace(source.name) ? "Mesh" : source.name);
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{folder}/{owner}_{meshName}_SimpleBlend.asset");

                generatedMesh.name = $"{ownerName} {source.name} SimpleBlend".Trim();
                AssetDatabase.CreateAsset(generatedMesh, assetPath);
                AssetDatabase.SaveAssets();
                return VertexPaintValidationResult.Valid();
            }
            catch (Exception exception)
            {
                if (generatedMesh != null && !AssetDatabase.Contains(generatedMesh))
                {
                    UnityEngine.Object.DestroyImmediate(generatedMesh);
                }

                generatedMesh = null;
                assetPath = string.Empty;
                return VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.GeneratedAssetWriteFailed,
                    $"Unity could not write a generated SimpleBlend mesh: {exception.Message}");
            }
        }

        private static VertexPaintValidationResult TryCreateEditableClone(
            Mesh source,
            Color32[] colors,
            out Mesh clone)
        {
            clone = null;
            Mesh.MeshDataArray writableData = default;
            var writableAllocated = false;

            try
            {
                using (var sourceDataArray = Mesh.AcquireReadOnlyMeshData(source))
                {
                    var sourceData = sourceDataArray[0];
                    writableData = Mesh.AllocateWritableMeshData(1);
                    writableAllocated = true;
                    var destinationData = writableData[0];
                    var attributes = source.GetVertexAttributes();

                    destinationData.SetVertexBufferParams(sourceData.vertexCount, attributes);
                    for (var stream = 0; stream < sourceData.vertexBufferCount; stream++)
                    {
                        var sourceBytes = sourceData.GetVertexData<byte>(stream);
                        var destinationBytes = destinationData.GetVertexData<byte>(stream);
                        destinationBytes.CopyFrom(sourceBytes);
                    }

                    if (sourceData.indexFormat == IndexFormat.UInt16)
                    {
                        var sourceIndices = sourceData.GetIndexData<ushort>();
                        destinationData.SetIndexBufferParams(sourceIndices.Length, IndexFormat.UInt16);
                        destinationData.GetIndexData<ushort>().CopyFrom(sourceIndices);
                    }
                    else
                    {
                        var sourceIndices = sourceData.GetIndexData<uint>();
                        destinationData.SetIndexBufferParams(sourceIndices.Length, IndexFormat.UInt32);
                        destinationData.GetIndexData<uint>().CopyFrom(sourceIndices);
                    }

                    destinationData.subMeshCount = sourceData.subMeshCount;
                    for (var subMeshIndex = 0; subMeshIndex < sourceData.subMeshCount; subMeshIndex++)
                    {
                        destinationData.SetSubMesh(
                            subMeshIndex,
                            sourceData.GetSubMesh(subMeshIndex),
                            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    }
                }

                clone = new Mesh
                {
                    name = source.name,
                    indexBufferTarget = source.indexBufferTarget,
                    vertexBufferTarget = source.vertexBufferTarget
                };
                Mesh.ApplyAndDisposeWritableMeshData(
                    writableData,
                    clone,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                writableAllocated = false;
                clone.bounds = source.bounds;

                CopySkinning(source, clone);
                CopyBlendShapes(source, clone);
                clone.SetColors(colors);
                return VertexPaintValidationResult.Valid();
            }
            catch (Exception exception)
            {
                if (writableAllocated)
                {
                    writableData.Dispose();
                }

                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                    clone = null;
                }

                return VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.SourceDataUnavailable,
                    $"Unity could not clone all mesh data from '{source.name}': {exception.Message}");
            }
        }

        private static void CopySkinning(Mesh source, Mesh destination)
        {
            var bindposes = source.GetBindposes();
            if (bindposes.IsCreated && bindposes.Length > 0)
            {
                destination.SetBindposes(bindposes);
            }

            var bonesPerVertex = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            if (bonesPerVertex.IsCreated && bonesPerVertex.Length > 0 && weights.IsCreated && weights.Length > 0)
            {
                destination.SetBoneWeights(bonesPerVertex, weights);
            }
        }

        private static void CopyBlendShapes(Mesh source, Mesh destination)
        {
            if (source.blendShapeCount == 0)
            {
                return;
            }

            var deltaVertices = new Vector3[source.vertexCount];
            var deltaNormals = new Vector3[source.vertexCount];
            var deltaTangents = new Vector3[source.vertexCount];

            for (var shapeIndex = 0; shapeIndex < source.blendShapeCount; shapeIndex++)
            {
                var shapeName = source.GetBlendShapeName(shapeIndex);
                var frameCount = source.GetBlendShapeFrameCount(shapeIndex);
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    Array.Clear(deltaVertices, 0, deltaVertices.Length);
                    Array.Clear(deltaNormals, 0, deltaNormals.Length);
                    Array.Clear(deltaTangents, 0, deltaTangents.Length);
                    source.GetBlendShapeFrameVertices(
                        shapeIndex,
                        frameIndex,
                        deltaVertices,
                        deltaNormals,
                        deltaTangents);
                    destination.AddBlendShapeFrame(
                        shapeName,
                        source.GetBlendShapeFrameWeight(shapeIndex, frameIndex),
                        deltaVertices,
                        deltaNormals,
                        deltaTangents);
                }
            }
        }

        private static string ResolveOutputFolder(Mesh source)
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(sourcePath)
                && sourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(directory))
                {
                    return directory;
                }
            }

            return FallbackFolder;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var segments = folder.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (var character in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(character, '_');
            }

            return value.Replace('/', '_').Replace('\\', '_');
        }
    }
}
