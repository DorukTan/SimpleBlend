using UnityEngine;

namespace SimpleBlend
{
    internal static class VertexPaintValidator
    {
        internal static VertexPaintValidationResult Validate(VertexPaintData data, string currentSourceIdentity = null)
        {
            if (data == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            if (data.SourceMesh == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            if (!data.SourceSignature.IsCreated)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSignature);
            }

            var identity = currentSourceIdentity ?? data.SourceSignature.SourceIdentity;
            var signatureResult = MeshSignatureFactory.TryCreate(data.SourceMesh, identity, out var currentSignature);
            if (!signatureResult.IsValid)
            {
                return signatureResult;
            }

            var signatureFailure = data.SourceSignature.Compare(currentSignature);
            if (signatureFailure != VertexPaintValidationFailure.None)
            {
                return VertexPaintValidationResult.Invalid(signatureFailure);
            }

            if (data.ColorCount != data.SourceSignature.VertexCount)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.ColorCountMismatch);
            }

            if (data.StorageMode == VertexPaintStorageMode.Unresolved)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.StorageModeUnresolved);
            }

            var meshRenderer = data.GetComponent<MeshRenderer>();
            var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();

            if (meshRenderer == null && skinnedRenderer == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            if (meshRenderer != null)
            {
                var meshFilter = data.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingMeshFilter);
                }

                if (data.StorageMode == VertexPaintStorageMode.AdditionalVertexStream
                    && meshFilter.sharedMesh != data.SourceMesh)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.RendererMeshMismatch);
                }

                if (data.StorageMode == VertexPaintStorageMode.ClonedMesh
                    && meshFilter.sharedMesh != data.SourceMesh
                    && meshFilter.sharedMesh != data.FallbackMesh)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.RendererMeshMismatch);
                }
            }

            if (skinnedRenderer != null)
            {
                if (data.StorageMode != VertexPaintStorageMode.ClonedMesh)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.UnsupportedStorageMode);
                }

                if (skinnedRenderer.sharedMesh != data.SourceMesh
                    && skinnedRenderer.sharedMesh != data.FallbackMesh)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.RendererMeshMismatch);
                }
            }

            if (data.StorageMode == VertexPaintStorageMode.ClonedMesh)
            {
                if (data.FallbackMesh == null)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingGeneratedMesh);
                }

                if (data.FallbackMesh.vertexCount != data.SourceSignature.VertexCount
                    || data.FallbackMesh.subMeshCount != data.SourceSignature.SubMeshCount)
                {
                    return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.GeneratedMeshMismatch);
                }
            }

            return VertexPaintValidationResult.Valid();
        }
    }
}
