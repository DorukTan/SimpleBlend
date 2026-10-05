# SimpleBlend

SimpleBlend paints vertex colors on meshes in the Unity Scene view and uses those colors to blend URP texture layers. Paint is stored per renderer, with imported source meshes preserved.

## Development status

The committed package is `0.4.0-beta.1`. Brush modes, storage, previews, managed performance checks, and hand-written three/four-layer height-blend shaders are implemented. Shader Graph and terrain integration are being completed in M8. Samples, compatibility checks, visual acceptance, and removal testing remain release work.

The observed development baseline is Unity `6000.4.8f1` with URP `17.4.0`. Unity 6.3 LTS (`6000.3`) and 6.6 (`6000.6`) are validation targets; they are not yet claimed as tested. The package supports URP workflows.

## Install the development package

In Package Manager, choose **Install package from Git URL** and enter:

```text
https://github.com/DorukTan/SimpleBlend.git?path=/Packages/com.simpletools.simpleblend#main
```

This installs the package subfolder, not the development project. `main` is a development branch; use a published release tag when stable releases become available.

## First stroke

Select a mesh or skinned renderer and choose **GameObject > SimpleBlend > Enable Vertex Painting**. Use the **SimpleBlend Brush** Scene view overlay to select a mode, channel, and preview, then paint the selected mesh. A collider is not required.

The three-layer shader uses the base layer plus Red and Green overlays. The four-layer shader adds Blue. Clear vertex colors select the base layer; alpha is available for custom masks.

See the [package documentation](Packages/com.simpletools.simpleblend/Documentation~/index.md) and [package README](Packages/com.simpletools.simpleblend/README.md) for setup, brush behavior, storage, and materials.

## Develop and contribute

This repository is a Unity development project. Package source is in `Packages/com.simpletools.simpleblend`; the root project's scenes and settings support development and verification.

Open the project with the recorded development Editor version. Follow [CONTRIBUTING.md](CONTRIBUTING.md) for the issue and PR workflow and relevant checks.

- [GitHub roadmap](docs/github-roadmap.md)
- [Milestones](https://github.com/DorukTan/SimpleBlend/milestones?state=all)
- [Open issues](https://github.com/DorukTan/SimpleBlend/issues)
- [Implementation history](docs/roadmap.md)

## License

SimpleBlend is released under the [MIT license](LICENSE). Distributed sample assets and dependencies are reviewed separately for their redistribution terms before release.
