using UnityEngine;

namespace SimpleBlend
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TexturePaintTarget : MonoBehaviour
    {
        [SerializeField] private TexturePaintAsset _paint;
        [SerializeField] private int _materialSection;

        public TexturePaintAsset Paint => _paint;
        public int MaterialSection => _materialSection;

        internal void Configure(TexturePaintAsset paint, int materialSection)
        {
            _paint = paint;
            _materialSection = materialSection;
        }
    }
}
