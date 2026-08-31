using System;
using UnityEngine;

namespace SimpleBlend
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VertexPaintData : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 2;

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private MeshSignature _sourceSignature;
        [SerializeField] private Color32[] _colors = Array.Empty<Color32>();
        [SerializeField] private VertexPaintStorageMode _storageMode;
        [SerializeField] private VertexPaintStorageReason _storageReason;
        [SerializeField] private Mesh _fallbackMesh;
        [SerializeField] private string _fallbackAssetPath = string.Empty;
        [SerializeField] private string _fallbackOwnerId = string.Empty;
        [SerializeField] private VertexPaintValidationFailure _validationFailure;

        [NonSerialized] private Mesh _transientStream;

        public int SchemaVersion => _schemaVersion;

        public Mesh SourceMesh => _sourceMesh;

        public MeshSignature SourceSignature => _sourceSignature;

        public int ColorCount => _colors?.Length ?? 0;

        public VertexPaintStorageMode StorageMode => _storageMode;

        public VertexPaintStorageReason StorageReason => _storageReason;

        public string StorageDescription => VertexPaintText.For(_storageReason);

        public Mesh FallbackMesh => _fallbackMesh;

        public string FallbackAssetPath => _fallbackAssetPath ?? string.Empty;

        public string FallbackOwnerId => _fallbackOwnerId ?? string.Empty;

        public VertexPaintValidationFailure ValidationFailure => _validationFailure;

        public string ValidationMessage => VertexPaintText.For(_validationFailure);

        public Color32 GetColor(int index)
        {
            return _colors[index];
        }

        internal Color32[] SerializedColors => _colors;

        internal Mesh TransientStream
        {
            get => _transientStream;
            set => _transientStream = value;
        }

        internal void Configure(
            Mesh sourceMesh,
            MeshSignature sourceSignature,
            Color32[] colors,
            VertexPaintStorageMode storageMode,
            VertexPaintStorageReason storageReason)
        {
            VertexPaintRuntimeApplicator.PrepareForReconfigure(this);
            _schemaVersion = CurrentSchemaVersion;
            _sourceMesh = sourceMesh;
            _sourceSignature = sourceSignature;
            _colors = colors == null ? Array.Empty<Color32>() : (Color32[])colors.Clone();
            _storageMode = storageMode;
            _storageReason = storageReason;
            _fallbackMesh = null;
            _fallbackAssetPath = string.Empty;
            _fallbackOwnerId = string.Empty;
            _validationFailure = VertexPaintValidationFailure.None;
        }

        internal void ReplaceColors(Color32[] colors)
        {
            _colors = colors == null ? Array.Empty<Color32>() : (Color32[])colors.Clone();
        }

        internal void SetFallbackMesh(Mesh fallbackMesh, string assetPath, string ownerId)
        {
            _fallbackMesh = fallbackMesh;
            _fallbackAssetPath = assetPath ?? string.Empty;
            _fallbackOwnerId = ownerId ?? string.Empty;
        }

        internal void SetStorageDecision(
            VertexPaintStorageMode storageMode,
            VertexPaintStorageReason storageReason)
        {
            _storageMode = storageMode;
            _storageReason = storageReason;
        }

        internal void SetValidation(VertexPaintValidationResult result)
        {
            _validationFailure = result.Failure;
        }

        internal void ResetSerializedState()
        {
            _schemaVersion = CurrentSchemaVersion;
            _sourceMesh = null;
            _sourceSignature = default;
            _colors = Array.Empty<Color32>();
            _storageMode = VertexPaintStorageMode.Unresolved;
            _storageReason = VertexPaintStorageReason.None;
            _fallbackMesh = null;
            _fallbackAssetPath = string.Empty;
            _fallbackOwnerId = string.Empty;
            _validationFailure = VertexPaintValidationFailure.None;
        }

        private void OnEnable()
        {
            VertexPaintRuntimeApplicator.Apply(this);
        }

        private void OnDisable()
        {
            VertexPaintRuntimeApplicator.DetachTransient(this);
        }

        private void OnDestroy()
        {
            VertexPaintRuntimeApplicator.DetachTransient(this);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                VertexPaintRuntimeApplicator.Apply(this);
            }
        }
    }
}
