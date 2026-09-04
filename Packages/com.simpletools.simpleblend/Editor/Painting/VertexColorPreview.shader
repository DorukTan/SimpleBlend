Shader "Hidden/SimpleBlend/Vertex Color Preview"
{
    Properties
    {
        _PreviewMode ("Preview Mode", Float) = 0
    }

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

            float _PreviewMode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                fixed4 color : COLOR;
                float2 density : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float density : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.color = input.color;
                output.density = input.density.x;
                return output;
            }

            fixed PreviewAlpha(fixed intensity)
            {
                return 0.18 + saturate(intensity) * 0.67;
            }

            fixed3 DensityColor(fixed density)
            {
                fixed3 cold = fixed3(0.05, 0.15, 1.0);
                fixed3 middle = fixed3(0.0, 1.0, 0.35);
                fixed3 hot = fixed3(1.0, 0.15, 0.0);
                density = saturate(density);
                return density < 0.5
                    ? lerp(cold, middle, density * 2.0)
                    : lerp(middle, hot, (density - 0.5) * 2.0);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                if (_PreviewMode < 0.5)
                {
                    return fixed4(input.color.r, 0.0, 0.0, PreviewAlpha(input.color.r));
                }

                if (_PreviewMode < 1.5)
                {
                    return fixed4(0.0, input.color.g, 0.0, PreviewAlpha(input.color.g));
                }

                if (_PreviewMode < 2.5)
                {
                    return fixed4(0.0, 0.0, input.color.b, PreviewAlpha(input.color.b));
                }

                if (_PreviewMode < 3.5)
                {
                    return fixed4(input.color.aaa, PreviewAlpha(input.color.a));
                }

                if (_PreviewMode < 4.5)
                {
                    fixed intensity = max(input.color.r, max(input.color.g, input.color.b));
                    return fixed4(input.color.rgb, PreviewAlpha(intensity));
                }

                return fixed4(DensityColor(input.density), 0.85);
            }
            ENDHLSL
        }
    }
}
