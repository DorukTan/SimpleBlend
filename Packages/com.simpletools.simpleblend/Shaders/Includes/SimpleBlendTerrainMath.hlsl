#ifndef SIMPLEBLEND_TERRAIN_MATH_INCLUDED
#define SIMPLEBLEND_TERRAIN_MATH_INCLUDED

struct SimpleBlendTerrainLayer
{
    half3 albedo;
    half3 normalTS;
    half metallic;
    half occlusion;
    half smoothness;
    half height;
};

float2 SimpleBlendTerrainControlUv(float3 positionWS, float4 origin, float4 size)
{
    return (positionWS.xz - origin.xz) / max(size.xz, float2(0.0001, 0.0001));
}

float2 SimpleBlendTerrainControlSampleUv(float2 controlUv, float4 texelSize)
{
    return (saturate(controlUv) * max(texelSize.zw - 1.0, 0.0) + 0.5) * texelSize.xy;
}

float2 SimpleBlendTerrainLayerUv(float3 positionWS, float4 origin, float4 tileSizeOffset)
{
    return (positionWS.xz - origin.xz + tileSizeOffset.zw) / tileSizeOffset.xy;
}

half4 SimpleBlendTerrainControlWeights(half4 control, half layerCount)
{
    half4 active = half4(layerCount > 0.5h, layerCount > 1.5h, layerCount > 2.5h, layerCount > 3.5h);
    half4 weights = max(control, 0.0h) * active;
    half total = dot(weights, half4(1, 1, 1, 1));
    return total > 0.0001h ? weights / total : half4(1, 0, 0, 0);
}

half3 SimpleBlendSafeNormal(half3 normal)
{
    return dot(normal, normal) > 0.0001h ? normalize(normal) : half3(0, 0, 1);
}

SimpleBlendTerrainLayer SimpleBlendDecodeTerrainLayer(half4 diffuse, half4 mask, half3 normal,
    float4 surface, float4 flags, float4 diffuseMin, float4 diffuseMax,
    float4 maskMin, float4 maskMax)
{
    SimpleBlendTerrainLayer layer;
    half4 remapped = mask * (maskMax - maskMin) + maskMin;
    half4 color = flags.x > 0.5 ? diffuse : half4(1, 1, 1, 1);
    layer.albedo = color.rgb * (diffuseMax.rgb - diffuseMin.rgb);
    layer.normalTS = flags.y > 0.5 ? normal : half3(0, 0, 1);
    layer.metallic = flags.z > 0.5 ? remapped.r : surface.y;
    layer.occlusion = flags.z > 0.5 ? remapped.g : maskMax.g;
    layer.smoothness = flags.z > 0.5 ? remapped.a : (surface.w > 0.5 ? color.a : surface.z);
    layer.height = flags.z > 0.5 ? remapped.b : maskMax.b;
    return layer;
}

SimpleBlendTerrainLayer SimpleBlendMixTerrainLayers(SimpleBlendTerrainLayer a,
    SimpleBlendTerrainLayer b, SimpleBlendTerrainLayer c, SimpleBlendTerrainLayer d, half4 weights)
{
    SimpleBlendTerrainLayer result;
    result.albedo = a.albedo * weights.x + b.albedo * weights.y + c.albedo * weights.z + d.albedo * weights.w;
    result.normalTS = SimpleBlendSafeNormal(a.normalTS * weights.x + b.normalTS * weights.y
        + c.normalTS * weights.z + d.normalTS * weights.w);
    result.metallic = dot(half4(a.metallic, b.metallic, c.metallic, d.metallic), weights);
    result.occlusion = dot(half4(a.occlusion, b.occlusion, c.occlusion, d.occlusion), weights);
    result.smoothness = dot(half4(a.smoothness, b.smoothness, c.smoothness, d.smoothness), weights);
    result.height = dot(half4(a.height, b.height, c.height, d.height), weights);
    return result;
}

half3 SimpleBlendProjectedNormalToMesh(half3 projectedNormal, half3 geometricNormal, half4 meshTangent)
{
    half3 n = normalize(geometricNormal);
    // Terrain's U direction is +world X and V direction is +world Z on a flat surface.
    half3 projectedTangent = cross(n, half3(0, 0, 1));
    if (dot(projectedTangent, projectedTangent) < 0.0001h)
        projectedTangent = cross(n, half3(0, 1, 0));
    projectedTangent = normalize(projectedTangent);
    half3 projectedBitangent = normalize(cross(projectedTangent, n));
    half3 worldNormal = SimpleBlendSafeNormal(projectedTangent * projectedNormal.x
        + projectedBitangent * projectedNormal.y + n * projectedNormal.z);
    half3 tangent = normalize(meshTangent.xyz);
    half3 bitangent = meshTangent.w * normalize(cross(n, tangent));
    return half3(dot(worldNormal, tangent), dot(worldNormal, bitangent), dot(worldNormal, n));
}

#endif
