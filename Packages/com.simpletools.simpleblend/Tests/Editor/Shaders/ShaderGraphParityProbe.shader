Shader "Hidden/SimpleBlend/Shader Graph Parity Probe"
{
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendHeightBlend.hlsl"
            #include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendSurfaceBlend.hlsl"
            float4 _RawWeights;
            float4 _Heights;
            float _Transition;
            float _LayerCount;
            float _UseFloat;
            float _Output;
            float _UseMaterialBlend;
            float _HeightBlendStrength;
            float4 _Albedo0, _Albedo1, _Albedo2, _Albedo3;
            float4 _Normal0, _Normal1, _Normal2, _Normal3;
            float4 _Mask0, _Mask1, _Mask2, _Mask3;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 weights;
                if (_UseMaterialBlend > 0.5)
                    weights = SimpleBlendMaterialWeights(_RawWeights, _Heights, _Transition, _LayerCount, _HeightBlendStrength);
                else if (_UseFloat > 0.5)
                {
                    if (_LayerCount < 3.5)
                    {
                        float3 w;
                        SimpleBlendHeightWeights3_float(_RawWeights.xyz, _Heights.xyz, _Transition, w);
                        weights = float4(w, 0.0);
                    }
                    else
                        SimpleBlendHeightWeights4_float(_RawWeights, _Heights, _Transition, weights);
                }
                else
                {
                    if (_LayerCount < 3.5)
                    {
                        half3 w;
                        SimpleBlendHeightWeights3_half(_RawWeights.xyz, _Heights.xyz, _Transition, w);
                        weights = float4(w, 0.0);
                    }
                    else
                    {
                        half4 w;
                        SimpleBlendHeightWeights4_half(_RawWeights, _Heights, _Transition, w);
                        weights = w;
                    }
                }
                if (_Output < 0.5)
                    return weights;
                float3 albedo;
                float3 normal;
                float4 mask;
                SimpleBlendSurfaceBlend4_float(weights,
                    _Albedo0.xyz, _Albedo1.xyz, _Albedo2.xyz, _Albedo3.xyz,
                    _Normal0.xyz, _Normal1.xyz, _Normal2.xyz, _Normal3.xyz,
                    _Mask0, _Mask1, _Mask2, _Mask3, albedo, normal, mask);
                if (_Output < 1.5)
                    return mask;
                if (_Output < 2.5)
                    return float4(normal, 1.0);
                return float4(albedo, 1.0);
            }
            ENDCG
        }
    }
}
