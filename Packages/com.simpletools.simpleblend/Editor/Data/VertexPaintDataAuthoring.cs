using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class VertexPaintDataAuthoring
    {
        internal static VertexPaintValidationResult Initialize(
            VertexPaintData data,
            Renderer renderer,
            bool? gpuResidentDrawerEnabled = null)
        {
            if (data == null || renderer == null || data.gameObject != renderer.gameObject)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingRenderer);
            }

            var source = GetDisplayedSourceMesh(renderer);
            if (data.FallbackMesh != null && source == data.FallbackMesh && data.SourceMesh != null)
            {
                source = data.SourceMesh;
            }
            if (source == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh);
            }

            var identity = MeshAssetIdentity.GetStableIdentity(source);
            var signatureResult = MeshSignatureFactory.TryCreate(source, identity, out var signature);
            if (!signatureResult.IsValid)
            {
                return signatureResult;
            }

            var colorResult = VertexColorInitializer.TryCreate(source, out var colors);
            if (!colorResult.IsValid)
            {
                return colorResult;
            }

            var decision = VertexPaintStoragePolicy.Resolve(
                renderer,
                gpuResidentDrawerEnabled ?? VertexPaintStoragePolicy.IsGpuResidentDrawerEnabled());
            if (decision.Mode == VertexPaintStorageMode.Unresolved)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.StorageModeUnresolved);
            }

            data.Configure(source, signature, colors, decision.Mode, decision.Reason);

            if (decision.Mode == VertexPaintStorageMode.ClonedMesh)
            {
                var cloneResult = GeneratedMeshWriter.TryCreate(
                    source,
                    colors,
                    data.gameObject.name,
                    out var clone,
                    out var assetPath);
                if (!cloneResult.IsValid)
                {
                    data.SetValidation(cloneResult);
                    EditorUtility.SetDirty(data);
                    return cloneResult;
                }

                data.SetFallbackMesh(clone, assetPath);
            }

            var applyResult = Apply(data);
            EditorUtility.SetDirty(data);
            if (data.FallbackMesh != null)
            {
                EditorUtility.SetDirty(data.FallbackMesh);
            }

            return applyResult;
        }

        internal static VertexPaintValidationResult Apply(VertexPaintData data)
        {
            var identity = data == null ? string.Empty : MeshAssetIdentity.GetStableIdentity(data.SourceMesh);
            var validation = VertexPaintValidator.Validate(data, identity);
            if (!validation.IsValid)
            {
                VertexPaintRuntimeApplicator.Reject(data, validation);
                return validation;
            }

            return VertexPaintRuntimeApplicator.Apply(data);
        }

        internal static void Clear(VertexPaintData data)
        {
            VertexPaintRuntimeApplicator.Clear(data);
            if (data != null)
            {
                EditorUtility.SetDirty(data);
            }
        }

        private static Mesh GetDisplayedSourceMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                return skinnedRenderer.sharedMesh;
            }

            if (renderer is MeshRenderer meshRenderer)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                return meshFilter == null ? null : meshFilter.sharedMesh;
            }

            return null;
        }
    }
}
