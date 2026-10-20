#ifndef SIMPLEBLEND_LAYER_BLEND_UTILITY_PASSES_INCLUDED
#define SIMPLEBLEND_LAYER_BLEND_UTILITY_PASSES_INCLUDED

#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendLayerBlendInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct SimpleBlendUtilityAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 paintUv : TEXCOORD3;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SimpleBlendDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

SimpleBlendDepthVaryings SimpleBlendDepthVertex(SimpleBlendUtilityAttributes input)
{
    SimpleBlendDepthVaryings output = (SimpleBlendDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half SimpleBlendDepthFragment(SimpleBlendDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return input.positionCS.z;
}

SimpleBlendDepthVaryings SimpleBlendShadowVertex(SimpleBlendUtilityAttributes input)
{
    SimpleBlendDepthVaryings output = (SimpleBlendDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}

half4 SimpleBlendShadowFragment(SimpleBlendDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return 0.0h;
}

struct SimpleBlendNormalVaryings
{
    float2 paintUv : TEXCOORD5;
    float3 positionWS : TEXCOORD4;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    half4 color : TEXCOORD3;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

SimpleBlendNormalVaryings SimpleBlendDepthNormalsVertex(SimpleBlendUtilityAttributes input)
{
    SimpleBlendNormalVaryings output = (SimpleBlendNormalVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.uv = input.uv;
    output.paintUv = input.paintUv;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(
        normalInputs.tangentWS,
        input.tangentOS.w * GetOddNegativeScale());
    output.color = input.color;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 SimpleBlendDepthNormalsFragment(SimpleBlendNormalVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData;
    InitializeSimpleBlendSurfaceData(input.uv, SimpleBlendPaintWeights(input.color, input.paintUv), input.positionWS,
        input.normalWS, input.tangentWS, surfaceData);
    half3 bitangentWS = input.tangentWS.w
                      * cross(input.normalWS, input.tangentWS.xyz);
    half3 normalWS = TransformTangentToWorld(
        surfaceData.normalTS,
        half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
    normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remappedOctNormalWS), 0.0h);
#else
    return half4(normalWS, 0.0h);
#endif
}

struct SimpleBlendMetaVaryings
{
    float2 paintUv : TEXCOORD5;
    float3 positionWS : TEXCOORD2;
    half3 normalWS : TEXCOORD3;
    half4 tangentWS : TEXCOORD4;
    float2 uv : TEXCOORD0;
    half4 color : TEXCOORD1;
    float4 positionCS : SV_POSITION;
};

SimpleBlendMetaVaryings SimpleBlendMetaVertex(SimpleBlendUtilityAttributes input)
{
    SimpleBlendMetaVaryings output = (SimpleBlendMetaVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.positionCS = UnityMetaVertexPosition(
        input.positionOS.xyz,
        input.uv1,
        input.uv2);
    output.uv = input.uv;
    output.paintUv = input.paintUv;
    output.color = input.color;
    return output;
}

half4 SimpleBlendMetaFragment(SimpleBlendMetaVaryings input) : SV_Target
{
    SurfaceData surfaceData;
    InitializeSimpleBlendSurfaceData(input.uv, SimpleBlendPaintWeights(input.color, input.paintUv), input.positionWS,
        input.normalWS, input.tangentWS, surfaceData);
    BRDFData brdfData;
    InitializeBRDFData(
        surfaceData.albedo,
        surfaceData.metallic,
        surfaceData.specular,
        surfaceData.smoothness,
        surfaceData.alpha,
        brdfData);
    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse
                     + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = surfaceData.emission;
    return UnityMetaFragment(metaInput);
}

#endif
