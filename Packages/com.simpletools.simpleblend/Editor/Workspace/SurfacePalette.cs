using UnityEngine;

namespace SimpleBlend.Editor
{
    [CreateAssetMenu(menuName = "SimpleBlend/Surface Palette", fileName = "SurfacePalette")]
    public sealed class SurfacePalette : ScriptableObject
    {
        [SerializeField] private Material _material;
        [SerializeField] private string[] _surfaceNames = { "Base", "Surface 1", "Surface 2", "Surface 3" };

        public Material Material => _material;

        internal string Problem
        {
            get
            {
                var count = SurfaceCount;
                if (count == 0) return "Choose a palette with a SimpleBlend Layer Blend 3 or 4 material.";
                if (!_material.HasProperty("_LayerCount")) return "The material is missing the supported layer shader properties.";
                if (!Mathf.Approximately(_material.GetFloat("_LayerCount"), count))
                    return "The material's layer count does not match its shader. Repair a copy in the material Inspector first.";
                var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < count; i++)
                {
                    if (!_material.HasProperty($"_Layer{i}Albedo") || !_material.HasProperty($"_Layer{i}Color"))
                        return "The material is missing the supported layer shader properties.";
                    if (_surfaceNames == null || i >= _surfaceNames.Length || string.IsNullOrWhiteSpace(_surfaceNames[i])
                        || _surfaceNames[i].Trim().Length > 64 || !names.Add(_surfaceNames[i].Trim()))
                        return "Give each palette surface a distinct name of 1–64 characters in its Inspector.";
                }
                return string.Empty;
            }
        }

        public int SurfaceCount
        {
            get
            {
                if (_material == null || _material.shader == null) return 0;
                var shader = _material.shader.name;
                if (shader == SimpleBlendLayerBlendContract.LayerBlend3ShaderName) return 3;
                if (shader == SimpleBlendLayerBlendContract.LayerBlend4ShaderName) return 4;
                return 0;
            }
        }

        public string SurfaceName(int index)
        {
            if (index < 0 || index >= SurfaceCount) throw new System.ArgumentOutOfRangeException(nameof(index));
            return _surfaceNames != null && index < _surfaceNames.Length && !string.IsNullOrWhiteSpace(_surfaceNames[index])
                ? _surfaceNames[index] : index == 0 ? "Base" : $"Surface {index}";
        }

        internal void Configure(Material material, string[] names)
        {
            _material = material;
            _surfaceNames = (string[])names.Clone();
        }
    }
}
