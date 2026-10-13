# Layer Blend shader setup

SimpleBlend `0.4.0-beta.1` includes two hand-written Universal Render Pipeline shaders:

- **SimpleBlend/Layer Blend 3**: base layer, Red overlay, Green overlay.
- **SimpleBlend/Layer Blend 4**: base layer, Red overlay, Green overlay, Blue overlay.

An unpainted vertex color (`0, 0, 0, 0`) selects the base layer. Vertex alpha is not consumed by either shader and remains available for project-specific masks or effects.

## Create a material

1. Create a Material and choose **SimpleBlend/Layer Blend 3** or **SimpleBlend/Layer Blend 4**.
2. Supply Albedo, Normal, Mask, and Height textures for each visible layer.
3. Use each Albedo texture's tiling and offset to control that layer's complete texture set. Normal, Mask, and Height use the corresponding Albedo UV transform.
4. Adjust **Height Transition**. Smaller values produce a sharper winning-height boundary; larger values retain a wider mixture.
5. Assign the material to a renderer carrying SimpleBlend vertex colors.

The custom material Inspector labels every layer with its controlling vertex channel. Layer Blend 3 intentionally ignores Blue rather than silently assigning it a different meaning.

## Texture conventions

- **Albedo** is multiplied by the layer color tint.
- **Normal** is a tangent-space normal map with an independent normal scale.
- **Mask R** is metallic and is multiplied by the layer metallic control.
- **Mask G** is ambient occlusion. The layer occlusion control adjusts its strength.
- **Mask B** is reserved and currently unused.
- **Mask A** is smoothness and is multiplied by the layer smoothness control.
- **Height** is sampled from the red channel and clamped to the `0–1` range.

Use linear import settings for data textures such as Mask and Height. Normal textures must use Unity's Normal Map import type.

## Height weighting

The shader computes all participating layer scores together from painted weight plus sampled height. It keeps layers within the transition range of the highest score, multiplies those contributions by their painted weights, then normalizes the final vector. A zero-painted layer cannot win only because its height texture is bright.

The same final normalized weights blend albedo, normals, metallic, ambient occlusion, and smoothness. This avoids the ordering bias of sequential `lerp` chains and keeps every surface channel aligned.

## URP behavior

Both public shaders share one implementation for Forward Lit, deferred GBuffer, ShadowCaster, DepthOnly, DepthNormals, and Meta passes. Material properties live in one stable `UnityPerMaterial` constant buffer. GPU instancing variants and URP DOTS declarations are present in every shared pass.

The automated gate compiles all shared passes and forces both public shaders into a Windows D3D11 player build. Live rendering appearance, Frame Debugger SRP Batcher status, baked-lightmap output, and editor/player reference-image parity still require an interactive Unity review.
