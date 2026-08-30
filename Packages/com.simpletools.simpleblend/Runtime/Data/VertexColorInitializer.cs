using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend
{
    internal static class VertexColorInitializer
    {
        internal static VertexPaintValidationResult TryCreate(Mesh source, out Color32[] colors)
        {
            colors = Array.Empty<Color32>();

            if (source == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            try
            {
                using (var meshDataArray = Mesh.AcquireReadOnlyMeshData(source))
                {
                    var meshData = meshDataArray[0];
                    colors = new Color32[meshData.vertexCount];

                    if (!meshData.HasVertexAttribute(VertexAttribute.Color))
                    {
                        return VertexPaintValidationResult.Valid();
                    }

                    using (var sourceColors = new NativeArray<Color32>(
                               meshData.vertexCount,
                               Allocator.Temp,
                               NativeArrayOptions.UninitializedMemory))
                    {
                        meshData.GetColors(sourceColors);
                        sourceColors.CopyTo(colors);
                    }
                }

                return VertexPaintValidationResult.Valid();
            }
            catch (Exception exception)
            {
                colors = Array.Empty<Color32>();
                return VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.SourceDataUnavailable,
                    $"Unity could not initialize colors from '{source.name}': {exception.Message}");
            }
        }
    }
}
