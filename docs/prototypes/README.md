# Workspace interaction review

Open `workspace.html` in a browser. There are no external resources or build dependencies. This is a browser-only interaction prototype for issue #24, not the shipping Unity UI. It cannot read, modify, save or export Unity assets. The demonstration canvas is a front projection, not a renderer or brush-engine accuracy fixture.

For a local browser preview, run `node tools/Serve-WorkspacePrototype.cjs` from the repository root and open the printed loopback URL. The server exposes only the three prototype files, not the project directory. Stop it with Ctrl+C when the review is finished.

## Try the first lesson

1. Select Lesson Wall, then Prepare paint data.
2. Open Material setup and create/assign the lesson material.
3. Choose Moss, adjust radius/strength and activate painting.
4. Drag across the canvas or use Sample stroke with the keyboard. Undo and Redo each operate on a complete gesture.
5. Save demo state marks this browser session clean; it does not save a file. In Unity the corresponding operation is native scene/prefab saving, followed by reopening to verify persistence.

Direct RGBA does not require the demonstration material. Scene-tool activation is explicit. Selection lock prevents switching while active. Escape cancels a preview/gesture and deactivates painting. Ctrl/Cmd+Z and Ctrl/Cmd+Y work outside text/select fields; native shortcut bindings remain proposals until #26 validates conflicts.

Raw channel choices change illustrative stroke hues only, not actual encoded RGBA data. Material-section and active-channel preview controls are explicitly unavailable layout proposals. The sparse-cube state offers both a subdivided copy and keeping existing coarse vertices. The canvas is not evidence of vertex-level accuracy for either choice.

Use Test state to inspect empty, setup, sparse, ready, mixed, unsupported, topology mismatch and write-failure flows. Resetting dirty demonstration paint requires confirmation. This is a demo reset safeguard, not a proposed modal on every native Unity selection change.

Advanced brush settings contains selection lock. Procedural treatment demonstrates preview/apply/cancel only; the applied illustrative treatment matches its preview, but is not the planned noise algorithm. Create/Sculpt/Output describe future operations rather than pretending their engines exist. Baked-output confirmation exports nothing.

## Owner review checklist

- At 320- and 440-point dock widths, in both themes, can you identify the target, active task, selected layer and next action?
- Does material setup make sense without knowing raw channel names?
- Is the sparse-cube explanation actionable without becoming a barrier to coarse painting?
- Are Direct RGBA, material sections and advanced options discoverable without crowding the basic lesson?
- Is preview versus applied/dirty state unambiguous? Can you cancel without losing valid paint?
- Do topology mismatch and output errors explain what remains safe and how to recover?
- Does the arrangement fit the way you expect to dock and use the tool in Unity?

Record feedback and revisions in `../authoring-workflow.md`. Approval of the earlier Unity-native style direction is not approval of this specific screen. Keep #24 open until actual screen review is recorded.

## Automated checks

With Node.js and Playwright available, run `node tools/Test-WorkspacePrototype.cjs` from the repository root. The script uses an installed browser, exercises interaction boundaries and writes review screenshots only under ignored `TestResults/WorkspacePrototype/`. It does not automate the user's browser or Unity editor.
