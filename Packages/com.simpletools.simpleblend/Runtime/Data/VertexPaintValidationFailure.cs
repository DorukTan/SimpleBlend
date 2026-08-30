namespace SimpleBlend
{
    public enum VertexPaintValidationFailure
    {
        None = 0,
        MissingRenderer = 1,
        MissingMeshFilter = 2,
        MissingSourceMesh = 3,
        MissingSignature = 4,
        SourceIdentityMismatch = 5,
        VertexCountMismatch = 6,
        SubMeshLayoutMismatch = 7,
        TopologyMismatch = 8,
        ColorCountMismatch = 9,
        SourceDataUnavailable = 10,
        StorageModeUnresolved = 11,
        UnsupportedStorageMode = 12,
        RendererMeshMismatch = 13,
        MissingGeneratedMesh = 14,
        GeneratedMeshMismatch = 15,
        GeneratedAssetWriteFailed = 16,
        UnsupportedPaintTarget = 17,
        NoTriangleGeometry = 18
    }
}
