# Checkpoint I — installation and usage guide

Issue #6 documentation coverage is prepared; candidate first-use/removal walkthroughs remain open.

## Added and corrected

- Git/subfolder/pinned-preview installation, local and embedded alternatives, source/revision availability and consumer checks.
- Exact observed compatibility scope versus Unity 6.3 LTS / 6.6 validation targets and manifest minima.
- First lesson with explicit brush channel values, source-color initialization, actual Renderer context command and actual Height Transition label.
- Shader Graph guidance explicitly conditional on PR #14 availability; terrain/sample preview does not silently claim graph integration.
- Troubleshooting mapped to validation, storage, terrain and dependency behavior.
- Removal guidance differentiating Clear, disable/delete, transient streams, retained clones, unrelated base materials and removed package shaders. No nonexistent bake/export command is promised.
- An indexed package guide and a repeatable read-only documentation checker.

## Observed verification

Windows / Unity 6000.4.8f1 / URP 17.4.0. The isolated consumer originally used an embedded package copy; only that owned copy was moved to a recoverable TestResults backup before testing Git installation.

- Unity Package Manager resolved the documented Git URL and subfolder to `6c9938bbff7d2656bfc25d8c11d9841f687af664`; consumer packages-lock.json recorded source `git` and that exact hash.
- All three public Package Manager sample-import checks passed, with no failures/skips: `TestResults/Documentation-git-consumer.xml` / `Logs/Documentation-git-consumer.log`.
- Documentation checker passed: 18 Markdown files, 28 local links, eight source control-label checks and eleven indexed pages.
- The same checks passed on a candidate package copy in the isolated consumer's TestResults directory. This verifies filesystem link portability, not installation of the new documentation through the pinned earlier commit.
- Setup/context labels were checked against VertexPaintSetupUtility; Inspector labels against VertexPaintDataInspector; shader label against SimpleBlendLayerBlendShaderGUI.
- Clear/disable/remove guidance was reviewed against VertexPaintRuntimeApplicator and VertexPaintData lifecycle code. Topology limits were reviewed against signature/validator implementation.

No runtime/editor implementation, dependency pin, source scene or unrelated render setting changed. No complete package suite or player build was rerun for this documentation-only slice. Earlier 153-test sample evidence remains separate.

## Remaining gates

Owner first-use and removal walkthroughs against a reviewed combined candidate are still required. Static link/control checks and Git sample imports do not establish painting feel, shader appearance or complete uninstall behavior. Graph PR #14 is not included in the tested pinned sample/terrain preview. Minimum-version and target-version matrices, issue #4 visual evidence and release acceptance remain open.
