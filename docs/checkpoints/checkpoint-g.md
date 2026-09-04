# Checkpoint G — hand-written URP height-blend shaders

- Date: 2026-09-04
- Package: `com.simpletools.simpleblend` `0.4.0-beta.1`
- Editor: Unity `6000.4.8f1`, URP `17.4.0`

## Automated outcome

The M7 implementation gate passed. SimpleBlend now supplies public three-layer and four-layer URP shaders backed by one hidden shared pass implementation. The locked mapping is base plus Red/Green overlays for Layer Blend 3 and base plus Red/Green/Blue overlays for Layer Blend 4. Clear vertex colors select the base; alpha remains free.

All material channels use one normalized height-aware weight vector. Each layer exposes Albedo, tangent-space Normal, Mask, and Height textures plus scalar controls. The mask convention is R metallic, G ambient occlusion, B reserved, A smoothness.

## Shader and build evidence

- Nine focused EditMode tests passed.
- The tests cover public shader names/properties, every required `UsePass`, the locked mapping, clear/extreme/tie/transition math, custom Inspector construction, one `UnityPerMaterial` buffer, instancing declarations, and DOTS declarations.
- Unity forced compilation of ForwardLit, GBuffer, ShadowCaster, DepthOnly, DepthNormals, and Meta without shader errors.
- A repeatable build fixture created temporary Resources materials for both public shaders, enabled GPU instancing, built the project for Windows x64, then removed the temporary assets.
- The D3D11 player build succeeded at `Builds/SimpleBlendM7/SimpleBlend.exe`; the build log confirms pass variant processing for the retained layer-blend shaders.

The final versioned package suite passed 82/82 EditMode tests in 5.242 seconds. A second retained-shader Windows x64 build from that versioned state also completed successfully.

## Architecture boundary

Layer Blend 3 and Layer Blend 4 contain public properties and delegate every rendering pass to one hidden shader. One input include owns the stable material buffer and layer sampling; one height-weight include owns the pure weighting rules. This keeps public variants and the later Shader Graph parity work from developing separate channel conventions.

## Explicitly unobserved

At the user's request, no desktop/GUI control was used. The following remain manual acceptance evidence rather than automated claims:

- material appearance under live URP lighting;
- Frame Debugger and Material Inspector confirmation of SRP Batcher behavior;
- baked Meta/lightmapping output;
- editor, play-mode, and built-player reference-image parity.

The player-build result proves inclusion and backend compilation, not screenshot parity. Acceptance test 7 therefore remains open until an interactive comparison is performed.

## Next boundary

M8 may reuse the locked weight and channel test vectors for Shader Graph parity. Terrain matching begins with the documented public-API contract spike; neither path may redefine the M7 mapping.
