#ifndef SIMPLEBLEND_HEIGHT_BLEND_INCLUDED
#define SIMPLEBLEND_HEIGHT_BLEND_INCLUDED

half3 SimpleBlendHeightWeights3(half3 rawWeights, half3 heights, half transition)
{
    rawWeights = max(rawWeights, 0.0h);
    transition = max(transition, 0.0001h);
    half3 scores = saturate(heights) + rawWeights;
    half highest = max(scores.x, max(scores.y, scores.z));
    half3 contributions = max(scores - highest + transition, 0.0h) * rawWeights;
    half total = contributions.x + contributions.y + contributions.z;
    if (total <= 0.0001h)
    {
        half rawTotal = rawWeights.x + rawWeights.y + rawWeights.z;
        return rawTotal > 0.0001h
            ? rawWeights / rawTotal
            : half3(1.0h, 0.0h, 0.0h);
    }

    return contributions / total;
}

half4 SimpleBlendHeightWeights4(half4 rawWeights, half4 heights, half transition)
{
    rawWeights = max(rawWeights, 0.0h);
    transition = max(transition, 0.0001h);
    half4 scores = saturate(heights) + rawWeights;
    half highest = max(max(scores.x, scores.y), max(scores.z, scores.w));
    half4 contributions = max(scores - highest + transition, 0.0h) * rawWeights;
    half total = contributions.x + contributions.y + contributions.z + contributions.w;
    if (total <= 0.0001h)
    {
        half rawTotal = rawWeights.x + rawWeights.y + rawWeights.z + rawWeights.w;
        return rawTotal > 0.0001h
            ? rawWeights / rawTotal
            : half4(1.0h, 0.0h, 0.0h, 0.0h);
    }

    return contributions / total;
}

half4 SimpleBlendMaterialWeights(half4 rawWeights, half4 heights, half transition, half layerCount, half contrast)
{
    rawWeights = max(rawWeights, 0.0h);
    if (layerCount < 3.5h) rawWeights.w = 0.0h;
    half total = dot(rawWeights, half4(1, 1, 1, 1));
    half4 smoothWeights = total > 0.0001h ? rawWeights / total : half4(1, 0, 0, 0);
    half4 heightWeights = layerCount > 3.5h
        ? SimpleBlendHeightWeights4(rawWeights, heights, transition)
        : half4(SimpleBlendHeightWeights3(rawWeights.xyz, heights.xyz, transition), 0);
    return lerp(smoothWeights, heightWeights, saturate(contrast));
}

void SimpleBlendHeightWeights3_half(
    half3 RawWeights,
    half3 Heights,
    half Transition,
    out half3 Weights)
{
    Weights = SimpleBlendHeightWeights3(RawWeights, Heights, Transition);
}

void SimpleBlendHeightWeights3_float(
    float3 RawWeights,
    float3 Heights,
    float Transition,
    out float3 Weights)
{
    Weights = SimpleBlendHeightWeights3(
        (half3)RawWeights,
        (half3)Heights,
        (half)Transition);
}

void SimpleBlendHeightWeights4_half(
    half4 RawWeights,
    half4 Heights,
    half Transition,
    out half4 Weights)
{
    Weights = SimpleBlendHeightWeights4(RawWeights, Heights, Transition);
}

void SimpleBlendHeightWeights4_float(
    float4 RawWeights,
    float4 Heights,
    float Transition,
    out float4 Weights)
{
    Weights = SimpleBlendHeightWeights4(
        (half4)RawWeights,
        (half4)Heights,
        (half)Transition);
}

#endif
