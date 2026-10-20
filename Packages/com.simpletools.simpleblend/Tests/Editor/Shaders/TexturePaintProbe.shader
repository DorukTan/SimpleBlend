Shader "Hidden/SimpleBlend/Texture Paint Probe"
{
    Properties
    {
        _UseTextureWeights("Texture Weights", Float) = 0
        _BlendMask("Mask", 2D) = "black" {}
        _ProbeVertex("Vertex Color", Color) = (0, 1, 0, 0)
        _ProbeUv("Mask UV", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendLayerBlendInput.hlsl"
            float4 _ProbeVertex;
            float4 _ProbeUv;
            struct Attributes { float4 vertex : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.vertex.xyz);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                return SimpleBlendPaintWeights(_ProbeVertex, _ProbeUv.xy);
            }
            ENDHLSL
        }
    }
}
