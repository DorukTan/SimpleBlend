#ifndef SIMPLEBLEND_LAYER_BLEND_GBUFFER_PASS_INCLUDED
#define SIMPLEBLEND_LAYER_BLEND_GBUFFER_PASS_INCLUDED

#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendLayerBlendPasses.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"

GBufferFragOutput SimpleBlendGBufferFragment(SimpleBlendVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeSimpleBlendSurfaceData(input.uv, SimpleBlendPaintWeights(input.color, input.paintUv), input.positionWS,
        input.normalWS, input.tangentWS, surfaceData);
    InputData inputData;
    InitializeSimpleBlendInputData(input, surfaceData.normalTS, inputData);

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    BRDFData brdfData;
    InitializeBRDFData(
        surfaceData.albedo,
        surfaceData.metallic,
        surfaceData.specular,
        surfaceData.smoothness,
        surfaceData.alpha,
        brdfData);

    Light mainLight = GetMainLight(
        inputData.shadowCoord,
        inputData.positionWS,
        inputData.shadowMask);
    MixRealtimeAndBakedGI(
        mainLight,
        inputData.normalWS,
        inputData.bakedGI,
        inputData.shadowMask);
    half3 globalIllumination = GlobalIllumination(
        brdfData,
        (BRDFData)0,
        0.0h,
        inputData.bakedGI,
        surfaceData.occlusion,
        inputData.positionWS,
        inputData.normalWS,
        inputData.viewDirectionWS,
        inputData.normalizedScreenSpaceUV);
    return PackGBuffersBRDFData(
        brdfData,
        inputData,
        surfaceData.smoothness,
        surfaceData.emission + globalIllumination,
        surfaceData.occlusion);
}

#endif
