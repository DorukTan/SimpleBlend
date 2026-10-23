using System;
using UnityEngine;

namespace SimpleBlend
{
    /// <summary>References the paint mesh and owns a blend material and the authoritative linear mask pixels.</summary>
    public sealed class TexturePaintAsset : ScriptableObject
    {
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;
        [SerializeField] private Texture2D _mask;
        [SerializeField] private Color32[] _pixels = Array.Empty<Color32>();
        [SerializeField] private string[] _surfaceNames = Array.Empty<string>();
        [SerializeField] private int _revision;
        [NonSerialized] private int _uploadedRevision = -1;
        [NonSerialized] private Texture2D _uploadedMask;

        public Mesh Mesh => _mesh;
        public Material Material => _material;
        public Texture2D Mask => _mask;
        public int SurfaceCount => _surfaceNames.Length;
        public string SurfaceName(int index) => _surfaceNames[index];
        internal Color32[] Pixels => _pixels;
        internal int WeightChannels => _surfaceNames.Length > 3 ? 3 : 2;

        internal void SetSurfaceNames(string[] names) => _surfaceNames = (string[])names.Clone();

        internal void Configure(Mesh mesh, Material material, Texture2D mask, string[] surfaceNames)
        {
            _mesh = mesh;
            _material = material;
            _mask = mask;
            _surfaceNames = (string[])surfaceNames.Clone();
            _pixels = new Color32[mask.width * mask.height];
            _revision++;
            RebuildMask();
        }

        internal void CommitPixels()
        {
            _revision++;
            RebuildMask();
        }

        internal bool RebuildMask()
        {
            if (_mask == null || !_mask.isReadable || _pixels == null
                || _pixels.Length != _mask.width * _mask.height) return false;
            if (_uploadedRevision == _revision && _uploadedMask == _mask) return false;
            _mask.SetPixels32(_pixels);
            _mask.Apply(false, false);
            _uploadedRevision = _revision;
            _uploadedMask = _mask;
            return true;
        }

        private void OnEnable() => RebuildMask();
    }
}
