using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class VertexPaintStrokeSession
    {
        private const string UndoName = "Paint SimpleBlend Vertex Colors";
        private readonly List<int> _candidates = new List<int>();
        private VertexPaintData _data;
        private int _undoGroup = -1;

        internal bool IsActive => _data != null;

        internal bool HasChanges { get; private set; }

        internal int UndoGroup => _undoGroup;

        internal bool Begin(VertexPaintData data)
        {
            if (data == null || IsActive)
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            Undo.RegisterCompleteObjectUndo(data, UndoName);
            _data = data;
            HasChanges = false;
            return true;
        }

        internal int Paint(MeshPaintCache cache, RedBrushCommand command)
        {
            if (!IsActive || cache == null)
            {
                return 0;
            }

            cache.QueryVertices(command.Center, command.Radius, _candidates);
            var changedCount = RedVertexPaintKernel.Apply(
                command,
                cache.WorldVertices,
                _candidates,
                _data.SerializedColors);
            if (changedCount == 0)
            {
                return 0;
            }

            HasChanges = true;
            VertexPaintDataAuthoring.Apply(_data);
            EditorUtility.SetDirty(_data);
            if (_data.FallbackMesh != null)
            {
                EditorUtility.SetDirty(_data.FallbackMesh);
            }

            SceneView.RepaintAll();
            return changedCount;
        }

        internal void End()
        {
            if (!IsActive)
            {
                return;
            }

            if (HasChanges)
            {
                EditorUtility.SetDirty(_data);
                PrefabUtility.RecordPrefabInstancePropertyModifications(_data);
                if (_data.FallbackMesh != null)
                {
                    EditorUtility.SetDirty(_data.FallbackMesh);
                }
            }

            Undo.CollapseUndoOperations(_undoGroup);
            _data = null;
            _undoGroup = -1;
            HasChanges = false;
        }
    }

    [InitializeOnLoad]
    internal static class VertexPaintUndoReapply
    {
        static VertexPaintUndoReapply()
        {
            Undo.undoRedoPerformed += ReapplyLoadedPaintData;
        }

        private static void ReapplyLoadedPaintData()
        {
            var paintData = Resources.FindObjectsOfTypeAll<VertexPaintData>();
            for (var index = 0; index < paintData.Length; index++)
            {
                var data = paintData[index];
                if (data != null && data.gameObject.scene.IsValid())
                {
                    VertexPaintDataAuthoring.Apply(data);
                }
            }

            SceneView.RepaintAll();
        }
    }
}
