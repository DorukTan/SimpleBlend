# SimpleBlend

SimpleBlend is an open-source Unity editor toolkit for painting texture masks, editing per-renderer vertex colors and blending URP material layers.

## Start here

- [Install and choose a revision](Documentation~/installation.md)
- [Check compatibility and observed results](Documentation~/compatibility.md)
- [Paint the first stroke](Documentation~/first-stroke.md)
- [Paint detailed texture masks](Documentation~/texture-painting.md)
- [Import the teaching samples](Documentation~/teaching-samples.md)
- [Full documentation](Documentation~/index.md)

## Current status

The development manifest version is `0.4.0-beta.1`. The integrated development branch includes terrain binding, terrain shading, Shader Graph references and teaching samples together. Other branches and published revisions can contain different features despite sharing that version. The [installation guide](Documentation~/installation.md) identifies an exact earlier preview commit and what it includes.

Layer Blend 3 uses an unpainted base plus Red/Green overlays. Layer Blend 4 adds Blue. Alpha is free. Terrain Blend uses a bound terrain base plus RGB overlays and supports one to four terrain layers with linear terrain blending.

Only Windows / Unity `6000.4.8f1` / URP `17.4.0` / D3D11 is described as the observed automated baseline. Unity 6.3 LTS and 6.6 await validation. Visual parity, owner walkthroughs and release acceptance remain open; this is not a 1.0 support claim.

## Workflow and safety

Open **Window > SimpleBlend > Workspace** to choose Texture paint or Vertex paint. No collider is required. The vertex-specific **GameObject > SimpleBlend > Enable Vertex Painting** command and **Enable SimpleBlend Vertex Painting** Renderer context command remain available, along with the **SimpleBlend Brush** vertex overlay.

For detailed strokes on a default cube, open **Window > SimpleBlend > Workspace** and use **Texture paint**. Its explicit setup creates a uniquely unwrapped paint copy and saved blend-mask asset, then hides the original renderer with Undo. No subdivision is needed. See [texture painting](Documentation~/texture-painting.md) for ownership, saving and current limits. **Vertex paint** remains available; its strokes are still limited by the mesh's vertex density.

Imported source meshes remain unchanged; serialized per-renderer `Color32[]` data is authoritative. Ordinary renderers use transient streams; skinned/static/GPU-resident cases use owned generated clones. Read [storage and ownership](Documentation~/data-safety.md) before editing prefabs or source topology.

[Removal guidance](Documentation~/removal.md) distinguishes clearing data, retaining generated assets, unrelated materials and shaders that disappear with the package. There is no current one-click standalone bake/export workflow.

## Development checks

Package tests need Unity Test Framework and `com.simpletools.simpleblend` in the development project's `testables` list. GPU checks need a graphics device: use hidden batch mode with D3D11, not `-nographics`, for the complete suite. Use `-runTests -testPlatform EditMode -testFilter SimpleBlend.Editor.Tests` with explicit project/result/log paths. Omit `-quit` for test dispatch on the observed editor.

Automated sample import checks are not owner first-use or visual evidence. Repository checkpoints record their exact scope.

## License

Project content is distributed under the included [MIT license](LICENSE). External Unity dependencies retain their own terms; the release redistribution audit is separate from package usage verification.
