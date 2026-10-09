#ifndef SIMPLEBLEND_GRAPH_INPUT_INCLUDED
#define SIMPLEBLEND_GRAPH_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

void SimpleBlendGraphLayer_float(
    float2 UV, UnityTexture2D Albedo, UnityTexture2D Normal,
    UnityTexture2D Mask, UnityTexture2D Height, float4 Tint,
    float NormalScale, float Metallic, float Smoothness, float OcclusionStrength,
    out float3 AlbedoOut, out float3 NormalOut, out float4 MaskOut, out float HeightOut)
{
    float2 uv = UV * Albedo.scaleTranslate.xy + Albedo.scaleTranslate.zw;
    half4 albedo = SAMPLE_TEXTURE2D(Albedo.tex, sampler_LinearRepeat, uv) * (half4)Tint;
    half4 mask = SAMPLE_TEXTURE2D(Mask.tex, sampler_LinearRepeat, uv);
    AlbedoOut = albedo.rgb;
    NormalOut = UnpackNormalScale(
        SAMPLE_TEXTURE2D(Normal.tex, sampler_LinearRepeat, uv), (half)NormalScale);
    MaskOut = half4(mask.r * (half)Metallic,
        lerp(1.0h, mask.g, saturate((half)OcclusionStrength)),
        mask.b, mask.a * (half)Smoothness);
    HeightOut = SAMPLE_TEXTURE2D(Height.tex, sampler_LinearRepeat, uv).r;
}

void SimpleBlendGraphLayer_half(
    half2 UV, UnityTexture2D Albedo, UnityTexture2D Normal,
    UnityTexture2D Mask, UnityTexture2D Height, half4 Tint,
    half NormalScale, half Metallic, half Smoothness, half OcclusionStrength,
    out half3 AlbedoOut, out half3 NormalOut, out half4 MaskOut, out half HeightOut)
{
    float3 albedo;
    float3 normal;
    float4 mask;
    float height;
    SimpleBlendGraphLayer_float(UV, Albedo, Normal, Mask, Height, Tint,
        NormalScale, Metallic, Smoothness, OcclusionStrength,
        albedo, normal, mask, height);
    AlbedoOut = albedo;
    NormalOut = normal;
    MaskOut = mask;
    HeightOut = height;
}

void SimpleBlendGraphInputs3_float(float4 VertexColor,
    float Height0, float Height1, float Height2,
    out float3 RawWeights, out float3 Heights)
{
    RawWeights = float3(saturate(1.0 - VertexColor.r - VertexColor.g),
        saturate(VertexColor.r), saturate(VertexColor.g));
    Heights = float3(Height0, Height1, Height2);
}

void SimpleBlendGraphInputs3_half(half4 VertexColor,
    half Height0, half Height1, half Height2,
    out half3 RawWeights, out half3 Heights)
{
    float3 rawWeights;
    float3 heights;
    SimpleBlendGraphInputs3_float(VertexColor, Height0, Height1, Height2,
        rawWeights, heights);
    RawWeights = rawWeights;
    Heights = heights;
}

void SimpleBlendGraphInputs4_float(float4 VertexColor,
    float Height0, float Height1, float Height2, float Height3,
    out float4 RawWeights, out float4 Heights)
{
    RawWeights = float4(saturate(1.0 - VertexColor.r - VertexColor.g - VertexColor.b),
        saturate(VertexColor.r), saturate(VertexColor.g), saturate(VertexColor.b));
    Heights = float4(Height0, Height1, Height2, Height3);
}

void SimpleBlendGraphInputs4_half(half4 VertexColor,
    half Height0, half Height1, half Height2, half Height3,
    out half4 RawWeights, out half4 Heights)
{
    float4 rawWeights;
    float4 heights;
    SimpleBlendGraphInputs4_float(VertexColor, Height0, Height1, Height2, Height3,
        rawWeights, heights);
    RawWeights = rawWeights;
    Heights = heights;
}

void SimpleBlendGraphExpand3_float(float3 Weights, out float4 Expanded)
{
    Expanded = float4(Weights, 0.0);
}

void SimpleBlendGraphExpand3_half(half3 Weights, out half4 Expanded)
{
    Expanded = half4(Weights, 0.0h);
}

#endif
