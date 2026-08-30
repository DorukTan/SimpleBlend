Shader "Hidden/SimpleBlend/Red Channel Preview"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed red : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.red = input.color.r;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                return fixed4(input.red, 0.0, 0.0, 0.18 + input.red * 0.67);
            }
            ENDHLSL
        }
    }
}
