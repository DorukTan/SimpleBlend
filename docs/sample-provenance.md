# Teaching sample provenance

Scope: the actual RockMoss, RoofSnow and WallGrime sample folders introduced for issue #5. This supplements the provisional issue #8 audit; it does not clear a final release candidate or external Unity dependencies.

Each sample has these distributed files, plus Unity metadata:

| File | Origin | Terms |
|---|---|---|
| `Surface.asset` | Project-authored deterministic dense grid; rounded/capped rock, pitched roof or vertical wall from TeachingSampleAuthoring.CreateMesh | Included SimpleTools MIT notice |
| `Albedo0.asset`, `Albedo1.asset`, `Albedo2.asset` | Project palette multiplied by deterministic sine/cosine texture formula | Included SimpleTools MIT notice |
| `Height0.asset`, `Height1.asset`, `Height2.asset` | Same procedural pattern, amplitude 0.05 | Included SimpleTools MIT notice |
| `Blend.mat` | Public SimpleBlend/Layer Blend 3 properties and the sample's own textures | Included SimpleTools MIT notice; URP libraries are external |
| Named `.unity` scene | Owned mesh/material, serialized per-renderer Color32 paint, camera, light and instructional TextMesh caption | Included SimpleTools MIT notice |
| `README.md`, `LICENSE` | Project-authored instructions and unchanged package MIT notice | Included SimpleTools MIT notice |

There are no copied Asset Store models, downloaded images, audio, font files or sample scripts in these folders. Captions reference Unity's built-in LegacyRuntime font; its engine resource is supplied by Unity and is not relabeled or bundled as a sample font file.

Source mesh colors are empty. Starting paint arrays are procedural values saved in VertexPaintData, not brush-stroke recordings. Import requires shipped runtime/editor code; the authoring method lives only in development tests and is not referenced by imported scenes.

Authoring was performed in a new GUID-owned Assets folder through TeachingSampleAuthoring.Generate. Only the three authored case folders and their metadata were moved into Samples~ after the Unity batch process exited. Folder/file GUIDs were preserved. The generator refuses an existing authoring root and does not overwrite published sample files.

For intentional reauthoring, use a fresh dedicated batch checkout and pass `-executeMethod SimpleBlend.Editor.Tests.TeachingSampleAuthoring.Generate -simpleBlendSampleRoot Assets/__SimpleBlendSamples_<new-guid>`. Review generated contents before replacing anything. Geometry, paint, material settings and texture formulas are defined in that method; documentation and licenses are maintained separately.

Final issue #8 review must inventory these actual folders in the reviewed candidate, retain their notices and verify owner rights confirmation and dependency boundaries. Owner-run appearance and first-use review remain separate gates.
