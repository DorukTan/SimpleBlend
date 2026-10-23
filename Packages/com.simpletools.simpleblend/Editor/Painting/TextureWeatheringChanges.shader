Shader "Hidden/SimpleBlend/Weathering Changes"
{
    Properties
    {
        _ChangeMask ("Change mask", 2D) = "black" {}
        _PreviewColor ("Preview color", Color) = (0.3, 0.6, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _ChangeMask;
            float4 _PreviewColor;
            struct Attributes { float4 position : POSITION; float2 paintUV : TEXCOORD3; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.paintUV;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float delta = tex2D(_ChangeMask, input.uv).r;
                clip(delta - 0.001);
                return float4(_PreviewColor.rgb, _PreviewColor.a * (0.18 + saturate(delta) * 0.67));
            }
            ENDHLSL
        }
    }
}
