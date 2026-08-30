namespace SimpleBlend
{
    public static class VertexPaintText
    {
        public static string For(VertexPaintStorageReason reason)
        {
            switch (reason)
            {
                case VertexPaintStorageReason.OrdinaryMeshRenderer:
                    return "Additional Vertex Stream — safe per-renderer storage for an ordinary MeshRenderer.";
                case VertexPaintStorageReason.SkinnedMeshRenderer:
                    return "Cloned Mesh — required because SkinnedMeshRenderer has no Additional Vertex Streams API.";
                case VertexPaintStorageReason.StaticBatching:
                    return "Cloned Mesh — required because static batching consumes and clears Additional Vertex Streams.";
                case VertexPaintStorageReason.GpuResidentDrawer:
                    return "Cloned Mesh — required while Additional Vertex Streams remain undocumented with GPU Resident Drawer.";
                default:
                    return "Storage has not been resolved yet.";
            }
        }

        public static string For(VertexPaintValidationFailure failure)
        {
            switch (failure)
            {
                case VertexPaintValidationFailure.None:
                    return "Paint data is valid.";
                case VertexPaintValidationFailure.MissingRenderer:
                    return "Add VertexPaintData to a GameObject with a MeshRenderer or SkinnedMeshRenderer.";
                case VertexPaintValidationFailure.MissingMeshFilter:
                    return "This MeshRenderer needs a MeshFilter with a source mesh.";
                case VertexPaintValidationFailure.MissingSourceMesh:
                    return "The original source mesh reference is missing. SimpleBlend will not guess a replacement.";
                case VertexPaintValidationFailure.MissingSignature:
                    return "The source signature is missing. Reinitialize or clear this paint data.";
                case VertexPaintValidationFailure.SourceIdentityMismatch:
                    return "The referenced source asset is not the asset this paint data was created for.";
                case VertexPaintValidationFailure.VertexCountMismatch:
                    return "The source vertex count changed. Clear or migrate the paint data before applying it.";
                case VertexPaintValidationFailure.SubMeshLayoutMismatch:
                    return "The source submesh layout changed. SimpleBlend refused to apply index-based paint data.";
                case VertexPaintValidationFailure.TopologyMismatch:
                    return "The source index topology changed even though its vertex count may be unchanged.";
                case VertexPaintValidationFailure.ColorCountMismatch:
                    return "The serialized color count does not match the signed source vertex count.";
                case VertexPaintValidationFailure.SourceDataUnavailable:
                    return "Unity could not provide read-only source mesh data. No paint data was applied.";
                case VertexPaintValidationFailure.StorageModeUnresolved:
                    return "Storage mode has not been resolved for this renderer.";
                case VertexPaintValidationFailure.UnsupportedStorageMode:
                    return "The selected storage mode is not supported by this renderer.";
                case VertexPaintValidationFailure.RendererMeshMismatch:
                    return "The renderer now references a different mesh. SimpleBlend will not overwrite it.";
                case VertexPaintValidationFailure.MissingGeneratedMesh:
                    return "The generated fallback mesh is missing. Rebuild it or clear the paint data.";
                case VertexPaintValidationFailure.GeneratedMeshMismatch:
                    return "The generated fallback mesh no longer matches the signed source mesh.";
                case VertexPaintValidationFailure.GeneratedAssetWriteFailed:
                    return "Unity could not create the generated fallback mesh asset.";
                case VertexPaintValidationFailure.UnsupportedPaintTarget:
                    return "The selected renderer is not supported by the current paint tool.";
                case VertexPaintValidationFailure.NoTriangleGeometry:
                    return "The source mesh has no triangle geometry to paint.";
                default:
                    return "Paint data is invalid for an unknown reason.";
            }
        }
    }
}
