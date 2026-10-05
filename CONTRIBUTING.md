# Contributing to SimpleBlend

Use the [roadmap](docs/github-roadmap.md) and open issues to choose a bounded change. For a new feature, explain the user problem and proposed behavior in an issue before implementation. Bug reports should include exact Unity and URP versions and a reproducible case.

## One issue, one PR

Create a branch from `main` for one actionable issue. Keep its PR focused on that issue's success criteria; include necessary code, verification, and documentation together.

Add `Closes #<number>` to the PR description. The issue closes when the completed PR is merged. Track independent findings in separate issues and PRs. Milestones represent the broader phase and do not need their own implementation PR.

Describe the final behavior, relevant verification, and any unobserved acceptance checks. Update the package changelog when user-visible behavior changes. Version bumps and release tags belong to their release issues.

## Package boundaries

- Preserve imported source meshes and per-renderer paint isolation.
- Keep serialized `Color32[]` data authoritative; transient rendering meshes are adapters.
- Reject changed topology rather than applying paint to incompatible geometry.
- Keep editor APIs out of runtime assemblies and player builds.
- Use public Unity APIs and justify dependency changes.
- Follow the documented channel mapping and terrain limits.
- Record asset provenance and redistribution terms for distributed samples.

URP is the v1 rendering scope. Runtime painting, HDRP, sculpting, texture painting, and a general mesh utility library are outside that scope.

## Verification

Use the smallest meaningful check for the change, then run the complete package EditMode suite at an implementation checkpoint. The [package README](Packages/com.simpletools.simpleblend/README.md#development-verification) contains the current baseline batch command.

Data and storage changes need regression evidence for source preservation, topology validation, shared instances, Undo, and prefab/lifecycle behavior as relevant. Shader changes need pass compilation and retained player-build checks as relevant. Documentation and metadata changes need link, syntax, and diff review rather than a Unity build.

State the exact Editor, URP, and Shader Graph versions used. Retain relevant logs or result XML as verification artifacts, outside the distributed package. Never describe an unrun check as passed.

Unity 6.3 LTS and 6.6 remain target versions until their validation results are recorded. Current baseline results apply only to the versions named in their checkpoint records.

Live brush, rendering, terrain-seam, and removal observations are explicit acceptance work. A reviewer performs those checks in Unity and records the settings, versions, and results. The [acceptance issue](https://github.com/DorukTan/SimpleBlend/issues/10) owns the final ten-test record.

## Local files and generated output

Stage only files required by the issue. Preserve unrelated local changes. Include Unity `.meta` files with package assets that require them. Do not commit Library, build output, logs, test output, credentials, or user-local editor settings.

## Releases

Complete implementation, sample, documentation, licensing, and acceptance gates before the release candidate. Prepare each release version and changelog through its own PR. Tags and release notes identify the merged package commit.
