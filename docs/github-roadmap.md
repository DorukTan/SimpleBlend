# GitHub roadmap

The [milestones](https://github.com/DorukTan/SimpleBlend/milestones?state=all) cover the package from API verification through 1.0. The earlier implementation history remains in [roadmap.md](roadmap.md) and the checkpoint records.

Completed implementation milestones do not imply that deferred visual or release acceptance checks have passed. Those checks remain visible in the open release issues.

## Milestones

| Stage | Status | Outcome |
| --- | --- | --- |
| [M0](https://github.com/DorukTan/SimpleBlend/milestone/1) | Closed | Installed public API behavior documented |
| [M1](https://github.com/DorukTan/SimpleBlend/milestone/2) | Closed | Package structure and assembly boundaries |
| [M2](https://github.com/DorukTan/SimpleBlend/milestone/3) | Closed | Durable paint data and source safety |
| [M3](https://github.com/DorukTan/SimpleBlend/milestone/4) | Closed | First visible stroke with Undo/Redo |
| [M4](https://github.com/DorukTan/SimpleBlend/milestone/5) | Closed | Prefab, fallback, and lifecycle integration |
| [M5](https://github.com/DorukTan/SimpleBlend/milestone/6) | Automated implementation gate complete | Full brush model; live overlay review deferred |
| [M6](https://github.com/DorukTan/SimpleBlend/milestone/7) | Automated implementation gate complete | Isolation/density and managed profiling; live review deferred |
| [M7](https://github.com/DorukTan/SimpleBlend/milestone/8) | Automated implementation gate complete | URP height shaders; live rendering checks deferred |
| [M8](https://github.com/DorukTan/SimpleBlend/milestone/9) | Open | Graph integration and terrain matching; 0.4.0-beta.2 |
| [M9](https://github.com/DorukTan/SimpleBlend/milestone/10) | Open | Samples, consumer acceptance, RC, and 1.0.0 |

## Actionable issues

| Issue | Milestone | Labels | Depends on |
| --- | --- | --- | --- |
| [#1 Shader Graph subgraphs and parity coverage](https://github.com/DorukTan/SimpleBlend/issues/1) | M8 | Technical, Testing, Documentation | Established shader contract |
| [#2 Public terrain-data binder](https://github.com/DorukTan/SimpleBlend/issues/2) | M8 | Technical, Testing | Documented terrain contract |
| [#3 Terrain-matching URP shader](https://github.com/DorukTan/SimpleBlend/issues/3) | M8 | Technical, Testing | #2 |
| [#4 Graph parity and terrain-seam evidence](https://github.com/DorukTan/SimpleBlend/issues/4) | M8 | Testing, Documentation | #1, #2, #3 |
| [#5 Three teaching samples](https://github.com/DorukTan/SimpleBlend/issues/5) | M9 | Design, Technical, Testing | #1, #3 |
| [#6 Complete package user documentation](https://github.com/DorukTan/SimpleBlend/issues/6) | M9 | Documentation | #1, #2, #3, #5 |
| [#7 Public repository and contribution guidance](https://github.com/DorukTan/SimpleBlend/issues/7) | M9 | Maintenance, Documentation | None |
| [#8 Redistribution audit](https://github.com/DorukTan/SimpleBlend/issues/8) | M9 | Maintenance, Release | #5 |
| [#9 Unity 6.3/6.6 validation automation](https://github.com/DorukTan/SimpleBlend/issues/9) | M9 | Technical, Testing | Maintainer licensing configuration |
| [#10 Ten-test acceptance record](https://github.com/DorukTan/SimpleBlend/issues/10) | M9 | Testing, Release | #4, #5, #6, #8, #9 |
| [#11 Prepare 0.9.0-rc.1](https://github.com/DorukTan/SimpleBlend/issues/11) | M9 | Release, Testing | #6, #7, #8, #9, #10 |
| [#12 Release 1.0.0](https://github.com/DorukTan/SimpleBlend/issues/12) | M9 | Release | #11 |

Each issue contains its problem, solution, success checklist, verification, dependencies, and PR requirement. Deliver one actionable issue in one focused PR; close it on merge.

## Release gates

Unity 6.3 LTS and 6.6 are validation targets. The observed baseline remains Unity 6000.4.8f1 and URP 17.4.0 until the target-version evidence is available.

The final record covers source-file preservation, shared-mesh isolation, exact stroke Undo/Redo, prefab overrides, Prefab Mode, play-mode persistence, editor/player rendering parity, topology rejection, live 40k-vertex responsiveness, and safe package removal. Graph/terrain visuals, brush previews, batching, and baked output are additional recorded checks.

Manual checks are performed in Unity by the maintainer or reviewer and recorded through an evidence PR. Release work remains open until those observations are available.

No calendar due dates are assigned. Dependencies and verified milestone gates determine the sequence.

## Reference guides

- [Unity release support](https://unity.com/releases/unity-6/support) and the [Unity 6.6 announcement](https://discussions.unity.com/t/unity-6-6-is-now-available/1735357) inform the target-version matrix.
- [Unity Git package dependencies](https://docs.unity3d.com/Manual/upm-git.html) describe subfolder and tagged installation.
- [GitHub labels and milestones](https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work) describe the tracking model.
- [GameCI test runner](https://game.ci/docs/github/test-runner/) and [GitHub Actions security](https://docs.github.com/en/actions/reference/security/secure-use) inform the validation workflow.

## Deferred features

HDRP, runtime painting, sculpting, texture painting, terrain sculpting, LOD propagation, simultaneous multi-object strokes, arbitrary terrain-shader integration, automatic generated-asset deletion, and general mesh utility APIs are deferred beyond v1.
