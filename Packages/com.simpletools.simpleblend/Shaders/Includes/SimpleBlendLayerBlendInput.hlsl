#ifndef SIMPLEBLEND_LAYER_BLEND_INPUT_INCLUDED
#define SIMPLEBLEND_LAYER_BLEND_INPUT_INCLUDED

#if defined(SIMPLEBLEND_TERRAIN)
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendTerrainInput.hlsl"
#else

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendHeightBlend.hlsl"
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendSurfaceBlend.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _Layer0Albedo_ST;
float4 _Layer1Albedo_ST;
float4 _Layer2Albedo_ST;
float4 _Layer3Albedo_ST;
half4 _Layer0Color;
half4 _Layer1Color;
half4 _Layer2Color;
half4 _Layer3Color;
half _Layer0NormalScale;
half _Layer1NormalScale;
half _Layer2NormalScale;
half _Layer3NormalScale;
half _Layer0Metallic;
half _Layer1Metallic;
half _Layer2Metallic;
half _Layer3Metallic;
half _Layer0Smoothness;
half _Layer1Smoothness;
half _Layer2Smoothness;
half _Layer3Smoothness;
half _Layer0OcclusionStrength;
half _Layer1OcclusionStrength;
half _Layer2OcclusionStrength;
half _Layer3OcclusionStrength;
half _BlendTransition;
half _LayerCount;
CBUFFER_END

TEXTURE2D(_Layer0Albedo);
TEXTURE2D(_Layer0Normal);
TEXTURE2D(_Layer0Mask);
TEXTURE2D(_Layer0Height);
TEXTURE2D(_Layer1Albedo);
TEXTURE2D(_Layer1Normal);
TEXTURE2D(_Layer1Mask);
TEXTURE2D(_Layer1Height);
TEXTURE2D(_Layer2Albedo);
TEXTURE2D(_Layer2Normal);
TEXTURE2D(_Layer2Mask);
TEXTURE2D(_Layer2Height);
TEXTURE2D(_Layer3Albedo);
TEXTURE2D(_Layer3Normal);
TEXTURE2D(_Layer3Mask);
TEXTURE2D(_Layer3Height);
float2 SimpleBlendLayerUv(float2 uv, float4 transform)
{
    return uv * transform.xy + transform.zw;
}

half SimpleBlendOcclusion(half sampled, half strength)
{
    return lerp(1.0h, sampled, saturate(strength));
}

void InitializeSimpleBlendSurfaceData(
    float2 uv,
    half4 vertexColor,
    out SurfaceData surfaceData)
{
    float2 uv0 = SimpleBlendLayerUv(uv, _Layer0Albedo_ST);
    float2 uv1 = SimpleBlendLayerUv(uv, _Layer1Albedo_ST);
    float2 uv2 = SimpleBlendLayerUv(uv, _Layer2Albedo_ST);
    float2 uv3 = SimpleBlendLayerUv(uv, _Layer3Albedo_ST);

    half4 rawWeights = half4(
        saturate(1.0h - vertexColor.r - vertexColor.g - (_LayerCount > 3.5h ? vertexColor.b : 0.0h)),
        saturate(vertexColor.r),
        saturate(vertexColor.g),
        _LayerCount > 3.5h ? saturate(vertexColor.b) : 0.0h);
    half4 heights = half4(
        SAMPLE_TEXTURE2D(_Layer0Height, sampler_LinearRepeat, uv0).r,
        SAMPLE_TEXTURE2D(_Layer1Height, sampler_LinearRepeat, uv1).r,
        SAMPLE_TEXTURE2D(_Layer2Height, sampler_LinearRepeat, uv2).r,
        _LayerCount > 3.5h
            ? SAMPLE_TEXTURE2D(_Layer3Height, sampler_LinearRepeat, uv3).r
            : 0.0h);
    half4 weights = _LayerCount > 3.5h
        ? SimpleBlendHeightWeights4(rawWeights, heights, _BlendTransition)
        : half4(SimpleBlendHeightWeights3(rawWeights.xyz, heights.xyz, _BlendTransition), 0.0h);

    half4 albedo0 = SAMPLE_TEXTURE2D(_Layer0Albedo, sampler_LinearRepeat, uv0) * _Layer0Color;
    half4 albedo1 = SAMPLE_TEXTURE2D(_Layer1Albedo, sampler_LinearRepeat, uv1) * _Layer1Color;
    half4 albedo2 = SAMPLE_TEXTURE2D(_Layer2Albedo, sampler_LinearRepeat, uv2) * _Layer2Color;
    half4 albedo3 = _LayerCount > 3.5h
        ? SAMPLE_TEXTURE2D(_Layer3Albedo, sampler_LinearRepeat, uv3) * _Layer3Color
        : half4(0.0h, 0.0h, 0.0h, 1.0h);

    half4 mask0 = SAMPLE_TEXTURE2D(_Layer0Mask, sampler_LinearRepeat, uv0);
    half4 mask1 = SAMPLE_TEXTURE2D(_Layer1Mask, sampler_LinearRepeat, uv1);
    half4 mask2 = SAMPLE_TEXTURE2D(_Layer2Mask, sampler_LinearRepeat, uv2);
    half4 mask3 = _LayerCount > 3.5h
        ? SAMPLE_TEXTURE2D(_Layer3Mask, sampler_LinearRepeat, uv3)
        : half4(0.0h, 1.0h, 0.0h, 0.0h);

    half3 normal0 = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_Layer0Normal, sampler_LinearRepeat, uv0),
        _Layer0NormalScale);
    half3 normal1 = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_Layer1Normal, sampler_LinearRepeat, uv1),
        _Layer1NormalScale);
    half3 normal2 = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_Layer2Normal, sampler_LinearRepeat, uv2),
        _Layer2NormalScale);
    half3 normal3 = _LayerCount > 3.5h
        ? UnpackNormalScale(
            SAMPLE_TEXTURE2D(_Layer3Normal, sampler_LinearRepeat, uv3),
            _Layer3NormalScale)
        : half3(0.0h, 0.0h, 1.0h);

    half4 packedMask0 = half4(
        mask0.r * _Layer0Metallic,
        SimpleBlendOcclusion(mask0.g, _Layer0OcclusionStrength),
        mask0.b,
        mask0.a * _Layer0Smoothness);
    half4 packedMask1 = half4(
        mask1.r * _Layer1Metallic,
        SimpleBlendOcclusion(mask1.g, _Layer1OcclusionStrength),
        mask1.b,
        mask1.a * _Layer1Smoothness);
    half4 packedMask2 = half4(
        mask2.r * _Layer2Metallic,
        SimpleBlendOcclusion(mask2.g, _Layer2OcclusionStrength),
        mask2.b,
        mask2.a * _Layer2Smoothness);
    half4 packedMask3 = half4(
        mask3.r * _Layer3Metallic,
        SimpleBlendOcclusion(mask3.g, _Layer3OcclusionStrength),
        mask3.b,
        mask3.a * _Layer3Smoothness);
    half3 blendedAlbedo;
    half3 blendedNormal;
    half4 blendedMask;
    SimpleBlendSurfaceBlend4(
        weights,
        albedo0.rgb,
        albedo1.rgb,
        albedo2.rgb,
        albedo3.rgb,
        normal0,
        normal1,
        normal2,
        normal3,
        packedMask0,
        packedMask1,
        packedMask2,
        packedMask3,
        blendedAlbedo,
        blendedNormal,
        blendedMask);

    surfaceData = (SurfaceData)0;
    surfaceData.albedo = blendedAlbedo;
    surfaceData.alpha = 1.0h;
    surfaceData.metallic = blendedMask.r;
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.smoothness = blendedMask.a;
    surfaceData.normalTS = blendedNormal;
    surfaceData.occlusion = blendedMask.g;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

void InitializeSimpleBlendSurfaceData(float2 uv, half4 vertexColor, float3 positionWS,
    half3 normalWS, half4 tangentWS, out SurfaceData surfaceData)
{
    InitializeSimpleBlendSurfaceData(uv, vertexColor, surfaceData);
}

#endif // SIMPLEBLEND_TERRAIN
#endif
