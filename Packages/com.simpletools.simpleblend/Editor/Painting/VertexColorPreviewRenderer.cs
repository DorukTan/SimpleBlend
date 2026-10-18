using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleBlend.Editor
{
    internal sealed class VertexColorPreviewRenderer
    {
        internal const string PreviewShaderName = "Hidden/SimpleBlend/Vertex Color Preview";
        private static readonly int PreviewModeId = Shader.PropertyToID("_PreviewMode");
        private MeshPaintSnapshot _snapshot;
        private Mesh _previewMesh;
        private Material _previewMaterial;
        private Vector2[] _densityUv;
        private int _densityTransformRevision = -1;

        internal bool Draw(
            MeshPaintCache cache,
            VertexPaintData data,
            VertexPaintPreviewMode previewMode,
            int section = -1)
        {
            if (previewMode == VertexPaintPreviewMode.Material) return false;
            if (cache?.Snapshot == null || data == null || Event.current.type != EventType.Repaint)
            {
                return false;
            }

            if (section < -1 || section >= cache.Snapshot.Topology.SubmeshCount) return false;

            if (!EnsureResources(cache.Snapshot))
            {
                return false;
            }

            _previewMesh.SetColors(data.SerializedColors);
            UploadDensityIfNeeded(cache);
            _previewMaterial.SetFloat(PreviewModeId, (float)previewMode);
            if (!_previewMaterial.SetPass(0))
            {
                return false;
            }

            Graphics.DrawMeshNow(_previewMesh, cache.LocalToWorld, section);
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
            _densityUv = null;
            _densityTransformRevision = -1;
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
                    name = "SimpleBlend Vertex Color Preview",
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

            _previewMesh = CreatePreviewMesh(snapshot);
            _previewMesh.MarkDynamic();
            _snapshot = snapshot;
            _densityUv = new Vector2[snapshot.LocalVertices.Length];
            _densityTransformRevision = -1;
            return true;
        }

        internal static Mesh CreatePreviewMesh(MeshPaintSnapshot snapshot)
        {
            var mesh = new Mesh
            {
                name = "SimpleBlend Vertex Color Preview",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = snapshot.LocalVertices.Length > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16,
                vertices = snapshot.LocalVertices,
                normals = snapshot.LocalNormals,
                subMeshCount = snapshot.Topology.SubmeshCount,
                bounds = snapshot.LocalBounds
            };
            var sections = new System.Collections.Generic.List<int>[mesh.subMeshCount];
            for (var i = 0; i < sections.Length; i++) sections[i] = new System.Collections.Generic.List<int>();
            for (var triangle = 0; triangle < snapshot.TriangleCount; triangle++)
            {
                var indices = sections[snapshot.TriangleSubmeshIds[triangle]];
                indices.Add(snapshot.Triangles[triangle * 3]);
                indices.Add(snapshot.Triangles[triangle * 3 + 1]);
                indices.Add(snapshot.Triangles[triangle * 3 + 2]);
            }
            for (var i = 0; i < sections.Length; i++) mesh.SetTriangles(sections[i], i, false);
            return mesh;
        }

        private void UploadDensityIfNeeded(MeshPaintCache cache)
        {
            if (_densityTransformRevision == cache.TransformRevision)
            {
                return;
            }

            var density = cache.DensityValues;
            for (var index = 0; index < _densityUv.Length; index++)
            {
                _densityUv[index] = new Vector2(density[index], 0f);
            }

            _previewMesh.uv2 = _densityUv;
            _densityTransformRevision = cache.TransformRevision;
        }
    }
}
