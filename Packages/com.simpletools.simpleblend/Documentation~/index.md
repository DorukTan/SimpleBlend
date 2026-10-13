# SimpleBlend documentation

SimpleBlend includes a deterministic multi-channel brush engine, production storage and prefab handling, and hand-written URP height-blend shaders. It supports paint, erase, smooth, fill, sample, flow, accumulation, composable masks, independent channel previews, and topology-relative density visualization without changing imported source meshes. The managed 40k-vertex profiling gate passes without Burst.

- [Data safety and storage](data-safety.md)
- [Paint the first stroke](first-stroke.md)
- [Brush controls and exact behavior](brush-controls.md)
- [Layer Blend shader setup](layer-blend-shaders.md)
- [Terrain material binding](terrain-binding.md)
- [Teaching samples](teaching-samples.md)

For the active development plan, see the repository-level `docs/roadmap.md` and `docs/implementation-plan.md` files.
