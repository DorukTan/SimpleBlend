# Troubleshooting

Start with the Console, VertexPaintData's validation message, **Storage Mode**, **Storage Reason**, and any **Generated Asset** path. Record the package commit, editor/URP versions and reproduction before changing source assets.

| Symptom | Check | Safe next step |
|---|---|---|
| Setup menu unavailable | Selected object has MeshFilter/MeshRenderer or SkinnedMeshRenderer | Select a supported renderer; a collider alone is not a target |
| Component has no source | It was added manually and not initialized | Use Initialize Vertex Painting or the GameObject setup command |
| No brush effect | Active tool, selected target, mode/channel, color, strength/flow and masks | Activate SimpleBlend Paint Tool; test an unmasked Paint stroke in an owned scene |
| Paint has no material-layer effect | Renderer material, URP asset, channel convention, normals/tangents | Assign a shipped blend material; Layer Blend 3 ignores Blue and both shaders ignore Alpha |
| Pink material | Shader exists at the installed revision; active pipeline is URP; compilation is clean | Fix dependency/pipeline errors or choose a present shader; after uninstall, replace removed SimpleBlend shaders |
| Invalid topology/source signature | Source identity, vertex count, submeshes or indexed topology changed | Restore the matching source from backup or intentionally start new paint; do not force old indices onto new topology |
| Source geometry changed but validation is still green | Signature is indexed topology/identity, not a full artistic-correspondence check | Review paint manually after mesh edits; the signature is not a universal vertex-remapping detector |
| Generated mesh write fails | Writable source folder or Assets/SimpleBlendGenerated, file permissions/VCS state | Make the destination writable; preserve referenced generated assets |
| Storage warning after renderer changes | Static batching/GPU Resident Drawer/skinned policy | Use Update Storage Policy when shown; it can migrate the adapter without discarding serialized paint |
| Duplicate/prefab paint affects unexpected target | Prefab Mode versus instance, local overrides, generated clone sharing | Check which asset/instance is being edited; normal painting forks inherited/shared fallback ownership |
| Terrain binder rejects material | Terrain binding v1 shader, editable standalone Assets .mat | Use Terrain Blend and a project-owned material; generic Layer Blend materials are not terrain binders |
| Terrain parameters appear stale | Layer/source replacement, tiling, remaps, position/size or smoothness source changed | Run Bind / Refresh Material; pixel edits to the same control texture stay live |
| Terrain seam mismatch | Layer count, linear blending, tile offsets, supported transform, geometry/normals | Test supported settings and compare matching geometry; custom height/density terrain modes are excluded |
| Samples missing in Package Manager | Installed commit has manifest sample entries | Install a reviewed sample-bearing revision; main and preview can have the same development version |
| Samples have package dependencies after copying | Imported scene still uses VertexPaintData and blend shaders | Keep the package installed or complete deliberate standalone conversion before removal |
| Package download/extraction fails on Windows | Git access, complete error path and path length | Use a shorter test-project path; do not blanket-delete project data or package caches |

**Reapply Paint Data** retries application of currently valid saved data. It does not remap topology, invent missing colors or resolve unsupported terrain features.

For an issue report, include expected/actual behavior, exact versions/commit, renderer/storage mode, source-change history, minimal owned reproduction and relevant Console text. Do not upload private project assets or credentials unnecessarily.
