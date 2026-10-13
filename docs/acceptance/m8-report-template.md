# M8 visual acceptance report

Protocol: simpleblend-visual-v1
Status: Not run
Observer: Not recorded
Run date/time/timezone: Not recorded

## Tested configuration

- Repository commit(s), including integration resolution: Not recorded
- Unity / URP / Shader Graph versions: Not recorded
- OS / GPU / driver / graphics API: Not recorded
- Package version: Not recorded
- Editor and player quality level: Not recorded
- URP asset GUID/hash and renderer data GUID/hash: Not recorded
- Effective rendering path: Not recorded
- Capture method and native resolution: Not recorded
- Color space, HDR, render scale, post processing, AA, fog, SSAO, decals: Not recorded
- Ambient/reflection assets and values: Not recorded
- Light rotation/color/intensity and shadows: Not recorded
- Camera target/position/FOV/clipping/background per distance: Not recorded
- Mesh GUID/hash, transform and serialized paint state/hash: Not recorded
- Material GUIDs/hashes and layer controls/textures: Not recorded
- TerrainData/TerrainLayer/control references, remaps, smoothness sources: Not recorded
- Predeclared graph crop and terrain crop validity: Not recorded

## Capture ledger

Create one row for each case × near/medium/far × Forward/Deferred × Editor/player. Add control repeats and Editor/player comparisons. Do not collapse separate states into one result.

| Case / distance / path / environment | A filename + SHA-256 | B filename + SHA-256 | Crop | Comparison JSON | Owner notes | Result |
|---|---|---|---|---|---|---|
| Graph 3 | Pending | Pending | Pending | Pending | Not observed | Not run |
| Graph 4 | Pending | Pending | Pending | Pending | Not observed | Not run |
| Terrain diffuse | Pending | Pending | (944,524,32,32), validity pending | Pending | Not observed | Not run |
| Terrain masked/normal | Pending | Pending | (944,524,32,32), validity pending | Pending | Not observed | Not run |
| Editor/player | Pending | Pending | Pending | Pending | Not observed | Not run |
| Control repeat | Pending | Pending | Pending | Pending | Not observed | Not run |
| Shadows-on seam review | Pending | Pending | Full-frame review | Not a numeric pair | Not observed | Not run |

For each JSON record copy mean error, RMSE, P99, changed-pixel fraction and `passed`. Attach the original JSON and both unchanged captures. Record exact normal/mask textures and non-default remaps for the masked terrain case.

## Source preservation

| Original source path | Before SHA-256 | After SHA-256 | Unchanged? |
|---|---|---|---|
| Mesh / TerrainData / layer / texture / paint source files | Pending | Pending | Not checked |

Any unexpected source change fails the session until explained and rerun. Generated acceptance fixtures must be identified separately.

## Decision and open checks

- [ ] All required rows were observed by the owner.
- [ ] Repeated control captures meet the same limits.
- [ ] Required numeric comparisons pass without retroactive threshold changes.
- [ ] No visible repeatable graph mismatch or terrain seam remains.
- [ ] Original sources are unchanged.
- [ ] Capture settings, versions, hashes and observer are complete.

Decision: Not accepted; evidence pending.

Frame Debugger batching: Not observed.
Baked-lightmap output: Not observed.
Steep-surface projection: Not observed.
Target-hardware performance: Not observed.
Unity 6.3 LTS / 6.6 matrix: Not observed.

Record failures and unsupported checks explicitly; do not infer support from another version's results.
