# Scene feedback and keyboard controls

These controls extend the existing brush; they do not introduce a second paint engine or change saved channel values during preview.

## See the material while painting

Choose **Material** in View to paint while seeing the renderer's assigned materials. SimpleBlend draws no replacement vertex-color overlay in this mode. It does not assign, clone, edit or hide those materials. New brush preferences use Material view; older serialized preview indices keep their meaning. Choosing a named surface explicitly switches to Material view.

Red, Green, Blue, Alpha, RGB and Density remain diagnostic overlays. With Submesh Mask enabled, **Preview section only** restricts that overlay to the masked section; otherwise it covers all sections. Material view keeps all actual renderer materials visible. This is not an operation that hides neighboring geometry or splits shared vertex data. A shared vertex can still affect more than one section when painted.

## Lock the paint target

Use **Lock paint target** in the workspace to pin the initialized renderer shown in its header. You can inspect other objects through the Hierarchy while the brush and workspace continue to target the pinned object. The Scene overlay also names the locked target. This lock does not change Unity's selection, initialize another object or write any asset.

**Unlock paint target** stops the SimpleBlend tool and releases the pin. Stopping/deactivating the tool also releases it. The pin is session-only, not saved in preferences, scenes or palettes. If its object disappears, it cannot fall through to another selected object. Unlock or choose/start a valid target again. Disabled/inactive/invalid targets are not paintable. Play-mode transitions release input and the active pin.

The paint tool is a global EditorTool with contextual availability, rather than a selection-bound component tool: its availability and data resolution honor the pin. Without a pin it requires one selected initialized target. Existing component/menu/workspace activation paths remain available. Creating a denser copy explicitly releases the pin before selecting the new object. Closing the settings workspace does not deactivate the separate Scene tool.

## Configure shortcuts

On Windows, open **Edit > Shortcuts** and search **SimpleBlend**. Commands start **unbound**; assign keys in your own shortcut profile. This avoids shipping unchecked bindings over navigation, terrain or other packages. Registration uses Unity's public [Shortcut Manager context API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShortcutManagement.ShortcutManager.RegisterContext.html); no shortcut profile is edited automatically.

Available commands:

- Paint, Erase, Smooth, Fill and Sample.
- Increase/Decrease Brush Size: ×1.1 or ÷1.1, bounded to 0.0001–10,000 world units.
- Increase/Decrease Strength: 0.05 steps within 0–1; stored vertex alpha is unaffected.
- Material View.
- Lock or Unlock Paint Target.
- Stop Painting.
- Temporary Erase (hold/clutch).

Commands require the active SimpleBlend tool in a focused Scene view. Text/UI keyboard focus, Play Mode and Alt navigation are reserved. Discrete changes are ignored during an owned mouse gesture; choose modes or adjust values between strokes. No default activation key or global editing shortcut is installed. Tooltips and native text controls remain the primary discoverable path until binding/focus walkthroughs are observed.

## Temporary erase and gesture ownership

Bind **Temporary Erase**, hold it before starting a Paint drag, then stroke normally. Erase is latched for that complete drag so changing the held key halfway through cannot split capped-stroke semantics. The clutch only overrides Paint drags; Fill, Sample, Smooth and explicit Erase retain their own operations. Brush strength, flow, falloff, masks and target channels still apply. The temporary operation is not written into saved brush preferences.

Each drag remains one Undo in capped and continuous modes. **Escape** finishes the current drag, stops the tool and releases its pin; it does not clear saved paint or roll the drag back. Use Undo to revert it. Alt navigation, focus loss, failed stroke startup, invalidated targets and tool deactivation release owned input. Repainting another Scene view is not a reason to end the focused view's gesture. Only a control owned by this tool is released; unrelated GUI controls are not deliberately cleared.

Native theme/focus, remapped-binding conflicts and multi-view mouse walkthroughs still require observed acceptance. Editor-state/policy tests and CPU preview topology checks are not a native rendering or event-delivery verdict. Pointer-drag size/strength gestures, richer affected-section/result presentation and sculpt input are separate remaining work in the input milestone.

Verification on 2026-10-18: 259 isolated Unity 6000.4.8f1 EditMode tests passed, no failures or skips, including 33 new Scene/input cases. Coverage includes real ToolManager pin/selection/deactivation, destroyed targets and pre-activation transition cleanup, command registration/policy, temporary-erase Undo in both accumulation modes, stable preview indices and captured submesh geometry. Documentation, roadmap and 63 existing icon checks passed. No desktop automation or native visual verdict was used.
