using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal sealed class VertexColorPreviewRenderer
    {
        private const string PreviewShaderName = "Hidden/SimpleBlend/Red Channel Preview";
        private MeshPaintSnapshot _snapshot;
        private Mesh _previewMesh;
        private Material _previewMaterial;

        internal bool Draw(MeshPaintCache cache, VertexPaintData data)
        {
            if (cache?.Snapshot == null || data == null || Event.current.type != EventType.Repaint)
            {
                return false;
            }

            if (!EnsureResources(cache.Snapshot))
            {
                return false;
            }

            _previewMesh.SetColors(data.SerializedColors);
            if (!_previewMaterial.SetPass(0))
            {
                return false;
            }

            Graphics.DrawMeshNow(_previewMesh, cache.LocalToWorld);
            return true;
        }

        internal void Dispose()
        {
            if (_previewMesh != null)
            {
                Object.DestroyImmediate(_previewMesh);
                _previewMesh = null;
            }

            if (_previewMaterial != null)
            {
                Object.DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }

            _snapshot = null;
        }

        private bool EnsureResources(MeshPaintSnapshot snapshot)
        {
            if (_previewMaterial == null)
            {
                var shader = Shader.Find(PreviewShaderName);
                if (shader == null)
                {
                    return false;
                }

                _previewMaterial = new Material(shader)
                {
                    name = "SimpleBlend Red Channel Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_previewMesh != null && ReferenceEquals(_snapshot, snapshot))
            {
                return true;
            }

            if (_previewMesh != null)
            {
                Object.DestroyImmediate(_previewMesh);
            }

            _previewMesh = new Mesh
            {
                name = "SimpleBlend Red Channel Preview",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = snapshot.LocalVertices.Length > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16,
                vertices = snapshot.LocalVertices,
                normals = snapshot.LocalNormals,
                triangles = snapshot.Triangles,
                bounds = snapshot.LocalBounds
            };
            _previewMesh.MarkDynamic();
            _snapshot = snapshot;
            return true;
        }
    }
}
