# SimpleBlend implementation plan

This is the executable task order for the roadmap in `docs/roadmap.md`. Each task has a bounded result and a verification point. Do not start a later slice while an earlier gate is red.

## Execution rules

1. Preserve all existing project assets and unrelated settings.
2. Make one conceptual change per task.
3. Run the narrowest relevant test after each task.
4. Run the full SimpleBlend EditMode suite at each checkpoint.
5. Record observed results; do not convert expected behavior into a support claim.
6. Keep generated test assets inside a disposable test fixture or a verified temporary project.
7. Add no package dependency without a written license and necessity review.

## Intended package tree

```text
Packages/com.simpletools.simpleblend/
|-- package.json
|-- LICENSE
|-- README.md
|-- CHANGELOG.md
|-- Runtime/
|   |-- SimpleBlend.Runtime.asmdef
|   |-- Data/
|   |-- Storage/
|   `-- Rendering/
|-- Editor/
|   |-- SimpleBlend.Editor.asmdef
|   |-- Brush/
|   |-- Inspectors/
|   |-- Painting/
|   |-- Storage/
|   `-- Tools/
|-- Shaders/
|   |-- Includes/
|   `-- Preview/
|-- ShaderGraph/
|-- Tests/
|   `-- Editor/
|       `-- SimpleBlend.Editor.Tests.asmdef
|-- Samples~/
|   `-- Blend Examples/
`-- Documentation~/
```

Development evidence remains in the repository-level `docs/` folder. User-facing package documentation lives in `Documentation~/`.

## Batch A — recoverable foundation

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| FND-001 | Establish version-control baseline | Git repository and baseline commit exist, or the user explicitly records that Git is intentionally omitted | `git status` has a known baseline |
| FND-002 | Create embedded UPM skeleton | Package appears in Package Manager with correct name/version/Unity metadata | Unity import log is clean |
| FND-003 | Add MIT and package metadata | License, author, changelog, and support statement are consistent; repository metadata is omitted until a canonical public URL exists | Manual metadata review |
| FND-004 | Add runtime/editor/test asmdefs | Editor references Runtime; tests reference both; Runtime references no Editor assembly | Editor compile plus player compile |
| FND-005 | Add test namespace and smoke test | One SimpleBlend EditMode smoke test runs from CLI | XML result contains one pass |

Checkpoint A: review only the package boundary, metadata, assembly references, and compilation. No painting logic yet.

## Batch B — durable data and validation

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| DATA-001 | Define storage and validation enums | Modes and failure reasons are serializable and have teaching display text | Serialization round-trip test |
| DATA-002 | Define `MeshSignature` | Signature detects vertex-count, submesh-layout, and topology changes deterministically | Mutation matrix tests |
| DATA-003 | Define `VertexPaintData` | Versioned component stores source, signature, compact colors, resolved mode, fallback, and status | Serialization round-trip test |
| DATA-004 | Implement color initialization | Complete source colors are copied; otherwise clear colors match vertex count | Existing/no-color tests |
| DATA-005 | Implement validation service | Null, unreadable, changed topology, and wrong-length states return explicit results without applying | Validation branch tests |
| DATA-006 | Implement transient stream builder | A same-count color stream is built and disposed/rebuilt safely without touching source | Source hash and stream tests |
| DATA-007 | Implement storage policy | Ordinary, skinned, static, and GPU Resident Drawer cases resolve exactly as Phase 0 specifies | Policy table tests |
| DATA-008 | Implement fallback clone writer | Unique persistent asset preserves full mesh data and records ownership/path | Clone-equivalence test |
| DATA-009 | Implement apply/clear lifecycle | Apply, disable, reload, and clear leave authoritative data coherent | Lifecycle tests |
| DATA-010 | Add shared-mesh and reimport fixtures | Acceptance tests 1, 2, and 8 are reproducible in EditMode | Tests pass with result XML |

Checkpoint B: inspect serialized YAML, source hashes, generated paths, and failure messages before Scene view code begins.

## Batch C — first red-channel stroke

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| HIT-001 | Validate public `HandleUtility.RaySnap` behavior | Exact behavior is recorded for meshes with and without colliders and in Prefab Mode | Editor probe notes |
| HIT-002 | Implement CPU triangle raycaster fallback | Selected supported mesh, including non-readable imported meshes through public read-only mesh data APIs, can be hit without a `MeshCollider` or target mutation | Ray/triangle fixture tests |
| CACHE-001 | Implement mesh paint cache | Vertices, normals, triangles, submesh IDs, and transforms have one invalidation owner | Cache invalidation tests |
| CACHE-002 | Implement uniform vertex grid | Radius queries return only candidates in overlapping cells | Brute-force comparison tests |
| TOOL-001 | Register Scene view EditorTool | Tool appears in the palette and enables only for supported selection | Manual Scene view check |
| TOOL-002 | Capture Scene view input | Paint drag cannot fall through to normal object selection | Manual control check |
| TOOL-003 | Draw projected brush disc | Disc follows hit point/normal under orbit and perspective changes | Manual visual check |
| BRUSH-001 | Define pure brush command | Radius, strength, color, channel, and falloff have no editor dependencies | Unit tests |
| BRUSH-002 | Implement R paint kernel | Candidate vertices receive deterministic weighted red changes | Numerical tests |
| TOOL-004 | Bind one-stroke Undo | Exactly one undo entry restores the pre-stroke array and redo restores the result | Acceptance test 3 |
| VIS-001 | Add red-channel preview | Painted red is visible without changing renderer materials | Manual visual check |

Checkpoint C demo: paint one red stroke on a collider-free shared mesh, undo once, redo once, and show that its sibling instance remains unchanged.

Checkpoint C completed on 2026-08-30 for `0.1.0-dev.3`. The live Scene view demo and 29-test EditMode suite passed. The complete settings overlay remains intentionally deferred to `UI-001`; Checkpoint C uses fixed first-slice settings.

## Batch D — prefab and fallback integration

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| PREFAB-001 | Record instance overrides | Data changes appear in the Overrides UI and revert cleanly | Acceptance test 4 |
| PREFAB-002 | Support Prefab Mode | Saving a painted prefab asset updates instances | Acceptance test 5 |
| STORE-001 | Surface storage reason in Inspector | User can see stream/clone mode, why, signature state, and output path | Inspector review |
| STORE-002 | Integrate static fallback | Static renderer uses an owned clone and survives reload/build preprocessing | Static fixture test |
| STORE-003 | Integrate skinned fallback | Clone preserves bindposes, bone weights, and blend shapes | Skinned fixture test |
| STORE-004 | Integrate GPU Resident fallback | Public URP mode detection selects clone without internal APIs | Policy/integration test |
| LIFE-001 | Reapply on enable/load | Stream mode reconstructs and clone mode remains assigned across play mode | Acceptance test 6 |

Checkpoint D: run acceptance tests 1–6 and 8. Do not start full brush modes until prefab and persistence behavior is stable.

Checkpoint D completed on 2026-08-31 for `0.2.0-alpha.2`. The 36-test EditMode suite, prefab instance/revert and Prefab Mode live checks, edit/play lifecycle check, static/skinned fallback checks, GPU policy inspection, and Windows x64 player build passed. See `docs/checkpoints/checkpoint-d.md`.

## Batch E — complete brush behavior

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| BRUSH-003 | Add channel abstraction | R/G/B/A and RGB alter only intended components | Channel matrix tests |
| BRUSH-004 | Add erase | Selected channels move toward documented zero/baseline behavior | Boundary tests |
| BRUSH-005 | Add adjacency and smooth | One-ring weighted smoothing is stable and does not cross disconnected indices accidentally | Topology tests |
| BRUSH-006 | Add fill scopes | Whole mesh and selected submesh fill exactly once per command | Submesh tests |
| BRUSH-007 | Add sample mode | Hit-triangle interpolation yields a stable sampled color | Barycentric tests |
| BRUSH-008 | Add normal mask | World/local normal convention is correct under rotation and non-uniform/negative scale | Transform tests |
| BRUSH-009 | Add submesh mask | Only vertices participating in the selected submesh are eligible | Shared-vertex tests |
| BRUSH-010 | Add threshold mask | Inclusive/exclusive limits are documented and deterministic | Limit tests |
| BRUSH-011 | Add flow and accumulation | Results remain stable across reasonable Scene view event rates | Simulated stroke tests |
| UI-001 | Complete compact overlay | Controls expose the finished brush model without owning painting logic | Manual UI review |

Checkpoint E: every brush feature passes tests before performance optimization.

Checkpoint E's automated gate completed on 2026-08-31 for `0.3.0-alpha.1`: 66/66 EditMode tests passed for every channel, mode, mask, fill scope, boundary, transformed-normal convention, Undo path, and event-density contract. The compact overlay compiled and passed a construction regression test. Live overlay ergonomics were intentionally not exercised because the user requested no desktop control. See `docs/checkpoints/checkpoint-e.md`.

## Batch F — visualization and profiling

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| VIS-002 | Add R/G/B/A/RGB isolation | Each view displays authoritative colors independently of material | Reference captures |
| VIS-003 | Add density overlay | Relative sparse/dense areas are legible and transform-safe | Reference mesh review |
| PERF-001 | Build 40k fixture | Repeatable mesh and stroke path exist for profiling | Fixture checksum |
| PERF-002 | Remove paint-loop allocations | Drag samples allocate no recurring managed garbage | Profiler allocation check |
| PERF-003 | Measure cache/query/kernel | Timings are logged separately so the actual bottleneck is visible | Profiler record |
| PERF-004 | Decide on optional Burst path | Add nothing if managed targets pass; otherwise write a separate optional integration plan | Recorded decision |

Checkpoint F: acceptance test 9 passes on the local machine with recorded timings.

Checkpoint F's automated gate completed on 2026-09-04 for `0.3.0-alpha.2`: 73/73 EditMode tests passed, the deterministic 40k fixture checksum passed, one-time cache construction measured 60.353 ms, and the warmed managed drag path measured 0.037 ms median spatial query, 0.142 ms median kernel, 0.179 ms median total, 0.499 ms maximum, zero samples above 16 ms, and zero recurring managed bytes across 250 samples. Burst was not added because the managed path passed comfortably. R/G/B/A/RGB and transform-invalidated density behavior passed deterministic tests; live Scene view legibility remains unobserved under the user's no-desktop-control instruction. See `docs/checkpoints/checkpoint-f.md`.

## Batch G — HLSL shaders

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| SHD-001 | Lock channel/layer convention | README, material labels, samples, and code agree | Decision review |
| SHD-002 | Implement shared height-weight include | Three/four-layer normalized weights behave at zero, ties, extremes, and transition limits | Shader math tests/reference material |
| SHD-003 | Implement Layer Blend 3 | Albedo/normal/mask/height sets render with URP lighting | Reference scene |
| SHD-004 | Implement Layer Blend 4 | Fourth layer behaves identically under the same contract | Reference scene |
| SHD-005 | Add required URP passes | Shadows, depth, depth normals, meta/lightmapping, and forward/deferred fallback render correctly | Frame Debugger and bake checks |
| SHD-006 | Validate batching/instancing | Material is SRP Batcher compatible and DOTS declarations compile | Inspector/Frame Debugger/build |
| SHD-007 | Build player reference | Editor and player captures match within tolerance | Acceptance test 7 |

Checkpoint G: no Shader Graph work begins until hand-written shaders pass build parity.

Checkpoint G's automated implementation gate completed on 2026-09-04 for `0.4.0-beta.1`: nine focused shader tests passed, the final package suite passed 82/82 tests in 5.242 seconds, all six shared URP passes compiled without errors, and a Windows x64 D3D11 player build retained and compiled materials using both public shaders. The mapping is locked to base + R/G for Layer Blend 3 and base + R/G/B for Layer Blend 4, with alpha free. Live lighting, Frame Debugger batching, baked Meta output, and editor/play/player image parity remain unobserved under the user's no-desktop-control instruction, so acceptance test 7 remains open. See `docs/checkpoints/checkpoint-g.md`.

## Batch H — Shader Graph and terrain

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| SG-001 | Create height-weight subgraphs | Three/four-layer graph outputs match HLSL test vectors | Parity material |
| SG-002 | Create normal/mask composition | Graph result uses the same final weights as albedo | Reference capture |
| TERR-001 | Run terrain contract spike | Public data available, limitations, and chosen path are documented | Decision note |
| TERR-002 | Implement terrain binder | Material inputs derive from selected terrain without modifying it | Inspector/data test |
| TERR-003 | Implement terrain-match shader | Mesh/terrain seam matches under documented layer limits | Reference scene |

Checkpoint H: parity and terrain limitations are documented before samples are authored around them.

## Batch I — sample and release

| ID | Work | Definition of done | Verification |
|---|---|---|---|
| SAMPLE-001 | Create original sample assets | Rock, roof, wall, textures, and captions are redistributable | License manifest review |
| SAMPLE-002 | Author self-teaching scene | Each setup names channels and brush settings in scene | Fresh-user walkthrough |
| DOC-001 | Complete user docs | Installation, painting, storage, shaders, troubleshooting, and removal behavior are covered | Link/checklist review |
| REL-001 | Install package into clean project | Package imports and sample opens with no SimpleBlend errors | Clean-project log |
| REL-002 | Run complete acceptance matrix | All ten tests have observed results and version metadata | Signed test record |
| REL-003 | Remove package in disposable copy | Base-material case opens; no null-reference spam or corruption | Acceptance test 10 |
| REL-004 | Final license/dependency audit | Every distributed file has an understood redistribution basis | Audit record |
| REL-005 | Cut 1.0.0 | Version, changelog, docs, and package metadata agree | Final package inspection |

## Test commands

The exact installed editor can run EditMode tests with a command shaped like:

```powershell
New-Item -ItemType Directory -Force `
  -Path 'D:\Projects\SimpleBlend\TestResults', 'D:\Projects\SimpleBlend\Logs' | Out-Null

& 'C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'D:\Projects\SimpleBlend' `
  -runTests -testPlatform EditMode `
  -testFilter 'SimpleBlend.Editor.Tests' `
  -testResults 'D:\Projects\SimpleBlend\TestResults\SimpleBlend-EditMode.xml' `
  -logFile 'D:\Projects\SimpleBlend\Logs\SimpleBlend-EditMode.log'
```

Do not add `-quit` to this `-runTests` invocation on the tested Unity 6000.4 editor. The Test Framework exits the batch process after the run; `-quit` can end the initial import before test dispatch.

The repeatable M7 player-build gate is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'D:\Projects\SimpleBlend' `
  -executeMethod 'SimpleBlend.Editor.Tests.LayerBlendPlayerBuildFixture.Build' `
  -logFile 'D:\Projects\SimpleBlend\TestResults\SimpleBlend-M7-PlayerBuild.log'
```

The fixture temporarily creates retained Resources materials for both public shaders, builds Windows x64, and removes those temporary assets. Reference captures remain an interactive acceptance step.

## Human review checkpoints

User judgment is required at these points:

1. Checkpoint A: package naming and public presentation.
2. Checkpoint C: first-stroke feel and projected brush feedback.
3. Checkpoint E: overlay workflow, defaults, and erase/smooth expectations.
4. SHD-001: exact channel-to-layer convention.
5. TERR-001: terrain feature limitations and chosen contract.
6. Checkpoint I: sample quality and 1.0 release acceptance.

## Next session starting point

The M7 automated implementation gate is complete. Shader Graph parity is proposed in PR #14, terrain binding in PR #15 and terrain shading in PR #16. Teaching samples are in PR #19. Issue #6's guide coverage and pinned Git installation checks are prepared, with evidence in `docs/checkpoints/checkpoint-i-user-guide.md`. Next prepare issue #9's version-validation automation while obtaining owner-run visual, first-use and removal evidence. Integrate only reviewed implementation branches. Neither open pull requests nor automated checks establish release acceptance.

During Batch H, preserve these established boundaries:

- HLSL and Shader Graph variants share one channel and mask convention,
- zero-painted layers cannot win through height alone,
- terrain integration uses only public APIs and never mutates terrain or imported mesh sources,
- the M7 deferred visual checks remain visible acceptance work,
- `Color32[]` remains the only authoritative editable paint state.
