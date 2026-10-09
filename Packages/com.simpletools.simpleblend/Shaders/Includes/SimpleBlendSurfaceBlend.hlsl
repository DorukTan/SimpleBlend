#ifndef SIMPLEBLEND_SURFACE_BLEND_INCLUDED
#define SIMPLEBLEND_SURFACE_BLEND_INCLUDED

half4 SimpleBlendNormalizeSurfaceWeights4(half4 weights)
{
    weights = max(weights, 0.0h);
    half total = weights.x + weights.y + weights.z + weights.w;
    return total > 0.0001h
        ? weights / total
        : half4(1.0h, 0.0h, 0.0h, 0.0h);
}

void SimpleBlendSurfaceBlend4(
    half4 weights,
    half3 albedo0,
    half3 albedo1,
    half3 albedo2,
    half3 albedo3,
    half3 normal0,
    half3 normal1,
    half3 normal2,
    half3 normal3,
    half4 mask0,
    half4 mask1,
    half4 mask2,
    half4 mask3,
    out half3 albedo,
    out half3 normal,
    out half4 mask)
{
    weights = SimpleBlendNormalizeSurfaceWeights4(weights);
    albedo = albedo0 * weights.x
           + albedo1 * weights.y
           + albedo2 * weights.z
           + albedo3 * weights.w;
    half3 weightedNormal = normal0 * weights.x
                         + normal1 * weights.y
                         + normal2 * weights.z
                         + normal3 * weights.w;
    normal = dot(weightedNormal, weightedNormal) > 0.0001h
        ? normalize(weightedNormal)
        : half3(0.0h, 0.0h, 1.0h);
    mask = mask0 * weights.x
         + mask1 * weights.y
         + mask2 * weights.z
         + mask3 * weights.w;
}

void SimpleBlendSurfaceBlend4_half(
    half4 Weights,
    half3 Albedo0,
    half3 Albedo1,
    half3 Albedo2,
    half3 Albedo3,
    half3 Normal0,
    half3 Normal1,
    half3 Normal2,
    half3 Normal3,
    half4 Mask0,
    half4 Mask1,
    half4 Mask2,
    half4 Mask3,
    out half3 Albedo,
    out half3 Normal,
    out half4 Mask)
{
    SimpleBlendSurfaceBlend4(
        Weights,
        Albedo0,
        Albedo1,
        Albedo2,
        Albedo3,
        Normal0,
        Normal1,
        Normal2,
        Normal3,
        Mask0,
        Mask1,
        Mask2,
        Mask3,
        Albedo,
        Normal,
        Mask);
}

void SimpleBlendSurfaceBlend4_float(
    float4 Weights,
    float3 Albedo0,
    float3 Albedo1,
    float3 Albedo2,
    float3 Albedo3,
    float3 Normal0,
    float3 Normal1,
    float3 Normal2,
    float3 Normal3,
    float4 Mask0,
    float4 Mask1,
    float4 Mask2,
    float4 Mask3,
    out float3 Albedo,
    out float3 Normal,
    out float4 Mask)
{
    half3 albedo;
    half3 normal;
    half4 mask;
    SimpleBlendSurfaceBlend4(
        (half4)Weights,
        (half3)Albedo0,
        (half3)Albedo1,
        (half3)Albedo2,
        (half3)Albedo3,
        (half3)Normal0,
        (half3)Normal1,
        (half3)Normal2,
        (half3)Normal3,
        (half4)Mask0,
        (half4)Mask1,
        (half4)Mask2,
        (half4)Mask3,
        albedo,
        normal,
        mask);
    Albedo = albedo;
    Normal = normal;
    Mask = mask;
}

#endif
