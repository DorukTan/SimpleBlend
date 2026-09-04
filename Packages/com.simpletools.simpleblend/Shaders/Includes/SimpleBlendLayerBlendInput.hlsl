#ifndef SIMPLEBLEND_LAYER_BLEND_INPUT_INCLUDED
#define SIMPLEBLEND_LAYER_BLEND_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendHeightBlend.hlsl"

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

half3 SimpleBlendWeightedNormal(
    half3 first,
    half3 second,
    half3 third,
    half3 fourth,
    half4 weights)
{
    half3 blended = first * weights.x
                  + second * weights.y
                  + third * weights.z
                  + fourth * weights.w;
    return dot(blended, blended) > 0.0001h
        ? normalize(blended)
        : half3(0.0h, 0.0h, 1.0h);
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

    surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo0.rgb * weights.x
                       + albedo1.rgb * weights.y
                       + albedo2.rgb * weights.z
                       + albedo3.rgb * weights.w;
    surfaceData.alpha = 1.0h;
    surfaceData.metallic = mask0.r * _Layer0Metallic * weights.x
                         + mask1.r * _Layer1Metallic * weights.y
                         + mask2.r * _Layer2Metallic * weights.z
                         + mask3.r * _Layer3Metallic * weights.w;
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.smoothness = mask0.a * _Layer0Smoothness * weights.x
                           + mask1.a * _Layer1Smoothness * weights.y
                           + mask2.a * _Layer2Smoothness * weights.z
                           + mask3.a * _Layer3Smoothness * weights.w;
    surfaceData.normalTS = SimpleBlendWeightedNormal(
        normal0,
        normal1,
        normal2,
        normal3,
        weights);
    surfaceData.occlusion = SimpleBlendOcclusion(mask0.g, _Layer0OcclusionStrength) * weights.x
                          + SimpleBlendOcclusion(mask1.g, _Layer1OcclusionStrength) * weights.y
                          + SimpleBlendOcclusion(mask2.g, _Layer2OcclusionStrength) * weights.z
                          + SimpleBlendOcclusion(mask3.g, _Layer3OcclusionStrength) * weights.w;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

#endif
