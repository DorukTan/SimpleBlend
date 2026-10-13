# Installation

Use a URP project and back it up before adding a preview package. SimpleBlend does not create or replace your project's render pipeline settings.

## Choose a revision

The repository is [DorukTan/SimpleBlend](https://github.com/DorukTan/SimpleBlend). The package is inside `Packages/com.simpletools.simpleblend`, not at the repository root.

At this documentation checkpoint, `main` contains the M7 implementation. Terrain and teaching samples are proposed in PRs #15/#16/#19; Shader Graph assets are in separate PR #14. All currently share the development manifest version `0.4.0-beta.1`, so version alone does not identify available preview features.

For a reproducible **development preview** of terrain and teaching samples, use commit `6c9938bbff7d2656bfc25d8c11d9841f687af664`. This is unmerged work, not an approved release. It does not include PR #14's graph references. Do not invent a release tag or assume every open PR is included in `main`.

## Git URL

In Package Manager, open the Add (+) menu and choose the Git URL option. Paste:

```text
https://github.com/DorukTan/SimpleBlend.git?path=/Packages/com.simpletools.simpleblend#6c9938bbff7d2656bfc25d8c11d9841f687af664
```

Keep the subfolder query before the revision fragment. Install Git and make it available to Unity through PATH. If Package Manager reports a fetch/credential error, check Git access before changing package files. See Unity's [Git installation procedure](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-giturl.html) and [subfolder/revision syntax](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html).

Prefer a reviewed release tag or immutable commit for a real project once available. Updating a branch URL can change behavior without changing the development package version; record the resolved commit and lockfile.

## Local or embedded package

For a local checkout, use Package Manager's disk/local-package option and select `Packages/com.simpletools.simpleblend/package.json`, not the project's `Packages/manifest.json`. Unity documents [local package installation](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-local.html).

An embedded development copy belongs at your project's `Packages/com.simpletools.simpleblend/`, with the package's own manifest and metadata preserved. Do not install a second copy under Assets or combine an embedded copy with a conflicting Git dependency of the same ID.

No scoped-registry distribution is currently declared. A Git repository snapshot TAR is not automatically an installable UPM release archive.

## Check the consumer project

1. Let Package Manager resolve dependencies and finish compilation. Address Console errors before painting.
2. Use the matching URP asset in Graphics and the active Quality level. A different quality asset can change player behavior.
3. Create a material using `SimpleBlend/Layer Blend 3` or `SimpleBlend/Layer Blend 4`.
4. Follow [first stroke](first-stroke.md), or import a [teaching sample](teaching-samples.md).
5. Save the scene, reopen it and confirm your paint state and materials. Owner first-use review is still a separate acceptance gate.

The manifest targets Unity 6000.3 and URP 17.3.0, but only the exact [observed baseline](compatibility.md) is described as tested. The recent clean-consumer test explicitly selected URP 17.4.0; it does not validate the minimum manifest versions.

The pinned URL above was also exercised by Unity Package Manager on that baseline: the consumer lockfile resolved to the stated commit and all three public sample imports passed. This verifies Git/subfolder resolution and sample import, not an interactive installation walkthrough or full release approval.

Use a short consumer project path on Windows. An earlier nested test root produced a 285-character Unity dependency path and extraction errors. Inspect the full log before blaming sample assets or deleting caches.
