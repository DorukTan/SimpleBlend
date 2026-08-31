using System;
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

            if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() == null)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingMeshFilter);
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

                data.SetFallbackMesh(clone, assetPath, CreateOwnerId());
            }

            var applyResult = Apply(data);
            EditorUtility.SetDirty(data);
            if (data.FallbackMesh != null)
            {
                EditorUtility.SetDirty(data.FallbackMesh);
            }

            return applyResult;
        }

        internal static VertexPaintValidationResult EnsureOwnedFallbackForPaint(
            VertexPaintData data,
            out bool ownershipChanged)
        {
            ownershipChanged = false;
            if (data == null || data.StorageMode != VertexPaintStorageMode.ClonedMesh)
            {
                return VertexPaintValidationResult.Valid();
            }

            if (!GeneratedMeshOwnership.RequiresFork(data))
            {
                return VertexPaintValidationResult.Valid();
            }

            var cloneResult = GeneratedMeshWriter.TryCreate(
                data.SourceMesh,
                data.SerializedColors,
                data.gameObject.name,
                out var clone,
                out var assetPath);
            if (!cloneResult.IsValid)
            {
                data.SetValidation(cloneResult);
                EditorUtility.SetDirty(data);
                return cloneResult;
            }

            var previousFallback = data.FallbackMesh;
            data.SetFallbackMesh(clone, assetPath, CreateOwnerId());
            AssignForkToRenderer(data, previousFallback, clone);
            var applyResult = Apply(data);
            if (!applyResult.IsValid)
            {
                return applyResult;
            }

            ownershipChanged = true;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(clone);
            return applyResult;
        }

        internal static VertexPaintValidationResult EnsureCurrentStorageForPaint(
            VertexPaintData data,
            bool? gpuResidentDrawerEnabled,
            out bool storageChanged)
        {
            storageChanged = false;
            var currentValidation = Apply(data);
            if (!currentValidation.IsValid)
            {
                return currentValidation;
            }

            var renderer = data.GetComponent<Renderer>();
            var decision = VertexPaintStoragePolicy.Resolve(
                renderer,
                gpuResidentDrawerEnabled ?? VertexPaintStoragePolicy.IsGpuResidentDrawerEnabled());
            if (decision.Mode == VertexPaintStorageMode.Unresolved)
            {
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.StorageModeUnresolved);
            }

            if (decision.Mode == data.StorageMode && decision.Reason == data.StorageReason)
            {
                return currentValidation;
            }

            if (decision.Mode == data.StorageMode)
            {
                data.SetStorageDecision(decision.Mode, decision.Reason);
                storageChanged = true;
                EditorUtility.SetDirty(data);
                return Apply(data);
            }

            if (decision.Mode == VertexPaintStorageMode.ClonedMesh)
            {
                var cloneResult = GeneratedMeshWriter.TryCreate(
                    data.SourceMesh,
                    data.SerializedColors,
                    data.gameObject.name,
                    out var clone,
                    out var assetPath);
                if (!cloneResult.IsValid)
                {
                    data.SetValidation(cloneResult);
                    EditorUtility.SetDirty(data);
                    return cloneResult;
                }

                VertexPaintRuntimeApplicator.PrepareForReconfigure(data);
                data.SetStorageDecision(decision.Mode, decision.Reason);
                data.SetFallbackMesh(clone, assetPath, CreateOwnerId());
                EditorUtility.SetDirty(clone);
            }
            else
            {
                VertexPaintRuntimeApplicator.PrepareForReconfigure(data);
                data.SetStorageDecision(decision.Mode, decision.Reason);
                data.SetFallbackMesh(null, string.Empty, string.Empty);
            }

            storageChanged = true;
            EditorUtility.SetDirty(data);
            var meshOwner = GetRendererMeshOwner(data);
            if (meshOwner != null)
            {
                EditorUtility.SetDirty(meshOwner);
            }

            return Apply(data);
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

        internal static UnityEngine.Object GetRendererMeshOwner(VertexPaintData data)
        {
            if (data == null)
            {
                return null;
            }

            var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();
            return skinnedRenderer != null
                ? skinnedRenderer
                : data.GetComponent<MeshFilter>();
        }

        internal static void RecordPrefabModifications(VertexPaintData data)
        {
            if (data == null)
            {
                return;
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            var meshOwner = GetRendererMeshOwner(data);
            if (meshOwner != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshOwner);
            }
        }

        private static string CreateOwnerId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static void AssignForkToRenderer(VertexPaintData data, Mesh previousFallback, Mesh clone)
        {
            var meshFilter = data.GetComponent<MeshFilter>();
            if (meshFilter != null
                && (meshFilter.sharedMesh == previousFallback || meshFilter.sharedMesh == data.SourceMesh))
            {
                meshFilter.sharedMesh = clone;
            }

            var skinnedRenderer = data.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null
                && (skinnedRenderer.sharedMesh == previousFallback || skinnedRenderer.sharedMesh == data.SourceMesh))
            {
                skinnedRenderer.sharedMesh = clone;
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
