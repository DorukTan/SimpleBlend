#ifndef SIMPLEBLEND_LAYER_BLEND_PASSES_INCLUDED
#define SIMPLEBLEND_LAYER_BLEND_PASSES_INCLUDED

#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendLayerBlendInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct SimpleBlendAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SimpleBlendVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half4 color : TEXCOORD4;
    half4 fogAndVertexLight : TEXCOORD5;
    float4 shadowCoord : TEXCOORD6;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD8;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

SimpleBlendVaryings SimpleBlendLitVertex(SimpleBlendAttributes input)
{
    SimpleBlendVaryings output = (SimpleBlendVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half tangentSign = input.tangentOS.w * GetOddNegativeScale();

    output.uv = input.uv;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, tangentSign);
    output.color = input.color;
    output.fogAndVertexLight = half4(
        ComputeFogFactor(positionInputs.positionCS.z),
        VertexLighting(positionInputs.positionWS, normalInputs.normalWS));
    output.shadowCoord = GetShadowCoord(positionInputs);
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy
                             + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH(output.normalWS, output.vertexSH);
    output.positionCS = positionInputs.positionCS;
    return output;
}

void InitializeSimpleBlendInputData(
    SimpleBlendVaryings input,
    half3 normalTS,
    out InputData inputData)
{
    inputData = (InputData)0;
    half tangentSign = input.tangentWS.w;
    half3 bitangentWS = tangentSign * cross(input.normalWS, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(
        input.tangentWS.xyz,
        bitangentWS,
        input.normalWS);

    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(
        TransformTangentToWorld(normalTS, tangentToWorld));
    inputData.tangentToWorld = tangentToWorld;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.shadowCoord = input.shadowCoord;
    inputData.fogCoord = input.fogAndVertexLight.x;
    inputData.vertexLighting = input.fogAndVertexLight.yzw;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#ifdef DYNAMICLIGHTMAP_ON
    inputData.bakedGI = SAMPLE_GI(
        input.staticLightmapUV,
        input.dynamicLightmapUV,
        input.vertexSH,
        inputData.normalWS);
#else
    inputData.bakedGI = SAMPLE_GI(
        input.staticLightmapUV,
        input.vertexSH,
        inputData.normalWS);
#endif
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
}

void SimpleBlendForwardFragment(
    SimpleBlendVaryings input,
    out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeSimpleBlendSurfaceData(input.uv, input.color, input.positionWS,
        input.normalWS, input.tangentWS, surfaceData);
    InputData inputData;
    InitializeSimpleBlendInputData(input, surfaceData.normalTS, inputData);

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = half4(color.rgb, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
