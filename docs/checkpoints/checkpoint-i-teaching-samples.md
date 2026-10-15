# Checkpoint I — teaching sample implementation

Issue #5 adds three independent Package Manager samples: Rock and Moss, Roof and Snow, and Wall and Grime.

## Delivered contents

Each folder has a saved scene, a dense original procedural mesh, three albedo textures, three low-amplitude height textures, a public Layer Blend 3 material, editable per-renderer VertexPaintData, camera/light, an instructional TextMesh caption, README and MIT notice. Starting colors are procedural; they are not represented as recorded brush strokes.

The rock has 827 vertices, a deformed rounded surface and end caps. Roof and wall each have 825 vertices. Normals and tangents are generated and source mesh colors remain empty. All three use Red/Green overlays with Blue/Alpha unused.

Asset origins and external Unity font/shader boundaries are documented in `docs/sample-provenance.md` and each sample README. Imported scenes do not reference test/development authoring helpers. The package manifest registers all three samples independently.

The branch builds on terrain PR #16 but uses shipped hand-written Layer Blend contracts; it does not include the separate Graph PR #14 implementation. No implementation PR has been merged as part of this work.

## Verification

Baseline: Unity `6000.4.8f1`, URP `17.4.0`, Shader Graph `17.4.0`, Windows/D3D11.

- Complete development package EditMode suite: **153 passed, 0 failed, 0 skipped**.
- Three new cases use public `Sample.FindByPackage` / `Sample.Import` APIs rather than copying folders to simulate import.
- Imported scenes load in preview scenes with valid paint, matching color/vertex counts, empty source colors, live additional streams and the expected public shader.
- Checks validate normals/tangents, outward rock normals, all six owned texture references per material, missing-component absence, caption mapping/brush text, free Blue/Alpha channels, and absence of staging/test dependencies.
- Existing 150 painting/binding/shader tests remain green.

Final complete-suite evidence: `TestResults/TeachingSamples-package-final.xml` and `Logs/TeachingSamples-package-final.log` (153/153 passed). Authoring evidence: `Logs/TeachingSamples-author-final.log`.

An initial import check expected an asset-relative import path; Unity returns an absolute path on this baseline. The check now validates the absolute destination stays inside the project's Samples directory, then converts it to an AssetDatabase-relative path. Existing imported samples are rejected rather than overwritten, and only test-owned newly imported folders are removed.

## Clean consumer

A fresh project was created with only an embedded package copy, URP 17.4.0 and Test Framework 1.6.0, with SimpleBlend enabled as a testable package. It has none of the development project's assets or render settings. All three focused Package Manager import cases passed, with zero failures/skips. After normalizing Unity-generated trailing whitespace and adding notice metadata, the three checks passed again on the final sample files.

Final consumer evidence: `TestResults/TeachingSamples-consumer-final.xml` and `Logs/TeachingSamples-consumer-final.log`; strengthened reference/geometry assertions also passed all three cases in `TestResults/TeachingSamples-reference-check.xml` / `Logs/TeachingSamples-reference-check.log`. The development suite covers runtime behavior; this isolated consumer run covers actual sample discovery/import, scene references, paint state, normals/tangents and captions. No consumer rendering result is implied.

The first deeply nested consumer root produced a 285-character dependency DLL path and PackageCache extraction errors. The consumer was recreated at a shorter isolated path. This setup issue is not a sample failure or a compatibility claim.

## Remaining acceptance

Owner-run first-use and appearance review remain open: painting feel, readability of captions, previews, erase/smooth, Undo/Redo, scene save/reopen and independent paint on a duplicate. No player build or photoreal-quality claim follows from these import tests. Samples require a consumer URP asset; they deliberately do not overwrite render settings. Unity 6.3 LTS / 6.6 and issue #4's rendering gate remain separate work.
