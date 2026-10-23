using UnityEngine;

namespace SimpleBlend.Editor
{
    internal readonly struct TexturePaintView
    {
        private readonly Vector3 _position, _forward;
        private readonly bool _orthographic;
        private readonly float _near;
        private readonly Matrix4x4 _projection;
        private readonly Matrix4x4 _cameraToWorld;

        internal TexturePaintView(Camera camera)
        {
            _position = camera.transform.position; _forward = camera.transform.forward;
            _orthographic = camera.orthographic; _near = camera.nearClipPlane;
            _projection = camera.projectionMatrix;
            _cameraToWorld = camera.cameraToWorldMatrix;
        }

        internal bool Matches(Camera camera) => camera != null && _position == camera.transform.position
            && _forward == camera.transform.forward && _orthographic == camera.orthographic
            && _near == camera.nearClipPlane && _projection == camera.projectionMatrix
            && _cameraToWorld == camera.cameraToWorldMatrix;

        internal bool RayTo(Vector3 point, out Ray ray, out float distance)
        {
            ray = default; distance = 0;
            var depth = Vector3.Dot(point - _position, _forward);
            if (depth <= _near) return false;
            if (_orthographic)
            {
                distance = depth - _near;
                ray = new Ray(point - _forward * distance, _forward);
            }
            else
            {
                var direction = (point - _position).normalized;
                var origin = _position + direction * (_near / Vector3.Dot(direction, _forward));
                distance = Vector3.Distance(origin, point);
                ray = new Ray(origin, direction);
            }
            return true;
        }
    }

    /// <summary>Lazy visibility shared by repeated dabs; gutters reuse their source texel's result.</summary>
    internal sealed class TexturePaintVisibility
    {
        private readonly TexturePaintAtlas _atlas;
        private readonly TexturePaintView _view;
        private readonly sbyte[] _visible;
        private int _revision;
        internal int QueryCount { get; private set; }

        internal TexturePaintVisibility(TexturePaintAtlas atlas, TexturePaintView view)
        {
            _atlas = atlas; _view = view; _revision = atlas.TransformRevision;
            _visible = new sbyte[atlas.Coverage.Length];
        }

        internal bool Allows(int index)
        {
            if (_revision != _atlas.TransformRevision)
            {
                System.Array.Clear(_visible, 0, _visible.Length);
                _revision = _atlas.TransformRevision;
            }
            var pixel = _atlas.Coverage[_atlas.Pixel(index)];
            if (_visible[pixel] != 0) return _visible[pixel] > 0;
            QueryCount++;
            var allowed = _view.RayTo(_atlas.WorldPosition(index), out var ray, out var distance)
                && !_atlas.Occluded(ray, distance - Mathf.Max(0.00001f, distance * 0.000001f));
            _visible[pixel] = (sbyte)(allowed ? 1 : -1);
            return allowed;
        }

        internal bool Project(Vector3 point, out MeshRaycastHit hit)
        {
            hit = default;
            return _view.RayTo(point, out var ray, out _) && _atlas.Raycast(ray, out hit);
        }
    }
}
