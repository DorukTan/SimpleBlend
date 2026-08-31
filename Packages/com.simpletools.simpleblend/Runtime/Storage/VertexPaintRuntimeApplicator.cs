using System;
using UnityEngine;

namespace SimpleBlend
{
    internal static class VertexPaintRuntimeApplicator
    {
        internal static VertexPaintValidationResult Apply(VertexPaintData data)
        {
            var validation = VertexPaintValidator.Validate(data);
            data?.SetValidation(validation);

            if (!validation.IsValid)
            {
                if (data != null)
                {
                    DetachTransient(data);
                    RestoreSourceIfOwnedFallback(data);
                }

                return validation;
            }

            if (data.StorageMode == VertexPaintStorageMode.AdditionalVertexStream)
            {
                var renderer = data.GetComponent<MeshRenderer>();
                renderer.additionalVertexStreams = AdditionalVertexStreamBuilder.BuildOrUpdate(data);
                return validation;
            }

            try
            {
                DetachTransient(data);
                data.FallbackMesh.SetColors(data.SerializedColors);

                var meshFilter = data.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = data.FallbackMesh;
                }

                var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();
                if (skinnedRenderer != null)
                {
                    skinnedRenderer.sharedMesh = data.FallbackMesh;
                }

                return validation;
            }
            catch (Exception exception)
            {
                validation = VertexPaintValidationResult.Invalid(
                    VertexPaintValidationFailure.SourceDataUnavailable,
                    $"SimpleBlend could not update generated mesh '{data.FallbackMesh.name}': {exception.Message}");
                data.SetValidation(validation);
                RestoreSourceIfOwnedFallback(data);
                return validation;
            }
        }

        internal static void Clear(VertexPaintData data)
        {
            if (data == null)
            {
                return;
            }

            DetachTransient(data);
            RestoreSourceIfOwnedFallback(data);
            data.ResetSerializedState();
        }

        internal static void Reject(VertexPaintData data, VertexPaintValidationResult validation)
        {
            if (data == null)
            {
                return;
            }

            data.SetValidation(validation);
            DetachTransient(data);
            RestoreSourceIfOwnedFallback(data);
        }

        internal static void PrepareForReconfigure(VertexPaintData data)
        {
            if (data == null)
            {
                return;
            }

            DetachTransient(data);
            RestoreSourceIfOwnedFallback(data);
        }

        internal static void DetachTransient(VertexPaintData data)
        {
            if (data == null || data.TransientStream == null)
            {
                return;
            }

            var renderer = data.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.additionalVertexStreams == data.TransientStream)
            {
                renderer.additionalVertexStreams = null;
            }

            var stream = data.TransientStream;
            data.TransientStream = null;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(stream);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(stream);
            }
        }

        private static void RestoreSourceIfOwnedFallback(VertexPaintData data)
        {
            if (data.FallbackMesh == null || data.SourceMesh == null)
            {
                return;
            }

            var meshFilter = data.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == data.FallbackMesh)
            {
                meshFilter.sharedMesh = data.SourceMesh;
            }

            var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.sharedMesh == data.FallbackMesh)
            {
                skinnedRenderer.sharedMesh = data.SourceMesh;
            }
        }
    }
}
