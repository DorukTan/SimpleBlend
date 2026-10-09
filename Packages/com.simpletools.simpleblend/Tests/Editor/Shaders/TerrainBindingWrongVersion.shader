Shader "Hidden/SimpleBlend/Terrain Binding Wrong Version"
{
    Properties
    {
        [HideInInspector] _SimpleBlendTerrainBindingVersion("Binding Version", Float) = 2
        _TerrainControl("Terrain Control", 2D) = "black" {}
        _TerrainOrigin("Terrain Origin", Vector) = (0,0,0,0)
        _TerrainSize("Terrain Size", Vector) = (1,1,1,0)
        _TerrainLayerCount("Layer Count", Float) = 0
        _OverlaySentinel("Overlay Sentinel", Float) = 0.37
        _TerrainLayer0Diffuse("Layer 0 Diffuse", 2D) = "white" {}
        _TerrainLayer0Normal("Layer 0 Normal", 2D) = "bump" {}
        _TerrainLayer0Mask("Layer 0 Mask", 2D) = "white" {}
        _TerrainLayer0TileSizeOffset("Layer 0 TileSizeOffset", Vector) = (0,0,0,0)
        _TerrainLayer0Surface("Layer 0 Surface", Vector) = (0,0,0,0)
        _TerrainLayer0Flags("Layer 0 Flags", Vector) = (0,0,0,0)
        _TerrainLayer0DiffuseRemapMin("Layer 0 DiffuseRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer0DiffuseRemapMax("Layer 0 DiffuseRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer0MaskRemapMin("Layer 0 MaskRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer0MaskRemapMax("Layer 0 MaskRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer1Diffuse("Layer 1 Diffuse", 2D) = "white" {}
        _TerrainLayer1Normal("Layer 1 Normal", 2D) = "bump" {}
        _TerrainLayer1Mask("Layer 1 Mask", 2D) = "white" {}
        _TerrainLayer1TileSizeOffset("Layer 1 TileSizeOffset", Vector) = (0,0,0,0)
        _TerrainLayer1Surface("Layer 1 Surface", Vector) = (0,0,0,0)
        _TerrainLayer1Flags("Layer 1 Flags", Vector) = (0,0,0,0)
        _TerrainLayer1DiffuseRemapMin("Layer 1 DiffuseRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer1DiffuseRemapMax("Layer 1 DiffuseRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer1MaskRemapMin("Layer 1 MaskRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer1MaskRemapMax("Layer 1 MaskRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer2Diffuse("Layer 2 Diffuse", 2D) = "white" {}
        _TerrainLayer2Normal("Layer 2 Normal", 2D) = "bump" {}
        _TerrainLayer2Mask("Layer 2 Mask", 2D) = "white" {}
        _TerrainLayer2TileSizeOffset("Layer 2 TileSizeOffset", Vector) = (0,0,0,0)
        _TerrainLayer2Surface("Layer 2 Surface", Vector) = (0,0,0,0)
        _TerrainLayer2Flags("Layer 2 Flags", Vector) = (0,0,0,0)
        _TerrainLayer2DiffuseRemapMin("Layer 2 DiffuseRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer2DiffuseRemapMax("Layer 2 DiffuseRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer2MaskRemapMin("Layer 2 MaskRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer2MaskRemapMax("Layer 2 MaskRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer3Diffuse("Layer 3 Diffuse", 2D) = "white" {}
        _TerrainLayer3Normal("Layer 3 Normal", 2D) = "bump" {}
        _TerrainLayer3Mask("Layer 3 Mask", 2D) = "white" {}
        _TerrainLayer3TileSizeOffset("Layer 3 TileSizeOffset", Vector) = (0,0,0,0)
        _TerrainLayer3Surface("Layer 3 Surface", Vector) = (0,0,0,0)
        _TerrainLayer3Flags("Layer 3 Flags", Vector) = (0,0,0,0)
        _TerrainLayer3DiffuseRemapMin("Layer 3 DiffuseRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer3DiffuseRemapMax("Layer 3 DiffuseRemapMax", Vector) = (0,0,0,0)
        _TerrainLayer3MaskRemapMin("Layer 3 MaskRemapMin", Vector) = (0,0,0,0)
        _TerrainLayer3MaskRemapMax("Layer 3 MaskRemapMax", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 frag(v2f_img input) : SV_Target { return fixed4(1,1,1,1); }
            ENDCG
        }
    }
}
