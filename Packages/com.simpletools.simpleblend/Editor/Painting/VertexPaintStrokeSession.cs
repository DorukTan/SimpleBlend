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
        private readonly HashSet<int> _touchedVertices = new HashSet<int>();
        private VertexPaintTopology _resultTopology;
        private bool _attempted;
        private bool _hadRadiusAttempt;
        private bool _hadFillAttempt;
        private bool _hadCandidates;
        private VertexPaintData _data;
        private Color32[] _strokeBaseline;
        private float[] _strongestStrokeWeights;
        private int _undoGroup = -1;

        internal bool IsActive => _data != null;
        internal VertexPaintData Target => _data;

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
            _touchedVertices.Clear();
            _resultTopology = null;
            _attempted = false;
            _hadRadiusAttempt = _hadFillAttempt = _hadCandidates = false;
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

            _attempted = true;
            _hadRadiusAttempt = true;
            _resultTopology = cache.Snapshot.Topology;
            _strokeSampler.Append(command.Center, command.Radius, _samplePoints);
            var changedCount = 0;
            for (var sampleIndex = 0; sampleIndex < _samplePoints.Count; sampleIndex++)
            {
                var sampleCommand = command.WithCenter(_samplePoints[sampleIndex]);
                cache.QueryVertices(sampleCommand.Center, sampleCommand.Radius, _candidates);
                _hadCandidates |= _candidates.Count > 0;
                changedCount += VertexPaintKernel.Apply(
                    sampleCommand,
                    cache.WorldVertices,
                    cache.WorldNormals,
                    _candidates,
                    cache.Snapshot.Topology,
                    _data.SerializedColors,
                    false,
                    command.Accumulate ? null : _strokeBaseline,
                    command.Accumulate ? null : _strongestStrokeWeights,
                    _touchedVertices);
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
            _attempted = true;
            _hadFillAttempt = true;
            _resultTopology = topology;
            var candidates = hitSubmeshIndex < 0
                ? topology.AllVertices
                : topology.GetSubmeshVertices(hitSubmeshIndex);
            _hadCandidates |= candidates.Count > 0;
            var changedCount = VertexPaintKernel.Apply(
                command,
                cache.WorldVertices,
                cache.WorldNormals,
                candidates,
                topology,
                _data.SerializedColors,
                true,
                changedVertices: _touchedVertices);
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

        internal VertexPaintStrokeResult End()
        {
            if (!IsActive)
            {
                ResetState();
                return null;
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
            var result = _attempted
                ? VertexPaintStrokeResult.Create(_strokeBaseline, _data.SerializedColors, _touchedVertices, _resultTopology,
                    _hadRadiusAttempt && !_hadFillAttempt && !_hadCandidates)
                : null;
            ResetState();
            return result;
        }

        private void ResetState()
        {
            _data = null;
            _strokeBaseline = null;
            _strongestStrokeWeights = null;
            _strokeSampler.Reset();
            _undoGroup = -1;
            HasChanges = false;
            _touchedVertices.Clear();
            _resultTopology = null;
            _attempted = false;
            _hadRadiusAttempt = _hadFillAttempt = _hadCandidates = false;
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
