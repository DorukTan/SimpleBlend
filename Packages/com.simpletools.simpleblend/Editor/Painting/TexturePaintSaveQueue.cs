using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    [InitializeOnLoad]
    internal static class TexturePaintSaveQueue
    {
        private static readonly HashSet<TexturePaintAsset> Pending = new HashSet<TexturePaintAsset>();
        private static double _lastChange;

        static TexturePaintSaveQueue()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
            EditorApplication.quitting += Flush;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Flush(); };
        }

        internal static void Enqueue(TexturePaintAsset paint)
        {
            if (paint == null || !AssetDatabase.Contains(paint)) return;
            Pending.Add(paint);
            _lastChange = EditorApplication.timeSinceStartup;
        }

        private static void Update()
        {
            if (Pending.Count == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating
                || GUIUtility.hotControl != 0 || EditorApplication.timeSinceStartup - _lastChange < 0.5) return;
            Flush();
        }

        internal static void Flush()
        {
            var paints = new List<TexturePaintAsset>(Pending);
            Pending.Clear();
            foreach (var paint in paints)
            {
                if (paint == null || !AssetDatabase.Contains(paint)) continue;
                try { AssetDatabase.SaveAssetIfDirty(paint); }
                catch (System.Exception exception)
                {
                    // Keep failed saves queued for an explicit retry or the next editor save opportunity.
                    Pending.Add(paint);
                    _lastChange = EditorApplication.timeSinceStartup + 5;
                    Debug.LogError("SimpleBlend could not save " + AssetDatabase.GetAssetPath(paint) + ": " + exception.Message, paint);
                }
            }
        }
    }
}
