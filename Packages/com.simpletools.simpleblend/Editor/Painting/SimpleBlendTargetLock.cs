using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    [InitializeOnLoad]
    internal static class SimpleBlendTargetLock
    {
        static SimpleBlendTargetLock()
        {
            EditorApplication.playModeStateChanged += _ => ReleaseForEditorTransition();
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseForEditorTransition;
        }

        private static VertexPaintData _data;
        internal static bool IsLocked { get; private set; }
        internal static VertexPaintData Data => _data;
        internal static event System.Action Changed;
        internal static void ReleaseForEditorTransition() => Unlock();

        internal static bool Lock(VertexPaintData data)
        {
            if (!PaintWorkspaceTarget.Inspect(new Object[] { data }).CanPaint) return false;
            _data = data;
            IsLocked = true;
            Changed?.Invoke();
            return true;
        }

        internal static void Unlock()
        {
            if (!IsLocked) return;
            IsLocked = false;
            _data = null;
            Changed?.Invoke();
        }

        internal static VertexPaintData Resolve(Object[] selection)
        {
            // A destroyed locked target must never fall through to another selected object.
            if (IsLocked) return _data;
            if (selection == null || selection.Length != 1) return null;
            if (selection[0] == null) return null;
            if (selection[0] is Component component) return component.GetComponent<VertexPaintData>();
            return selection[0] is GameObject gameObject ? gameObject.GetComponent<VertexPaintData>() : null;
        }
    }
}
