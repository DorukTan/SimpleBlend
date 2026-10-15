Shader "Hidden/SimpleBlend/Terrain Surface Probe"
{
    Properties
    {
        [Header(Blend Contract)]
        _BlendTransition("Height Transition", Range(0.001, 1.0)) = 0.2
        _TerrainHeightScale("Terrain Base Height Influence", Range(0, 1)) = 0
        [HideInInspector] _LayerCount("Layer Count", Float) = 4

        [HideInInspector] _SimpleBlendTerrainBindingVersion("Terrain Binding Version", Float) = 1
        [HideInInspector] _TerrainControl("Terrain Control", 2D) = "red" {}
        [HideInInspector] _TerrainOrigin("Terrain Origin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainSize("Terrain Size", Vector) = (1,1,1,0)
        [HideInInspector] _TerrainLayerCount("Terrain Layer Count", Float) = 0
        [HideInInspector] _TerrainLayer0Diffuse("Terrain 0 Diffuse", 2D) = "white" {}
        [HideInInspector] _TerrainLayer0Normal("Terrain 0 Normal", 2D) = "bump" {}
        [HideInInspector] _TerrainLayer0Mask("Terrain 0 Mask", 2D) = "white" {}
        [HideInInspector] _TerrainLayer0TileSizeOffset("Terrain 0 TileSizeOffset", Vector) = (1,1,0,0)
        [HideInInspector] _TerrainLayer0Surface("Terrain 0 Surface", Vector) = (1,0,0.5,0)
        [HideInInspector] _TerrainLayer0Flags("Terrain 0 Flags", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer0DiffuseRemapMin("Terrain 0 DiffuseRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer0DiffuseRemapMax("Terrain 0 DiffuseRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer0MaskRemapMin("Terrain 0 MaskRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer0MaskRemapMax("Terrain 0 MaskRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer1Diffuse("Terrain 1 Diffuse", 2D) = "white" {}
        [HideInInspector] _TerrainLayer1Normal("Terrain 1 Normal", 2D) = "bump" {}
        [HideInInspector] _TerrainLayer1Mask("Terrain 1 Mask", 2D) = "white" {}
        [HideInInspector] _TerrainLayer1TileSizeOffset("Terrain 1 TileSizeOffset", Vector) = (1,1,0,0)
        [HideInInspector] _TerrainLayer1Surface("Terrain 1 Surface", Vector) = (1,0,0.5,0)
        [HideInInspector] _TerrainLayer1Flags("Terrain 1 Flags", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer1DiffuseRemapMin("Terrain 1 DiffuseRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer1DiffuseRemapMax("Terrain 1 DiffuseRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer1MaskRemapMin("Terrain 1 MaskRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer1MaskRemapMax("Terrain 1 MaskRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer2Diffuse("Terrain 2 Diffuse", 2D) = "white" {}
        [HideInInspector] _TerrainLayer2Normal("Terrain 2 Normal", 2D) = "bump" {}
        [HideInInspector] _TerrainLayer2Mask("Terrain 2 Mask", 2D) = "white" {}
        [HideInInspector] _TerrainLayer2TileSizeOffset("Terrain 2 TileSizeOffset", Vector) = (1,1,0,0)
        [HideInInspector] _TerrainLayer2Surface("Terrain 2 Surface", Vector) = (1,0,0.5,0)
        [HideInInspector] _TerrainLayer2Flags("Terrain 2 Flags", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer2DiffuseRemapMin("Terrain 2 DiffuseRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer2DiffuseRemapMax("Terrain 2 DiffuseRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer2MaskRemapMin("Terrain 2 MaskRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer2MaskRemapMax("Terrain 2 MaskRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer3Diffuse("Terrain 3 Diffuse", 2D) = "white" {}
        [HideInInspector] _TerrainLayer3Normal("Terrain 3 Normal", 2D) = "bump" {}
        [HideInInspector] _TerrainLayer3Mask("Terrain 3 Mask", 2D) = "white" {}
        [HideInInspector] _TerrainLayer3TileSizeOffset("Terrain 3 TileSizeOffset", Vector) = (1,1,0,0)
        [HideInInspector] _TerrainLayer3Surface("Terrain 3 Surface", Vector) = (1,0,0.5,0)
        [HideInInspector] _TerrainLayer3Flags("Terrain 3 Flags", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer3DiffuseRemapMin("Terrain 3 DiffuseRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer3DiffuseRemapMax("Terrain 3 DiffuseRemapMax", Vector) = (1,1,1,1)
        [HideInInspector] _TerrainLayer3MaskRemapMin("Terrain 3 MaskRemapMin", Vector) = (0,0,0,0)
        [HideInInspector] _TerrainLayer3MaskRemapMax("Terrain 3 MaskRemapMax", Vector) = (1,1,1,1)

        [Header(Layer 1 Red Channel)]
        _Layer1Albedo("Red Albedo", 2D) = "white" {}
        _Layer1Color("Red Tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer1Normal("Red Normal", 2D) = "bump" {}
        _Layer1NormalScale("Red Normal Scale", Range(0, 2)) = 1
        _Layer1Mask("Red Mask (R Metallic, G AO, A Smoothness)", 2D) = "white" {}
        _Layer1Metallic("Red Metallic", Range(0, 1)) = 0
        _Layer1Smoothness("Red Smoothness", Range(0, 1)) = 0.5
        _Layer1OcclusionStrength("Red AO Strength", Range(0, 1)) = 1
        _Layer1Height("Red Height", 2D) = "black" {}

        [Header(Layer 2 Green Channel)]
        _Layer2Albedo("Green Albedo", 2D) = "white" {}
        _Layer2Color("Green Tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer2Normal("Green Normal", 2D) = "bump" {}
        _Layer2NormalScale("Green Normal Scale", Range(0, 2)) = 1
        _Layer2Mask("Green Mask (R Metallic, G AO, A Smoothness)", 2D) = "white" {}
        _Layer2Metallic("Green Metallic", Range(0, 1)) = 0
        _Layer2Smoothness("Green Smoothness", Range(0, 1)) = 0.5
        _Layer2OcclusionStrength("Green AO Strength", Range(0, 1)) = 1
        _Layer2Height("Green Height", 2D) = "black" {}

        [Header(Layer 3 Blue Channel)]
        _Layer3Albedo("Blue Albedo", 2D) = "white" {}
        _Layer3Color("Blue Tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer3Normal("Blue Normal", 2D) = "bump" {}
        _Layer3NormalScale("Blue Normal Scale", Range(0, 2)) = 1
        _Layer3Mask("Blue Mask (R Metallic, G AO, A Smoothness)", 2D) = "white" {}
        _Layer3Metallic("Blue Metallic", Range(0, 1)) = 0
        _Layer3Smoothness("Blue Smoothness", Range(0, 1)) = 0.5
        _Layer3OcclusionStrength("Blue AO Strength", Range(0, 1)) = 1
        _Layer3Height("Blue Height", 2D) = "black" {}

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1
    }

    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ProbeVertex
            #pragma fragment ProbeFragment
            #include "Packages/com.simpletools.simpleblend/Shaders/Includes/SimpleBlendTerrainInput.hlsl"
            float4 _ProbePosition;
            float4 _ProbeColor;
            float4 _ProbeNormal;
            float4 _ProbeTangent;
            float4 _ProbeUv;
            float _ProbeMode;
            struct ProbeVaryings { float4 positionCS : SV_POSITION; };
            ProbeVaryings ProbeVertex(uint id : SV_VertexID)
            {
                ProbeVaryings output;
                output.positionCS = float4(id == 2 ? 3 : -1, id == 1 ? 3 : -1, 0, 1);
                return output;
            }
            float4 ProbeFragment(ProbeVaryings input) : SV_Target
            {
                if (_ProbeMode < 0.5)
                    return float4(SimpleBlendTerrainControlUv(_ProbePosition.xyz, _TerrainOrigin, _TerrainSize),
                        SimpleBlendTerrainLayerUv(_ProbePosition.xyz, _TerrainOrigin, _TerrainLayer0TileSizeOffset));
                if (_ProbeMode < 1.5)
                    return float4(SimpleBlendSampleTerrain(_ProbePosition.xyz).albedo, 1);
                if (_ProbeMode < 2.5)
                {
                    SimpleBlendTerrainLayer layer = SimpleBlendSampleTerrain(_ProbePosition.xyz);
                    return float4(layer.metallic, layer.occlusion, layer.smoothness, layer.height);
                }
                if (_ProbeMode < 3.5)
                    return float4(SimpleBlendProjectedNormalToMesh(_ProbeNormal.xyz, half3(0,1,0), _ProbeTangent), 1);
                SurfaceData surface;
                InitializeSimpleBlendSurfaceData(_ProbeUv.xy, _ProbeColor, _ProbePosition.xyz,
                    half3(0,1,0), _ProbeTangent, surface);
                if (_ProbeMode < 4.5) return float4(surface.albedo, surface.alpha);
                if (_ProbeMode < 5.5) return float4(surface.metallic, surface.occlusion, surface.smoothness, 1);
                return float4(surface.normalTS, 1);
            }
            ENDHLSL
        }
    }
}
