# Redistribution audit — implementation snapshots

Status: **provisional; issue #8 remains open**. Issue #5's teaching samples and the final candidate archive do not exist in the audited snapshots. This is a factual content/dependency inventory, not legal approval or a declaration that all future release content is cleared.

## Actual inspected contents

Two committed package snapshots were archived and inspected independently. They are not a combined build or installable release-candidate archive.

| Snapshot | Commit | Files including metadata | Content bytes | Snapshot TAR SHA-256 |
|---|---|---|---|---|
| Terrain implementation, PR #16 | `cdb64b1c78e25cf1831c19d9a1449ef82e2cc0d1` | 202 | 489374 | `EBF797B2757C039B26244996D1EE5DBEFE56EA5F90C3BFD9F6EFF0FAE7009696` |
| Shader Graph implementation, PR #14 | `1985f23718bb66f596021278f6928e4fc1c7433c` | 225 | 832902 | `4EB687EF034F7A8848FC6609199EE09D26C3F45B08702649C5F9D04A09B28DEC` |

Archives contain only the committed `Packages/com.simpletools.simpleblend` subtree, with its LICENSE present. Dirty scenes/settings, development-project assets, PackageCache, generated reference scenes, player builds and test logs are excluded. Repeating the terrain snapshot produced the same hash. Git snapshot TARs preserve the repository package path; they are evidence snapshots, not UPM release packaging.

Every file entry in both TARs matched the Git inventory (202 terrain / 225 graph files). The inventory script parsed without errors and rejected an invalid revision. These checks ran on Windows with PowerShell 7.6.5; no Unity run or new player build was needed for this documentation/inventory slice. The notices added by this audit PR are not part of the earlier snapshot hashes and must be included in the final candidate inventory.

The terrain snapshot contains 64 C# files, seven HLSL files, ten shaders, three assembly definitions, documentation/metadata, and no committed model, image, material, scene or binary payload. The graph snapshot adds the actual procedural texture/material/graph references listed below; it does not include terrain changes. Neither snapshot registers importable teaching samples; `Samples~/README.md` is only a placeholder.

## Content provenance ledger

All paths below are relative to the package root. “Declared MIT” refers to the package's existing license, not an independent ownership determination. The release owner must confirm rights to project-authored content and resolve any adapted-source uncertainty.

| Included content | Traceable origin | Declared terms / review state |
|---|---|---|
| Runtime, editor, tests, preview/Layer Blend shaders, HLSL and documentation | Committed SimpleBlend project implementation/history | Package MIT notice; owner rights confirmation pending release review |
| Terrain shader, public-data binder and property-fixture shaders | PRs #15/#16 implementation; terrain checkpoint documents external API/source research | Package MIT notice; inspect any adapted-source origin before final clearance |
| `ShaderGraph/Examples/Textures/Albedo0.asset` through `Albedo3.asset` | `Tests/Editor/ShaderGraphReferenceFixture.cs`: procedural color/coordinate formula | Declared MIT, no downloaded texture source recorded |
| `ShaderGraph/Examples/Textures/Height0.asset` through `Height3.asset` | Same fixture: procedural height formula | Declared MIT, no downloaded texture source recorded |
| `ShaderGraph/Examples/Textures/Normal.asset` and `Mask.asset` | Same fixture: procedural packed-channel formulas | Declared MIT, no downloaded texture source recorded |
| `ShaderGraph/Examples/Materials/Graph3.mat`, `Graph4.mat`, `Hlsl3.mat`, `Hlsl4.mat` | Same fixture: named package shaders and the ten procedural textures | Declared MIT; referenced shader packages are separate dependencies |
| `ShaderGraph/Examples/LayerBlend3Parity.shadergraph`, `LayerBlend4Parity.shadergraph` | PR #14 authored graph assets invoking project blend functions | Declared MIT; native graph data is not a vendored Shader Graph implementation |
| Three `ShaderGraph/Subgraphs/*.shadersubgraph` assets | PR #14 project height/surface functions and graph connections | Declared MIT; retain project license |
| Terrain/reference scene assets created by test fixtures | Procedural fixture methods in Tests/Editor | Not present in either package snapshot; audit separately if shipped later |
| Rock/moss, roof/snow, wall/grime teaching samples | Not implemented in these snapshots | Missing; no origin or clearance can be asserted |

No DLL, executable, archive, font, audio or imported model was found in either inspected package tree. That file inventory is not proof of authorship for text/serialized assets or a blanket future “no third-party content” guarantee.

## Dependency relationships and necessity

Package manifest declares `com.unity.render-pipelines.universal: 17.3.0`. The development project resolves newer versions below; minimum-version compatibility has not been validated by this audit.

| Relationship | Observed installed version | Why used | Distribution boundary |
|---|---|---|---|
| URP, declared package dependency | 17.4.0 | Public shaders, URP includes and renderer storage checks | Registry package, not vendored |
| SRP Core, through URP | 17.4.0 | Rendering infrastructure; provides GPU-driven assembly reference | Registry package, not vendored |
| Shader Graph, through URP | 17.4.0 | Imports graph/subgraph references in PR #14 | Registry package, not vendored |
| Collections, through SRP Core | 6.4.0 resolved; Core manifest requests 2.4.3 | Native collections in mesh/color read paths; Runtime and Editor asmdef references | Registry package, not vendored |
| Unity Test Framework | 1.6.0 | Editor test assembly / NUnit integration in the development project | Test runner is not bundled |
| Burst and Mathematics, through Unity dependencies | See resolved lockfile for candidate version | Unity dependency requirements; no direct SimpleBlend Burst assembly/API usage found | Transitive packages, not brush-kernel requirements |
| Development template packages (navigation, timeline, input, collaboration, etc.) | Development manifest only | No package dependency claim follows from the template manifest | Excluded from these package snapshots |

Inspected installed LICENSE.md files identify URP, SRP Core, Shader Graph, Collections and Test Framework as Unity Companion licensed. They are not MIT merely because SimpleBlend references them. The installed package closure may include third-party notices under other terms; review the resolved candidate graph if dependencies are redistributed.

Collections is an actual direct assembly/API usage currently supplied transitively. A clean consumer check on each supported editor must establish resolution and API compatibility; do not silently pin the development editor's Collections 6.4.0 into a Unity 6.3 package. Runtime/editor dependency declarations are a compatibility gate, separate from content licensing.

No paid Asset Store dependency is declared or bundled in the inspected package trees. This observation does not clear assets added by future sample work.

## Notices and policy

The existing package LICENSE is a complete MIT notice for SimpleBlend. `ThirdPartyNotices.md` now explains the external dependency boundary; it is informational and does not substitute for vendor licenses if vendor code/assets are actually copied.

- Retain SimpleBlend's copyright and MIT license when redistributing its content.
- Do not bundle Unity PackageCache, vendor samples or Asset Store content by default.
- Record exact origins and required notices for any copied/adapted third-party content. Resolve uncertain rights before the candidate is cleared.
- Do not label Unity Companion licensed content as MIT or infer its redistribution terms from free availability.
- Audit generated sample assets after they become actual distribution files, not from proposed names.

Sources: [MIT terms](https://opensource.org/license/mit), [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license), and [Unity package legal-file guidance](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-legal). Installed package license/manifest files provide the version-specific observations above.

## Reproduction

From a repository checkout with both commits available:

```powershell
$inventory = ./tools/Get-PackageInventory.ps1 -Revision cdb64b1
$inventory | ConvertTo-Json -Depth 6
tar -tf $inventory.snapshotPath
```

The script verifies a local commit, inventories every committed package blob and size, requires LICENSE, rejects symlinks/non-file tree entries, and creates a snapshot in a new GUID-owned `TestResults/RedistributionAudit_*` directory. It does not include uncommitted files or modify the package. Returned Git blob IDs are Git identifiers; the archive fingerprint is SHA-256. Snapshot files remain local for inspection.

## Required to complete issue #8

- [ ] Teaching sample assets exist, with per-file origins and terms.
- [ ] Reviewed M8/M9 integration produces a concrete release candidate.
- [ ] Owner confirms project-authored content rights; any adapted-source uncertainty is resolved with required notices.
- [ ] Actual candidate file list, assets, dependency resolution and archive hash match the inventory.
- [ ] Final candidate contains LICENSE and all applicable vendor notices.
- [ ] Clean-consumer compatibility and separate visual/release gates are satisfied without broadening license claims.

No issue #8 success criterion is marked complete by this provisional record.
