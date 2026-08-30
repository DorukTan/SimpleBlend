using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimpleBlend.Editor
{
    internal static class VertexPaintStoragePolicy
    {
        internal static VertexPaintStorageDecision Resolve(Renderer renderer, bool gpuResidentDrawerEnabled)
        {
            if (renderer is SkinnedMeshRenderer)
            {
                return new VertexPaintStorageDecision(
                    VertexPaintStorageMode.ClonedMesh,
                    VertexPaintStorageReason.SkinnedMeshRenderer);
            }

            if (!(renderer is MeshRenderer meshRenderer))
            {
                return new VertexPaintStorageDecision(
                    VertexPaintStorageMode.Unresolved,
                    VertexPaintStorageReason.None);
            }

            if (GameObjectUtility.AreStaticEditorFlagsSet(
                    meshRenderer.gameObject,
                    StaticEditorFlags.BatchingStatic)
                || meshRenderer.isPartOfStaticBatch)
            {
                return new VertexPaintStorageDecision(
                    VertexPaintStorageMode.ClonedMesh,
                    VertexPaintStorageReason.StaticBatching);
            }

            if (gpuResidentDrawerEnabled)
            {
                return new VertexPaintStorageDecision(
                    VertexPaintStorageMode.ClonedMesh,
                    VertexPaintStorageReason.GpuResidentDrawer);
            }

            return new VertexPaintStorageDecision(
                VertexPaintStorageMode.AdditionalVertexStream,
                VertexPaintStorageReason.OrdinaryMeshRenderer);
        }

        internal static bool IsGpuResidentDrawerEnabled()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return pipelineAsset != null
                   && pipelineAsset.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled;
        }
    }
}
