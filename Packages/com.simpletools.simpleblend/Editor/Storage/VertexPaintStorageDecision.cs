namespace SimpleBlend.Editor
{
    internal readonly struct VertexPaintStorageDecision
    {
        internal VertexPaintStorageDecision(VertexPaintStorageMode mode, VertexPaintStorageReason reason)
        {
            Mode = mode;
            Reason = reason;
        }

        internal VertexPaintStorageMode Mode { get; }

        internal VertexPaintStorageReason Reason { get; }
    }
}
