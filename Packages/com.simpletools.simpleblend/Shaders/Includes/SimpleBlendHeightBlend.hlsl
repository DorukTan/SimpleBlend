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

#endif
