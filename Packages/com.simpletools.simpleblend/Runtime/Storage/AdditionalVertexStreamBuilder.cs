using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend
{
    internal static class AdditionalVertexStreamBuilder
    {
        private static readonly VertexAttributeDescriptor[] ColorLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
        };

        internal static Mesh BuildOrUpdate(VertexPaintData data)
        {
            var stream = data.TransientStream;
            if (stream == null || stream.vertexCount != data.ColorCount)
            {
                VertexPaintRuntimeApplicator.DetachTransient(data);
                stream = new Mesh
                {
                    name = $"{data.gameObject.name} SimpleBlend Stream",
                    hideFlags = HideFlags.HideAndDontSave,
                    bounds = data.SourceMesh.bounds
                };
                stream.MarkDynamic();
                stream.SetVertexBufferParams(data.ColorCount, ColorLayout);
                data.TransientStream = stream;
            }

            stream.SetVertexBufferData(
                data.SerializedColors,
                0,
                0,
                data.ColorCount,
                0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            stream.bounds = data.SourceMesh.bounds;
            return stream;
        }
    }
}
