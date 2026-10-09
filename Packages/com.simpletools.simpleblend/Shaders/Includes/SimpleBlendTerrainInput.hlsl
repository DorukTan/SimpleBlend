#ifndef SIMPLEBLEND_TERRAIN_INPUT_INCLUDED
#define SIMPLEBLEND_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendHeightBlend.hlsl"
#include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendTerrainMath.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _TerrainOrigin;
float4 _TerrainSize;
float4 _TerrainControl_TexelSize;
half _TerrainLayerCount;
half _SimpleBlendTerrainBindingVersion;
half _BlendTransition;
half _TerrainHeightScale;
half _LayerCount;
float4 _TerrainLayer0TileSizeOffset;
float4 _TerrainLayer0Surface;
float4 _TerrainLayer0Flags;
float4 _TerrainLayer0DiffuseRemapMin;
float4 _TerrainLayer0DiffuseRemapMax;
float4 _TerrainLayer0MaskRemapMin;
float4 _TerrainLayer0MaskRemapMax;
float4 _TerrainLayer1TileSizeOffset;
float4 _TerrainLayer1Surface;
float4 _TerrainLayer1Flags;
float4 _TerrainLayer1DiffuseRemapMin;
float4 _TerrainLayer1DiffuseRemapMax;
float4 _TerrainLayer1MaskRemapMin;
float4 _TerrainLayer1MaskRemapMax;
float4 _TerrainLayer2TileSizeOffset;
float4 _TerrainLayer2Surface;
float4 _TerrainLayer2Flags;
float4 _TerrainLayer2DiffuseRemapMin;
float4 _TerrainLayer2DiffuseRemapMax;
float4 _TerrainLayer2MaskRemapMin;
float4 _TerrainLayer2MaskRemapMax;
float4 _TerrainLayer3TileSizeOffset;
float4 _TerrainLayer3Surface;
float4 _TerrainLayer3Flags;
float4 _TerrainLayer3DiffuseRemapMin;
float4 _TerrainLayer3DiffuseRemapMax;
float4 _TerrainLayer3MaskRemapMin;
float4 _TerrainLayer3MaskRemapMax;
float4 _Layer1Albedo_ST;
half4 _Layer1Color;
half _Layer1NormalScale;
half _Layer1Metallic;
half _Layer1Smoothness;
half _Layer1OcclusionStrength;
float4 _Layer2Albedo_ST;
half4 _Layer2Color;
half _Layer2NormalScale;
half _Layer2Metallic;
half _Layer2Smoothness;
half _Layer2OcclusionStrength;
float4 _Layer3Albedo_ST;
half4 _Layer3Color;
half _Layer3NormalScale;
half _Layer3Metallic;
half _Layer3Smoothness;
half _Layer3OcclusionStrength;
CBUFFER_END

TEXTURE2D(_TerrainControl);
TEXTURE2D(_TerrainLayer0Diffuse);
TEXTURE2D(_TerrainLayer0Normal);
TEXTURE2D(_TerrainLayer0Mask);
TEXTURE2D(_TerrainLayer1Diffuse);
TEXTURE2D(_TerrainLayer1Normal);
TEXTURE2D(_TerrainLayer1Mask);
TEXTURE2D(_TerrainLayer2Diffuse);
TEXTURE2D(_TerrainLayer2Normal);
TEXTURE2D(_TerrainLayer2Mask);
TEXTURE2D(_TerrainLayer3Diffuse);
TEXTURE2D(_TerrainLayer3Normal);
TEXTURE2D(_TerrainLayer3Mask);
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

// Terrain textures share repeat sampling; the control texture must not repeat.
#define SIMPLEBLEND_SAMPLE_TERRAIN(INDEX, RESULT) \
    float2 terrainUv##INDEX = SimpleBlendTerrainLayerUv(positionWS, _TerrainOrigin, _TerrainLayer##INDEX##TileSizeOffset); \
    half4 diffuse##INDEX = SAMPLE_TEXTURE2D(_TerrainLayer##INDEX##Diffuse, sampler_LinearRepeat, terrainUv##INDEX); \
    half4 mask##INDEX = SAMPLE_TEXTURE2D(_TerrainLayer##INDEX##Mask, sampler_LinearRepeat, terrainUv##INDEX); \
    half3 normal##INDEX = _TerrainLayer##INDEX##Flags.y > 0.5 \
        ? UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainLayer##INDEX##Normal, sampler_LinearRepeat, terrainUv##INDEX), \
            _TerrainLayer##INDEX##Surface.x) : half3(0, 0, 1); \
    SimpleBlendTerrainLayer RESULT = SimpleBlendDecodeTerrainLayer(diffuse##INDEX, mask##INDEX, normal##INDEX, \
        _TerrainLayer##INDEX##Surface, _TerrainLayer##INDEX##Flags, _TerrainLayer##INDEX##DiffuseRemapMin, \
        _TerrainLayer##INDEX##DiffuseRemapMax, _TerrainLayer##INDEX##MaskRemapMin, _TerrainLayer##INDEX##MaskRemapMax);

#define SIMPLEBLEND_SAMPLE_OVERLAY(INDEX, RESULT, HEIGHT) \
    float2 overlayUv##INDEX = uv * _Layer##INDEX##Albedo_ST.xy + _Layer##INDEX##Albedo_ST.zw; \
    half4 overlayMask##INDEX = SAMPLE_TEXTURE2D(_Layer##INDEX##Mask, sampler_LinearRepeat, overlayUv##INDEX); \
    SimpleBlendTerrainLayer RESULT; \
    RESULT.albedo = SAMPLE_TEXTURE2D(_Layer##INDEX##Albedo, sampler_LinearRepeat, overlayUv##INDEX).rgb * _Layer##INDEX##Color.rgb; \
    RESULT.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_Layer##INDEX##Normal, sampler_LinearRepeat, overlayUv##INDEX), _Layer##INDEX##NormalScale); \
    RESULT.metallic = overlayMask##INDEX.r * _Layer##INDEX##Metallic; \
    RESULT.occlusion = lerp(1.0h, overlayMask##INDEX.g, saturate(_Layer##INDEX##OcclusionStrength)); \
    RESULT.smoothness = overlayMask##INDEX.a * _Layer##INDEX##Smoothness; \
    RESULT.height = 0; \
    half HEIGHT = SAMPLE_TEXTURE2D(_Layer##INDEX##Height, sampler_LinearRepeat, overlayUv##INDEX).r;

SimpleBlendTerrainLayer SimpleBlendSampleTerrain(float3 positionWS)
{
    SIMPLEBLEND_SAMPLE_TERRAIN(0, layer0)
    SIMPLEBLEND_SAMPLE_TERRAIN(1, layer1)
    SIMPLEBLEND_SAMPLE_TERRAIN(2, layer2)
    SIMPLEBLEND_SAMPLE_TERRAIN(3, layer3)
    float2 controlUv = SimpleBlendTerrainControlUv(positionWS, _TerrainOrigin, _TerrainSize);
    float2 sampleUv = SimpleBlendTerrainControlSampleUv(controlUv, _TerrainControl_TexelSize);
    half4 control = SAMPLE_TEXTURE2D(_TerrainControl, sampler_LinearClamp, sampleUv);
    half4 weights = SimpleBlendTerrainControlWeights(control, _TerrainLayerCount);
    return SimpleBlendMixTerrainLayers(layer0, layer1, layer2, layer3, weights);
}

void InitializeSimpleBlendSurfaceData(float2 uv, half4 vertexColor, float3 positionWS,
    half3 normalWS, half4 tangentWS, out SurfaceData surfaceData)
{
    SimpleBlendTerrainLayer terrain = SimpleBlendSampleTerrain(positionWS);
    terrain.normalTS = SimpleBlendProjectedNormalToMesh(terrain.normalTS, normalWS, tangentWS);
    SIMPLEBLEND_SAMPLE_OVERLAY(1, red, redHeight)
    SIMPLEBLEND_SAMPLE_OVERLAY(2, green, greenHeight)
    SIMPLEBLEND_SAMPLE_OVERLAY(3, blue, blueHeight)
    half4 rawWeights = half4(saturate(1.0h - vertexColor.r - vertexColor.g - vertexColor.b),
        saturate(vertexColor.rgb));
    half4 weights = SimpleBlendHeightWeights4(rawWeights,
        half4(terrain.height * _TerrainHeightScale, redHeight, greenHeight, blueHeight), _BlendTransition);
    SimpleBlendTerrainLayer combined = SimpleBlendMixTerrainLayers(terrain, red, green, blue, weights);
    surfaceData = (SurfaceData)0;
    surfaceData.albedo = combined.albedo;
    surfaceData.alpha = 1.0h;
    surfaceData.normalTS = combined.normalTS;
    surfaceData.metallic = saturate(combined.metallic);
    surfaceData.smoothness = saturate(combined.smoothness);
    surfaceData.occlusion = saturate(combined.occlusion);
}

#endif
