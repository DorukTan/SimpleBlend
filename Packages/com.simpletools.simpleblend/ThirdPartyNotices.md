# Dependency and redistribution boundaries

SimpleBlend's own content is distributed under the included MIT license and its copyright notice. Preserve that license when redistributing copies or substantial portions. This notice does not relicense external dependencies.

The package declares Unity's Universal Render Pipeline dependency. Unity Package Manager supplies URP and its dependency packages separately; Unity package source, binaries and vendor samples must not be copied into a SimpleBlend distribution merely because they are available in the development project's PackageCache.

The inspected Unity URP, SRP Core, Shader Graph, Collections and Test Framework packages carry Unity Companion License notices. Those packages remain subject to their own licenses and any notices they ship. See the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). Dependencies are not covered by SimpleBlend's MIT license.

Shader includes reference installed URP libraries. Runtime/editor assembly definitions also reference Unity Collections and rendering assemblies. Test Framework and its NUnit support are development/test dependencies, not vendored libraries. The managed brush implementation does not directly require Burst; the resolved Unity dependency graph can still install Burst transitively.

Any future third-party code, texture, model, font, audio, sample or adapted source requires an origin record, redistribution review and its required notices before inclusion. A package manifest's MIT field alone is not evidence of rights to every new asset.

The repository's redistribution audit is provisional until the actual release candidate and teaching samples have been reviewed. No Asset Store or dependency license is overridden by this notice.
