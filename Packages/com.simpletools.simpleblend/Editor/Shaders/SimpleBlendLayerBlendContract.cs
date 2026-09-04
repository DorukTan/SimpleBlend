using System;

namespace SimpleBlend.Editor
{
    internal static class SimpleBlendLayerBlendContract
    {
        internal const string LayerBlend3ShaderName = "SimpleBlend/Layer Blend 3";
        internal const string LayerBlend4ShaderName = "SimpleBlend/Layer Blend 4";
        internal const string MaskConvention = "R Metallic, G Ambient Occlusion, B Reserved, A Smoothness";

        internal static readonly string[] RequiredPassNames =
        {
            "ForwardLit",
            "GBuffer",
            "ShadowCaster",
            "DepthOnly",
            "DepthNormals",
            "Meta"
        };

        internal static int GetLayerCount(string shaderName)
        {
            switch (shaderName)
            {
                case LayerBlend3ShaderName:
                    return 3;
                case LayerBlend4ShaderName:
                    return 4;
                default:
                    throw new ArgumentException(
                        $"'{shaderName}' is not a SimpleBlend layer shader.",
                        nameof(shaderName));
            }
        }

        internal static string GetLayerLabel(int layerIndex)
        {
            switch (layerIndex)
            {
                case 0:
                    return "Base Layer — Unpainted";
                case 1:
                    return "Layer 1 — Red Channel";
                case 2:
                    return "Layer 2 — Green Channel";
                case 3:
                    return "Layer 3 — Blue Channel";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layerIndex));
            }
        }
    }
}
