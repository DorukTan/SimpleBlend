Shader "SimpleBlend/Layer Blend 4"
{
    Properties
    {
        [Header(Blend Contract)]
        _BlendTransition("Height Transition", Range(0.001, 1.0)) = 0.2
        [HideInInspector] _LayerCount("Layer Count", Float) = 4

        [Header(Base Layer Unpainted)]
        [MainTexture] _Layer0Albedo("Base Albedo", 2D) = "white" {}
        [MainColor] _Layer0Color("Base Tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer0Normal("Base Normal", 2D) = "bump" {}
        _Layer0NormalScale("Base Normal Scale", Range(0, 2)) = 1
        _Layer0Mask("Base Mask (R Metallic, G AO, A Smoothness)", 2D) = "white" {}
        _Layer0Metallic("Base Metallic", Range(0, 1)) = 0
        _Layer0Smoothness("Base Smoothness", Range(0, 1)) = 0.5
        _Layer0OcclusionStrength("Base AO Strength", Range(0, 1)) = 1
        _Layer0Height("Base Height", 2D) = "black" {}

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
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        UsePass "Hidden/SimpleBlend/Layer Blend Passes/ForwardLit"
        UsePass "Hidden/SimpleBlend/Layer Blend Passes/GBuffer"
        UsePass "Hidden/SimpleBlend/Layer Blend Passes/ShadowCaster"
        UsePass "Hidden/SimpleBlend/Layer Blend Passes/DepthOnly"
        UsePass "Hidden/SimpleBlend/Layer Blend Passes/DepthNormals"
        UsePass "Hidden/SimpleBlend/Layer Blend Passes/Meta"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "SimpleBlend.Editor.SimpleBlendLayerBlendShaderGUI"
}
