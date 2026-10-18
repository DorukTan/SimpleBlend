using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal sealed class VertexPaintStrokeSession
    {
        private const string UndoName = "Paint SimpleBlend Vertex Colors";
        private readonly List<int> _candidates = new List<int>();
        private readonly List<Vector3> _samplePoints = new List<Vector3>();
        private readonly VertexStrokeSampler _strokeSampler = new VertexStrokeSampler();
        private VertexPaintData _data;
        private Color32[] _strokeBaseline;
        private float[] _strongestStrokeWeights;
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
            var meshOwner = VertexPaintDataAuthoring.GetRendererMeshOwner(data);
            if (meshOwner == null)
            {
                Undo.RegisterCompleteObjectUndo(data, UndoName);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { data, meshOwner }, UndoName);
            }

            var storageResult = VertexPaintDataAuthoring.EnsureCurrentStorageForPaint(
                data,
                null,
                out var storageChanged);
            if (!storageResult.IsValid)
            {
                Undo.CollapseUndoOperations(_undoGroup);
                _undoGroup = -1;
                return false;
            }

            var ownershipResult = VertexPaintDataAuthoring.EnsureOwnedFallbackForPaint(
                data,
                out var ownershipChanged);
            if (!ownershipResult.IsValid)
            {
                Undo.CollapseUndoOperations(_undoGroup);
                _undoGroup = -1;
                return false;
            }

            _data = data;
            _strokeBaseline = (Color32[])data.SerializedColors.Clone();
            _strongestStrokeWeights = new float[data.ColorCount];
            _strokeSampler.Reset();
            HasChanges = storageChanged || ownershipChanged;
            return true;
        }

        internal int Paint(MeshPaintCache cache, VertexBrushCommand command)
        {
            if (!IsActive
                || cache?.Snapshot == null
                || (command.Mode != VertexPaintBrushMode.Paint
                    && command.Mode != VertexPaintBrushMode.Erase
                    && command.Mode != VertexPaintBrushMode.Smooth))
            {
                return 0;
            }

            _strokeSampler.Append(command.Center, command.Radius, _samplePoints);
            var changedCount = 0;
            for (var sampleIndex = 0; sampleIndex < _samplePoints.Count; sampleIndex++)
            {
                var sampleCommand = command.WithCenter(_samplePoints[sampleIndex]);
                cache.QueryVertices(sampleCommand.Center, sampleCommand.Radius, _candidates);
                changedCount += VertexPaintKernel.Apply(
                    sampleCommand,
                    cache.WorldVertices,
                    cache.WorldNormals,
                    _candidates,
                    cache.Snapshot.Topology,
                    _data.SerializedColors,
                    false,
                    command.Accumulate ? null : _strokeBaseline,
                    command.Accumulate ? null : _strongestStrokeWeights);
            }

            if (changedCount == 0)
            {
                return 0;
            }

            ApplyChanges();
            return changedCount;
        }

        internal int Fill(MeshPaintCache cache, VertexBrushCommand command, int hitSubmeshIndex)
        {
            if (!IsActive || cache?.Snapshot == null || command.Mode != VertexPaintBrushMode.Fill)
            {
                return 0;
            }

            var topology = cache.Snapshot.Topology;
            var candidates = hitSubmeshIndex < 0
                ? topology.AllVertices
                : topology.GetSubmeshVertices(hitSubmeshIndex);
            var changedCount = VertexPaintKernel.Apply(
                command,
                cache.WorldVertices,
                cache.WorldNormals,
                candidates,
                topology,
                _data.SerializedColors,
                true);
            if (changedCount == 0)
            {
                return 0;
            }

            ApplyChanges();
            return changedCount;
        }

        private void ApplyChanges()
        {
            HasChanges = true;
            VertexPaintDataAuthoring.Apply(_data);
            EditorUtility.SetDirty(_data);
            if (_data.FallbackMesh != null)
            {
                EditorUtility.SetDirty(_data.FallbackMesh);
            }

            SceneView.RepaintAll();
        }

        internal void End()
        {
            if (!IsActive)
            {
                ResetState();
                return;
            }

            if (HasChanges)
            {
                EditorUtility.SetDirty(_data);
                VertexPaintDataAuthoring.RecordPrefabModifications(_data);
                if (_data.FallbackMesh != null)
                {
                    EditorUtility.SetDirty(_data.FallbackMesh);
                }
            }

            Undo.CollapseUndoOperations(_undoGroup);
            ResetState();
        }

        private void ResetState()
        {
            _data = null;
            _strokeBaseline = null;
            _strongestStrokeWeights = null;
            _strokeSampler.Reset();
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
