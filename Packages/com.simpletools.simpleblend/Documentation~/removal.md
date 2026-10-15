# Clear paint and remove the package

Back up scenes, prefabs, materials, paint data and generated meshes before removal. These operations have different consequences; do not treat disabling a component as a complete uninstall.

## Clear one renderer

While SimpleBlend is installed, use **Clear Paint Data** in the VertexPaintData Inspector and confirm the dialog. It removes serialized paint from that component, detaches its owned transient stream, and restores the source mesh if the renderer still references that component's owned fallback. It does not delete imported sources or generated mesh assets.

Clear is undoable in the editor. Reinitializing afterwards creates a new editable paint state; it is not a topology-remapping recovery operation. Save only after confirming the intended state.

Disabling or deleting the component detaches the owned transient stream, but does **not** perform the full clear/source-restoration workflow for clone mode. Use Clear first if you want the original source assignment back.

## Before uninstalling

1. Decide whether to discard paint or preserve it in project-owned assets. Do this before removing the scripts.
2. For discard, clear each affected scene/prefab target while the package is installed. Review Prefab Mode and instance overrides separately.
3. Remove cleared VertexPaintData components from the affected scenes and prefab assets, then save those changes.
4. Replace every material that uses a SimpleBlend shader, including imported sample and terrain materials.
5. Check remaining references to package graphs, shaders, scripts and sample resources.
6. Remove the Git/local package with Package Manager. For an embedded development package, remove its package folder only after backup and reference cleanup; do not delete the whole project's Packages directory.
7. Reopen affected scenes/prefabs and check the Console and rendering.

## Material behavior is separate from mesh safety

An unrelated project/base material is not changed by painting or clearing; it can still render the restored source mesh after package removal. SimpleBlend does not remember a previous material assignment and does not automatically switch a blend material back to some earlier material.

A material using `SimpleBlend/Layer Blend 3`, `Layer Blend 4`, `Terrain Blend`, or a removed package graph loses its shader when that shader is uninstalled. Missing/pink rendering in that case is expected unless you assign a replacement. Source-mesh preservation is not a promise that removed shader appearances survive.

## Keep paint without the package

There is no one-click standalone bake/export command in the current Inspector. Transient Additional Vertex Streams are not persistent export assets; serialized paint still needs its component while that workflow is in use.

Generated clone meshes contain color data, but removing the component can leave an assigned clone and does not provide a standalone replacement shader. If you intend to retain a clone, make a deliberate project-owned copy, validate its saved colors and assignments, and provide a project-owned material/shader that uses them. Do not assume ordinary URP Lit reproduces multi-layer vertex blending.

Use project tooling to create and verify independent mesh/material output before uninstalling. Preserve the original sources and backups until the replacement is proven. The owner-run removal acceptance test is not completed by these instructions.

## Generated assets and recovery

Generated files are retained on Clear/revert; automatic orphan deletion is not implemented. An ownership ID helps diagnose sharing/forking but is not a permission to delete every file in `Assets/SimpleBlendGenerated`. Inspect scene/prefab references before manually removing any asset. Writable source folders can also contain generated clones.

If you removed the package too early, restore the backed-up project and the same package revision before further cleanup. Saving over missing-script/material states can complicate recovery. Imported samples are consumer Assets copies; removing the package does not remove those folders or make their package script/shader references independent.
