# Shader Graph integration

This integrated development checkout includes the graph assets alongside terrain binding and teaching samples. Published revisions may contain a different feature set; check the files included with the installed revision.

Three subgraphs under `ShaderGraph/Subgraphs` share the project's height/surface functions:

- **SimpleBlend Height Weights 3**: three raw weights, sampled heights and transition.
- **SimpleBlend Height Weights 4**: four raw weights, sampled heights and transition.
- **SimpleBlend Surface Blend 4**: aligned weighted albedo, tangent-space normal and packed surface outputs.

For three layers, raw weights are `(saturate(1-r-g), saturate(r), saturate(g))`. Append a zero fourth weight for the four-input surface subgraph; a neutral fourth normal is `(0,0,1)`. For four layers, use `(saturate(1-r-g-b), saturate(r), saturate(g), saturate(b))`. Sample heights before computing the final weights. Alpha remains free.

Apply the metallic multiplier, AO strength and smoothness multiplier to each layer's packed mask before surface blending. R is metallic, G is AO, A is smoothness; mask B is not consumed by the standard overlay shaders. Normals are tangent-space vectors, not world-space normals.

The complete `LayerBlend3Parity.shadergraph` and `LayerBlend4Parity.shadergraph` examples show URP Lit wiring. Per-layer Albedo tiling/offset applies to its entire texture set. Use normal-map decoding and linear data textures consistently with [the hand-written shader guide](layer-blend-shaders.md).

Paired materials `ShaderGraph/Examples/Materials/Graph3.mat` / `Hlsl3.mat` and `Graph4.mat` / `Hlsl4.mat` use the same procedural textures and controls. For a strict appearance comparison, keep one renderer and camera fixed and swap only its assigned material; side-by-side placements can change lighting. The owner-run visual gate remains open.

Do not run development authoring methods in a consumer project just to repair missing assets. Graph assets and their dependencies should arrive with the reviewed package revision. Imported graphs, GPU math/pass checks and player compilation are distinct from live graph/HLSL image parity.
