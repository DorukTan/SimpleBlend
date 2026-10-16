# SimpleBlend product roadmap

Scope revised on 2026-10-16 in [#23](https://github.com/DorukTan/SimpleBlend/issues/23). The target is an independently implemented, MIT-licensed toolkit covering all researched public RealBlend workflows, plus the additional capabilities below. This is a larger product than the original URP vertex painter. Current code is a foundation, not a release candidate.

Read [research and the feature ledger](realblend-research.md) for source confidence, discrepancies and current gaps. [product-roadmap.json](product-roadmap.json) holds the detailed tasks and tracker identities. Each issue has its problem, solution, success criteria, verification, dependencies, labels and focused-PR requirement.

## Existing work

M0-M7 record completed foundation/automated implementation gates, not finished authoring experience or visual acceptance. M8 remains open for graph/terrain implementation review and observed acceptance. M9 retains samples, documentation, licensing, consumer acceptance and release qualification, but cannot complete before the expanded product milestones.

Existing issues #1-#12 remain valid. Integration #21 / PR #22 brings developed branches into the working checkout; local integration does not merge those PRs remotely. Never close unfinished evidence work because a later integration exists.

## New milestones

| Milestone | Required outcome | Focused issues |
| --- | --- | --- |
| [M10 — Artist workflow and editor experience](https://github.com/DorukTan/SimpleBlend/milestone/11) | Make setup, authoring modes and recovery understandable without exposing storage internals as the primary interface. | [#24](https://github.com/DorukTan/SimpleBlend/issues/24), [#25](https://github.com/DorukTan/SimpleBlend/issues/25), [#26](https://github.com/DorukTan/SimpleBlend/issues/26) |
| [M11 — Advanced brushes and procedural painting](https://github.com/DorukTan/SimpleBlend/milestone/12) | Complete expressive painting, reliable target selection and repeatable surface treatments. | [#27](https://github.com/DorukTan/SimpleBlend/issues/27), [#28](https://github.com/DorukTan/SimpleBlend/issues/28), [#29](https://github.com/DorukTan/SimpleBlend/issues/29) |
| [M12 — Texture preparation and material libraries](https://github.com/DorukTan/SimpleBlend/milestone/13) | Provide safe mask packing, texture-set discovery and reusable array libraries. | [#30](https://github.com/DorukTan/SimpleBlend/issues/30), [#31](https://github.com/DorukTan/SimpleBlend/issues/31), [#32](https://github.com/DorukTan/SimpleBlend/issues/32) |
| [M13 — Complete shader and pipeline coverage](https://github.com/DorukTan/SimpleBlend/milestone/14) | Deliver advertised rendering workflows across URP, HDRP and Built-in without changing legacy paint interpretation. | [#33](https://github.com/DorukTan/SimpleBlend/issues/33), [#34](https://github.com/DorukTan/SimpleBlend/issues/34), [#35](https://github.com/DorukTan/SimpleBlend/issues/35), [#36](https://github.com/DorukTan/SimpleBlend/issues/36), [#37](https://github.com/DorukTan/SimpleBlend/issues/37), [#38](https://github.com/DorukTan/SimpleBlend/issues/38) |
| [M14 — Editable geometry and recovery foundation](https://github.com/DorukTan/SimpleBlend/milestone/15) | Extend the color-only authoring contract to geometry edits without sacrificing source, prefab or Undo safety. | [#39](https://github.com/DorukTan/SimpleBlend/issues/39), [#40](https://github.com/DorukTan/SimpleBlend/issues/40) |
| [M15 — Sculpting and topology authoring](https://github.com/DorukTan/SimpleBlend/milestone/16) | Add practical sculpting, element edits and local topology operations. | [#41](https://github.com/DorukTan/SimpleBlend/issues/41), [#42](https://github.com/DorukTan/SimpleBlend/issues/42), [#43](https://github.com/DorukTan/SimpleBlend/issues/43), [#44](https://github.com/DorukTan/SimpleBlend/issues/44), [#45](https://github.com/DorukTan/SimpleBlend/issues/45) |
| [M16 — Procedural paint-ready mesh creation](https://github.com/DorukTan/SimpleBlend/milestone/17) | Create useful environment geometry with predictable density, UVs and pivots. | [#46](https://github.com/DorukTan/SimpleBlend/issues/46), [#47](https://github.com/DorukTan/SimpleBlend/issues/47), [#48](https://github.com/DorukTan/SimpleBlend/issues/48), [#49](https://github.com/DorukTan/SimpleBlend/issues/49) |
| [M17 — Boolean and solid-wall openings](https://github.com/DorukTan/SimpleBlend/milestone/18) | Support volume cuts and production door/window reveals safely. | [#50](https://github.com/DorukTan/SimpleBlend/issues/50), [#51](https://github.com/DorukTan/SimpleBlend/issues/51) |
| [M18 — Transfer, ProBuilder and standalone output](https://github.com/DorukTan/SimpleBlend/milestone/19) | Complete revision transfer, topology-owner integration and dependency-free finalized geometry. | [#52](https://github.com/DorukTan/SimpleBlend/issues/52), [#53](https://github.com/DorukTan/SimpleBlend/issues/53), [#54](https://github.com/DorukTan/SimpleBlend/issues/54) |
| [M19 — Capabilities beyond baseline parity](https://github.com/DorukTan/SimpleBlend/milestone/20) | Add reusable edit layers, batch/LOD workflows, texture-mask painting and depth-aware rendering. | [#55](https://github.com/DorukTan/SimpleBlend/issues/55), [#56](https://github.com/DorukTan/SimpleBlend/issues/56), [#57](https://github.com/DorukTan/SimpleBlend/issues/57), [#58](https://github.com/DorukTan/SimpleBlend/issues/58) |
| [M20 — Product acceptance and expanded release gates](https://github.com/DorukTan/SimpleBlend/milestone/21) | Prove usability, performance, compatibility and complete feature coverage before release qualification. | [#59](https://github.com/DorukTan/SimpleBlend/issues/59), [#60](https://github.com/DorukTan/SimpleBlend/issues/60), [#61](https://github.com/DorukTan/SimpleBlend/issues/61) |

No calendar deadlines or completion percentages are assigned. Dependencies, user review and evidence determine the sequence.

## Execution order

1. **Next: #24, then #25/#26.** Approve the artist workflow before building another collection of controls. Keep native Scene-view tools and source-safe setup; do not copy a competitor's interface.
2. **Core authoring:** M11 brush work, M12 texture preparation and M14 geometry state can proceed once their individual prerequisites are met. Geometry foundations precede sculpt/creation/cut work.
3. **Rendering:** M13 builds on material preparation. Make integrations optional so HDRP/Built-in consumers do not have to install URP. Never reinterpret existing alpha-free paint automatically.
4. **Geometry workflows:** M15 sculpt/topology and M16 generators precede M17 cuts and relevant M18 transfer/output work. Spline and ProBuilder integrations remain optional, public-API adapters.
5. **Beyond baseline:** M19 adds editable paint layers/recipes, atomic multi-object and LOD workflows, UV texture-mask painting for sparse meshes, and qualified depth-aware blending. These are requirements, not existing features or evidence of competitor absence.
6. **Qualification:** M20 closes observed usability, performance, graphics/platform and full-ledger acceptance. Existing #8/#9/#10 expand with the package, rather than retaining a color-only URP release boundary.
7. **Release:** only after #61 and its prerequisites pass may #11 prepare 0.9.0-rc.1; #12 then qualifies 1.0.0. A smaller earlier release requires an explicit user-approved scope change and must not claim full parity.

## Focused task index

### M10 — Artist workflow and editor experience

- [#24 Define the authoring workflow and approve editor interaction prototypes](https://github.com/DorukTan/SimpleBlend/issues/24) — Design, Testing.
- [#25 Implement a unified workspace and guided target/material setup](https://github.com/DorukTan/SimpleBlend/issues/25) — Design, Technical, Testing.
- [#26 Add coherent shortcuts, selection lock and trustworthy live feedback](https://github.com/DorukTan/SimpleBlend/issues/26) — Design, Technical, Testing, accessibility.

### M11 — Advanced brushes and procedural painting

- [#27 Add textured stamps and reusable brush presets](https://github.com/DorukTan/SimpleBlend/issues/27) — Technical, Design, Testing.
- [#28 Add deterministic procedural dirt, grime and edge masks](https://github.com/DorukTan/SimpleBlend/issues/28) — Technical, Design, Testing.
- [#29 Make brush targeting surface-aware and submesh-aware](https://github.com/DorukTan/SimpleBlend/issues/29) — Technical, Testing.

### M12 — Texture preparation and material libraries

- [#30 Implement exact manual mask packing and import profiles](https://github.com/DorukTan/SimpleBlend/issues/30) — Technical, Testing.
- [#31 Add texture-set discovery and safe batch material preparation](https://github.com/DorukTan/SimpleBlend/issues/31) — Technical, Design, Testing.
- [#32 Build stable texture-array libraries and material conversion](https://github.com/DorukTan/SimpleBlend/issues/32) — Technical, Testing.

### M13 — Complete shader and pipeline coverage

- [#33 Add five-layer URP blending without breaking existing channel profiles](https://github.com/DorukTan/SimpleBlend/issues/33) — Technical, Testing.
- [#34 Deliver optional HDRP three-layer and five-layer shaders](https://github.com/DorukTan/SimpleBlend/issues/34) — Technical, Testing.
- [#35 Deliver a Built-in three-layer fallback and capability guidance](https://github.com/DorukTan/SimpleBlend/issues/35) — Technical, Testing, Documentation.
- [#36 Add variation and painted wetness with explicit semantic profiles](https://github.com/DorukTan/SimpleBlend/issues/36) — Technical, Design, Testing.
- [#37 Add UV/triplanar, bounded parallax and painted cutout variants](https://github.com/DorukTan/SimpleBlend/issues/37) — Technical, Testing.
- [#38 Connect array-backed materials and reusable Shader Graph integration](https://github.com/DorukTan/SimpleBlend/issues/38) — Technical, Testing.

### M14 — Editable geometry and recovery foundation

- [#39 Introduce owned editable geometry state and complete persistence](https://github.com/DorukTan/SimpleBlend/issues/39) — Technical, Testing.
- [#40 Add checkpoints, pose/topology revert and explicit paint reprojection](https://github.com/DorukTan/SimpleBlend/issues/40) — Technical, Design, Testing.

### M15 — Sculpting and topology authoring

- [#41 Implement pull, push, smooth, nudge and axis flatten brushes](https://github.com/DorukTan/SimpleBlend/issues/41) — Technical, Design, Testing.
- [#42 Add local refinement with safe attribute and boundary handling](https://github.com/DorukTan/SimpleBlend/issues/42) — Technical, Testing.
- [#43 Add circle/square face removal and controlled rim adjustment](https://github.com/DorukTan/SimpleBlend/issues/43) — Technical, Testing.
- [#44 Add vertex, edge and face selection with transform handles](https://github.com/DorukTan/SimpleBlend/issues/44) — Technical, Design, Testing.
- [#45 Add normals, tangents, UV repair and mesh diagnostics](https://github.com/DorukTan/SimpleBlend/issues/45) — Technical, Testing, Documentation.

### M16 — Procedural paint-ready mesh creation

- [#46 Create density-controlled floors and surface/solid/profile walls](https://github.com/DorukTan/SimpleBlend/issues/46) — Technical, Design, Testing.
- [#47 Create curved walls and inward/outward beveled boxes](https://github.com/DorukTan/SimpleBlend/issues/47) — Technical, Testing.
- [#48 Generate spline tunnels and profile-based structures](https://github.com/DorukTan/SimpleBlend/issues/48) — Technical, Testing.
- [#49 Add responsive creation previews, placement and snapping](https://github.com/DorukTan/SimpleBlend/issues/49) — Design, Technical, Testing.

### M17 — Boolean and solid-wall openings

- [#50 Implement validated single and atomic multi-target boolean subtraction](https://github.com/DorukTan/SimpleBlend/issues/50) — Technical, Testing.
- [#51 Cut solid-wall doors/windows with materialized reveal faces](https://github.com/DorukTan/SimpleBlend/issues/51) — Technical, Testing.

### M18 — Transfer, ProBuilder and standalone output

- [#52 Transfer paint to revised meshes with match and projection previews](https://github.com/DorukTan/SimpleBlend/issues/52) — Technical, Design, Testing.
- [#53 Bake standalone painted/sculpted meshes and remove storage safely](https://github.com/DorukTan/SimpleBlend/issues/53) — Technical, Testing, Documentation.
- [#54 Add optional ProBuilder paint synchronization and ownership guards](https://github.com/DorukTan/SimpleBlend/issues/54) — Technical, Testing.

### M19 — Capabilities beyond baseline parity

- [#55 Add reusable non-destructive paint layers and mask recipes](https://github.com/DorukTan/SimpleBlend/issues/55) — Design, Technical, Testing.
- [#56 Add atomic multi-object painting and LOD color propagation](https://github.com/DorukTan/SimpleBlend/issues/56) — Technical, Design, Testing.
- [#57 Add UV texture-mask painting for sparse meshes](https://github.com/DorukTan/SimpleBlend/issues/57) — Technical, Design, Testing.
- [#58 Prototype and qualify depth-aware blend rendering](https://github.com/DorukTan/SimpleBlend/issues/58) — Technical, Testing.

### M20 — Product acceptance and expanded release gates

- [#59 Run first-use, accessibility and production-task acceptance](https://github.com/DorukTan/SimpleBlend/issues/59) — Design, Testing, accessibility.
- [#60 Qualify rendering, performance and platform/dependency combinations](https://github.com/DorukTan/SimpleBlend/issues/60) — Technical, Testing, Release.
- [#61 Close the evidence-backed feature ledger before RC and 1.0](https://github.com/DorukTan/SimpleBlend/issues/61) — Testing, Release, Documentation.

## Non-negotiable acceptance

- Imported and builtin source meshes/materials/textures remain unchanged unless the user explicitly approves a separate recoverable source-cleanup action.
- One user gesture is one Undo. Multi-target commits are atomic; preflight failure or cancellation changes no target.
- Existing paint/channel contracts survive upgrades. New semantic profiles, geometry state and material libraries have versioned migrations and explicit copy conversion.
- Closed milestones or a passing test count do not prove useful UX, valid lighting, interactive performance or player parity.
- First-use qualification includes at least three participants, a recorded five-minute first-stroke target, task outcomes and retested blocking findings. Report the small sample honestly.
- Pipeline/API/OS claims identify exact observed versions and evidence. Unity 6.3 LTS, the listing's 6.5 baseline, and 6.6 are validation targets until tested; do not infer compatibility.
- Every required feature-ledger row and planned improvement gate needs an implementation PR, automated results and applicable observed workflow/render evidence before acceptance.
- Independent implementation and MIT-compatible redistribution remain requirements. Review optional dependency licenses and provenance before adopting code/assets.

## Still outside this expanded release

Runtime painting APIs, terrain sculpting, geo-scattering, arbitrary third-party terrain-shader adapters and automatic orphaned generated-asset deletion are not established RealBlend parity requirements. They remain deferred unless separately requested. HDRP, sculpting, procedural creation, topology operations, transfer/LOD workflows and texture-mask painting are explicitly promoted into this expanded plan; older deferral lists no longer exclude them.

## Delivery rules

Use one focused issue branch/PR at a time. If a task grows too large, split reviewed child issues first and deliver each through its own PR. Do not close an issue solely on implementation if its acceptance evidence remains open. Do not merge PRs, publish tags or change release/support claims without the required authority and evidence.
