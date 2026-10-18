using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleBlend.Editor
{
    internal sealed class SimpleBlendShortcutContext : IShortcutContext
    {
        public bool active
        {
            get
            {
                if (!(EditorWindow.focusedWindow is SceneView scene)) return false;
                var focus = scene.rootVisualElement.focusController?.focusedElement;
                var uiHasFocus = focus != null && !(focus is IMGUIContainer);
                return SimpleBlendPaintInput.CanDispatch(true,
                    EditorGUIUtility.editingTextField || GUIUtility.keyboardControl != 0 || uiHasFocus,
                    EditorApplication.isPlayingOrWillChangePlaymode,
                    ToolManager.activeToolType == typeof(SimpleBlendPaintTool), false, Event.current?.alt ?? false);
            }
        }
    }

    [InitializeOnLoad]
    internal static class SimpleBlendPaintInput
    {
        private static readonly SimpleBlendShortcutContext Context = new SimpleBlendShortcutContext();
        internal static bool EraseHeld { get; private set; }

        static SimpleBlendPaintInput()
        {
            EditorApplication.delayCall += Register;
            AssemblyReloadEvents.beforeAssemblyReload += Unregister;
            EditorApplication.update += ReleaseLostClutch;
        }

        private static void Register() => ShortcutManager.RegisterContext(Context);
        private static void Unregister() { ShortcutManager.UnregisterContext(Context); ClearTemporaryErase(); }
        private static void ReleaseLostClutch() { if (EraseHeld && !Context.active) ClearTemporaryErase(); }

        internal static bool CanDispatch(bool sceneFocused, bool textOrUiFocused, bool playing, bool paintToolActive, bool gestureActive, bool navigating = false)
            => sceneFocused && !textOrUiFocused && !playing && paintToolActive && !gestureActive && !navigating;

        private static bool CanAct => Context.active && GUIUtility.hotControl == 0 && !(Event.current?.alt ?? false);
        internal static void SetTemporaryErase(bool held) => EraseHeld = held;
        internal static void ClearTemporaryErase() => EraseHeld = false;
        internal static bool EraseAtStrokeStart(VertexPaintBrushMode mode) => EraseHeld && mode == VertexPaintBrushMode.Paint;

        internal static void AdjustRadius(SimpleBlendBrushSettings settings, bool increase)
        {
            var radius = settings.Radius;
            if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 1.25f;
            settings.Radius = Mathf.Clamp(radius * (increase ? 1.1f : 1f / 1.1f), 0.0001f, 10000f);
        }

        internal static void AdjustStrength(SimpleBlendBrushSettings settings, bool increase)
        {
            var strength = settings.Strength;
            if (float.IsNaN(strength) || float.IsInfinity(strength)) strength = 1;
            settings.Strength = Mathf.Clamp01(strength + (increase ? 0.05f : -0.05f));
        }

        private static void ChangeMode(VertexPaintBrushMode mode)
        {
            if (!CanAct) return;
            SimpleBlendBrushSettings.instance.Mode = mode;
            SimpleBlendBrushSettings.instance.SaveSettings();
        }

        [Shortcut("SimpleBlend/Paint", typeof(SimpleBlendShortcutContext))]
        private static void Paint() => ChangeMode(VertexPaintBrushMode.Paint);
        [Shortcut("SimpleBlend/Erase", typeof(SimpleBlendShortcutContext))]
        private static void Erase() => ChangeMode(VertexPaintBrushMode.Erase);
        [Shortcut("SimpleBlend/Smooth", typeof(SimpleBlendShortcutContext))]
        private static void Smooth() => ChangeMode(VertexPaintBrushMode.Smooth);
        [Shortcut("SimpleBlend/Fill", typeof(SimpleBlendShortcutContext))]
        private static void Fill() => ChangeMode(VertexPaintBrushMode.Fill);
        [Shortcut("SimpleBlend/Sample", typeof(SimpleBlendShortcutContext))]
        private static void Sample() => ChangeMode(VertexPaintBrushMode.Sample);

        [Shortcut("SimpleBlend/Increase Brush Size", typeof(SimpleBlendShortcutContext))]
        private static void IncreaseRadius() => ChangeRadius(true);
        [Shortcut("SimpleBlend/Decrease Brush Size", typeof(SimpleBlendShortcutContext))]
        private static void DecreaseRadius() => ChangeRadius(false);
        private static void ChangeRadius(bool increase)
        {
            if (!CanAct) return;
            AdjustRadius(SimpleBlendBrushSettings.instance, increase);
            SimpleBlendBrushSettings.instance.SaveSettings();
        }

        [Shortcut("SimpleBlend/Increase Strength", typeof(SimpleBlendShortcutContext))]
        private static void IncreaseStrength() => ChangeStrength(true);
        [Shortcut("SimpleBlend/Decrease Strength", typeof(SimpleBlendShortcutContext))]
        private static void DecreaseStrength() => ChangeStrength(false);
        private static void ChangeStrength(bool increase)
        {
            if (!CanAct) return;
            AdjustStrength(SimpleBlendBrushSettings.instance, increase);
            SimpleBlendBrushSettings.instance.SaveSettings();
        }

        [Shortcut("SimpleBlend/Material View", typeof(SimpleBlendShortcutContext))]
        private static void MaterialView()
        {
            if (!CanAct) return;
            SimpleBlendBrushSettings.instance.PreviewMode = VertexPaintPreviewMode.Material;
            SimpleBlendBrushSettings.instance.SaveSettings();
        }

        [Shortcut("SimpleBlend/Lock or Unlock Paint Target", typeof(SimpleBlendShortcutContext))]
        private static void ToggleLock()
        {
            if (!CanAct) return;
            if (SimpleBlendTargetLock.IsLocked) SimpleBlendPaintTool.UnlockTarget();
            else SimpleBlendTargetLock.Lock(SimpleBlendTargetLock.Resolve(Selection.objects));
        }

        [Shortcut("SimpleBlend/Stop Painting", typeof(SimpleBlendShortcutContext))]
        private static void Stop() { if (CanAct) ToolManager.RestorePreviousTool(); }

        [ClutchShortcut("SimpleBlend/Temporary Erase", typeof(SimpleBlendShortcutContext))]
        private static void TemporaryErase(ShortcutArguments arguments)
        {
            if (arguments.stage == ShortcutStage.End) ClearTemporaryErase();
            else if (CanAct) SetTemporaryErase(true);
        }
    }
}
