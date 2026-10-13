# Checkpoint H — visual acceptance preparation

Issue #4 remains open. This checkpoint prepares the owner-run gate; it does not report visual parity or seam success.

## Added

- A versioned protocol for paired graph/HLSL captures, two terrain cases, three camera distances, Forward/Deferred paths, and Editor/player comparisons.
- Fixed native resolution, camera/light settings, crops, image metrics and thresholds defined before judging captures.
- A report template that requires exact versions, commits, render settings, assets, observer, capture hashes, comparison output and source-file preservation.
- A Windows PowerShell PNG comparator that reads captures without changing them. Its JSON `passed` field reports the metric decision; human review remains required.
- Synthetic comparator checks for identical, near, large and sparse errors, hashes, percentile calculation, valid crops, mismatched sizes, invalid crops, transparency, missing inputs and mislabeled non-PNG files.

## Observed

All **15 comparison self-tests passed** on Windows with PowerShell 7.6.5. These tests use temporary synthetic bitmaps, not Unity captures. The test script removes only its GUID-owned temporary directory after validating its absolute path.

No Unity rendering, new player build, live editor operation or desktop automation was performed for this preparation slice. Existing Unity implementation evidence remains in the graph and terrain checkpoints.

## Pending

- A reviewed integration of PR #14 with PRs #15/#16; this branch does not include graph implementation.
- Owner confirmation/use of the predeclared criteria, repeatable capture controls, all required render captures and observer notes.
- Actual source hashes before/after the owner session.
- Review of the completed evidence report and any failures.

Until these exist, graph/HLSL appearance, terrain seams, and Editor/player visual parity are **not accepted**. Frame Debugger, lightmapping, steep surfaces, performance, and additional Unity versions remain explicitly unobserved.
