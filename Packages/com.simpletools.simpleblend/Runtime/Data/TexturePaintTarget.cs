using System;
using UnityEngine;

namespace SimpleBlend
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TexturePaintTarget : MonoBehaviour
    {
        [SerializeField] private TexturePaintAsset _paint;
        [SerializeField] private int _materialSection;
        [SerializeField] private TexturePaintAsset[] _sectionPaints = Array.Empty<TexturePaintAsset>();

        public TexturePaintAsset Paint => _paint;
        public int MaterialSection => _materialSection;

        /// <summary>Returns the prepared paint for a material section, or null when it has no paint binding.</summary>
        public TexturePaintAsset PaintForSection(int materialSection)
        {
            if (materialSection < 0) return null;
            if (materialSection == _materialSection) return _paint;
            return _sectionPaints != null && materialSection < _sectionPaints.Length ? _sectionPaints[materialSection] : null;
        }

        internal void BindSection(TexturePaintAsset paint, int materialSection)
        {
            if (_paint == null || _paint.Mesh == null || paint == null || paint.Mesh != _paint.Mesh)
                throw new ArgumentException("Section paint must use this target's existing paint mesh.");
            if (materialSection < 0 || materialSection >= _paint.Mesh.subMeshCount)
                throw new ArgumentOutOfRangeException(nameof(materialSection));
            if (PaintForSection(materialSection) != null)
                throw new InvalidOperationException("This material section already has paint.");
            if (_sectionPaints == null) _sectionPaints = Array.Empty<TexturePaintAsset>();
            if (_sectionPaints.Length <= materialSection) Array.Resize(ref _sectionPaints, materialSection + 1);
            _sectionPaints[materialSection] = paint;
        }

        internal void Configure(TexturePaintAsset paint, int materialSection)
        {
            _paint = paint;
            _materialSection = materialSection;
            _sectionPaints = Array.Empty<TexturePaintAsset>();
        }
    }
}
