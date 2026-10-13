# Compatibility and verification scope

## Observed baseline

| Environment | Observed result | Not established |
|---|---|---|
| Windows, Unity 6000.4.8f1, URP 17.4.0, Shader Graph package 17.4.0, D3D11 | Painting/storage/shader automated checks; latest sample branch full suite 153/153 passed | A combined reviewed release candidate or complete visual acceptance |
| Same editor, fresh embedded consumer with URP 17.4.0 and Test Framework 1.6.0 | Three public Package Manager sample imports passed | Interactive first-use, material appearance or player sample presentation |
| Same isolated consumer switched to the documented pinned Git URL | Lockfile resolved to 6c9938b; three public sample imports passed | Minimum URP 17.3.0 resolution or owner walkthrough |
| Separate graph PR #14 snapshot on that baseline | Graph imports/math/passes and retained Windows build checked | Graph/HLSL reference-image parity |
| Separate terrain PR #16 snapshot on that baseline | Public-data binder, GPU surface checks, six pass compilation and retained Windows build checked | Terrain seam appearance, batching, baked output or all hardware targets |

These are observations from separate implementation snapshots. Do not add test counts together or treat them as one integrated release run.

## Targets, not tested support

- Unity 6000.3 LTS and the declared URP 17.3.0 minimum await their clean-consumer matrix.
- Unity 6.6 is a planned validation target, not an observed compatibility result or a statement about its current availability.
- Other editor, URP, graphics API, OS and hardware combinations are not claimed tested here.
- Built-in Render Pipeline and HDRP are not supported shader targets.

Consult [Unity's release/support information](https://unity.com/releases/unity-6/support) for editor lifecycle status. Unity's support policy is distinct from SimpleBlend's own verification.

## Practical limits

Painting needs triangle geometry. Normal mapping needs usable mesh normals/tangents and appropriate texture imports. Dense meshes cost more; a managed 40k-vertex profiling fixture passing its own budget is not a performance promise for your scene.

Ordinary MeshRenderer storage differs from skinned, static-batched and GPU Resident Drawer paths. Read [data safety](data-safety.md). Collections/rendering assembly references are supplied through the Unity dependency graph; a successful development import does not validate every older dependency combination. The brush kernel has no direct Burst requirement, but URP dependencies can install Burst transitively.

Terrain matching is one axis-aligned, unit-world-scale terrain with one to four assigned layers and linear control blending. Height/density terrain modes, holes, custom terrain shaders and automatic runtime rebinding are excluded. Geometry/normal agreement is still required at seams.

Live rendering, Frame Debugger, lightmapping, Editor/player reference images, target hardware profiling and owner walkthroughs remain separate open gates. No beta feature list alone establishes 1.0 acceptance.
