# Checkpoint A — package foundation

Date: 2026-08-28

Result: passed.

## Environment

- Unity: `6000.4.8f1` (`f8b72d3d7343`)
- Universal Render Pipeline: `17.4.0`
- Test Framework: `1.6.0`
- Build target: Windows 64-bit
- Baseline commit: `24d0503`

## Delivered boundary

- Embedded package `com.simpletools.simpleblend` at version `0.1.0-dev.1`.
- MIT license and honest compatibility statement.
- `SimpleBlend.Runtime`, `SimpleBlend.Editor`, and `SimpleBlend.Editor.Tests` assemblies.
- Editor assembly references Runtime; Runtime has no Editor reference.
- Reserved package directories for documentation, samples, hand-written shaders, and Shader Graph assets.
- One foundation test covering package discovery, identity, version, and assembly names.

No vertex-paint data model, Scene view tool, mesh mutation, renderer integration, or shader implementation exists at this checkpoint.

## Verification evidence

| Gate | Observed result |
|---|---|
| Package discovery | Unity registered `com.simpletools.simpleblend` as an embedded package |
| EditMode test | Passed: 1 total, 1 passed, 0 failed, 0 skipped |
| Test identity | `SimpleBlend.Editor.Tests.PackageFoundationTests.EmbeddedPackageHasExpectedIdentityAndAssemblyBoundaries` |
| Player compile | Windows 64-bit build completed with `Result: Success` and process exit code 0 |
| Package-owned compiler errors | None observed |
| External dependencies | URP only; no third-party source or binary added |

Local evidence is written under ignored `TestResults/` and `Builds/CheckpointA/` directories. The test XML and logs are reproducible with the command in the package README.

## Scope audit

Unity serialized unrelated URP prefilter caches, volume defaults, and standalone batching defaults during the verification build. Those changes were identified and reverted; they are not part of SimpleBlend.

Checkpoint B begins with the serialized data model and validation vocabulary before any Scene view painting code.
