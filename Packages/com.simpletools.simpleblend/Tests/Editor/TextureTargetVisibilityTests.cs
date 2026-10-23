using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureTargetVisibilityTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope(); _scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Paint view"); SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            _camera = cameraObject.AddComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -2);
            _camera.nearClipPlane = 0.01f; _camera.enabled = false;
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop(); TexturePaintSaveQueue.Flush(); Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets.Dispose();
        }

        [TestCase(false, 0.02f)]
        [TestCase(true, 0.02f)]
        [TestCase(false, 0.0001f)]
        [TestCase(true, 0.0001f)]
        public void SameFacingHiddenShellReceivesNoBrushPaintAndUndoRestoresTheStroke(bool orthographic, float gap)
        {
            _camera.orthographic = orthographic;
            var target = Target(false, gap); var atlas = Atlas(target);
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            var before = (Color32[])target.Paint.Pixels.Clone();
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.GreaterThan(0));
            stroke.End(); var after = (Color32[])target.Paint.Pixels.Clone();
            var front = 0; var hidden = 0;
            foreach (var pixel in atlas.Pixels)
            {
                if (atlas.Positions[pixel].z > gap * 0.5f)
                { Assert.That(after[pixel].r, Is.Zero); hidden++; }
                else if (after[pixel].r > 0) front++;
            }
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20));
            Undo.PerformUndo(); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(target.Paint.Pixels, Is.EqualTo(after));
            Assert.That(target.GetComponent<Collider>(), Is.Null);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AnotherSectionBlocksLockedPointerHitsAndProjection(bool orthographic, bool reverseOccluder)
        {
            _camera.orthographic = orthographic;
            var target = Target(true, reverseOccluder: reverseOccluder); var atlas = Atlas(target);
            Assert.That(atlas.Raycast(new Ray(_camera.transform.position, Vector3.forward), out _), Is.False);
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            Assert.That(visibility.Project(new Vector3(0, 0, 0.02f), out _), Is.False);
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility);
            Assert.That(stroke.Dab(new Vector3(0, 0, 0.02f), Vector3.back), Is.Zero);
            Assert.That(stroke.End(), Is.Zero);
        }

        [Test]
        public void FillRemainsSectionWideDespiteViewOcclusion()
        {
            var target = Target(false); var atlas = Atlas(target);
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility);
            stroke.Dab(Vector3.zero, Vector3.up, fill: true); stroke.End();
            foreach (var pixel in atlas.Pixels) Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(255));
            Assert.That(visibility.QueryCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VisibilityHandlesMirroredAndNonuniformTransforms(bool mirrored)
        {
            var target = Target(false);
            target.transform.localScale = new Vector3(mirrored ? -2 : 2, 0.5f, 1.5f);
            target.transform.rotation = Quaternion.Euler(0, 15, 0);
            var atlas = Atlas(target); var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            var front = 0; var hidden = 0;
            for (var i = 0; i < atlas.Pixels.Count; i++)
            {
                var point = atlas.Positions[atlas.Pixel(i)];
                if (Mathf.Abs(point.x) > 0.25f || Mathf.Abs(point.y) > 0.25f) continue;
                if (point.z > 0.01f) { Assert.That(visibility.Allows(i), Is.False); hidden++; }
                else { Assert.That(visibility.Allows(i), Is.True); front++; }
            }
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20));
        }

        [Test]
        public void RepeatedDabsAndGuttersReuseVisibilityAndTransformChangesInvalidateIt()
        {
            var target = Target(false); var atlas = Atlas(target);
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            for (var i = 0; i < atlas.Pixels.Count; i++) visibility.Allows(i);
            var firstQueries = visibility.QueryCount;
            Assert.That(firstQueries, Is.EqualTo(atlas.Pixels.Count(pixel => atlas.Coverage[pixel] == pixel)));
            for (var i = 0; i < atlas.Pixels.Count; i++) visibility.Allows(i);
            Assert.That(visibility.QueryCount, Is.EqualTo(firstQueries));
            atlas.SetTransform(Matrix4x4.Translate(Vector3.right * 0.1f));
            visibility.Allows(0); Assert.That(visibility.QueryCount, Is.EqualTo(firstQueries + 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ViewRejectsPointsBehindNearPlaneAndDetectsCameraChanges(bool orthographic)
        {
            _camera.orthographic = orthographic; var view = new TexturePaintView(_camera);
            Assert.That(view.RayTo(_camera.transform.position, out _, out _), Is.False);
            Assert.That(view.RayTo(new Vector3(0.2f, 0.1f, 0), out var ray, out var distance), Is.True);
            Assert.That(Vector3.Distance(ray.GetPoint(distance), new Vector3(0.2f, 0.1f, 0)), Is.LessThan(0.00001f));
            var expectedDirection = orthographic ? Vector3.forward : (new Vector3(0.2f, 0.1f, 0) - _camera.transform.position).normalized;
            Assert.That(Vector3.Distance(ray.direction, expectedDirection), Is.LessThan(0.00001f));
            Assert.That(view.Matches(_camera), Is.True);
            _camera.transform.rotation = Quaternion.Euler(0, 0, 20);
            Assert.That(view.Matches(_camera), Is.False, "Roll must invalidate the captured brush frame too.");
            view = new TexturePaintView(_camera); _camera.nearClipPlane *= 2;
            Assert.That(view.Matches(_camera), Is.False);
        }

        [Test]
        public void RayIndexMatchesReferenceQueriesAcrossSplitNodesAndDistanceLimits()
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var start = vertices.Count;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x, y + 1, 0));
                vertices.Add(new Vector3(x + 1, y, 0));
                normals.AddRange(new[] { Vector3.back, Vector3.back, Vector3.back });
                triangles.AddRange(new[] { start, start + 1, start + 2 });
            }
            var positions = vertices.ToArray(); var directions = normals.ToArray(); var indices = triangles.ToArray();
            var sections = new int[triangles.Count / 3]; var index = new MeshRaycastIndex(positions, directions, indices, sections);
            index.Rebuild();
            for (var i = 0; i < 200; i++)
            {
                var ray = new Ray(new Vector3((i * 37 % 101) * 0.09f - 0.5f, (i * 17 % 97) * 0.09f - 0.5f, -2), Vector3.forward);
                var reference = MeshTriangleRaycaster.TryRaycast(ray, positions, directions, indices, sections, out var expected);
                Assert.That(index.Raycast(ray, out var actual), Is.EqualTo(reference));
                if (!reference) continue;
                Assert.That(actual.TriangleIndex, Is.EqualTo(expected.TriangleIndex));
                Assert.That(actual.Distance, Is.EqualTo(expected.Distance).Within(0.00001f));
                Assert.That(index.Raycast(ray, out _, 1.9f), Is.False);
                Assert.That(index.Raycast(ray, out _, 2.1f), Is.True);
            }
            Assert.That(index.Raycast(new Ray(Vector3.zero, Vector3.zero), out _), Is.False);
        }

        private TexturePaintTarget Target(bool occludingSection, float gap = 0.02f, bool reverseOccluder = false)
        {
            var mesh = new Mesh { name = "Overlapping shells" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0), new Vector3(0.5f,-0.5f,0),
                new Vector3(-0.5f,-0.5f,gap), new Vector3(-0.5f,0.5f,gap), new Vector3(0.5f,0.5f,gap), new Vector3(0.5f,-0.5f,gap)
            };
            mesh.normals = Enumerable.Repeat(Vector3.back, 8).ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            if (occludingSection)
            {
                mesh.subMeshCount = 2; mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 0);
                mesh.SetTriangles(reverseOccluder ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 }, 1);
            }
            else mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/Source.asset");
            var source = new GameObject("Shells", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(source, _scene); source.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, _assets.AssetPath + "/Source.mat");
            source.GetComponent<MeshRenderer>().sharedMaterials = occludingSection ? new[] { material, material } : new[] { material };
            return TexturePaintSetup.CreateAutomatic(source, resolution: 256, directory: _assets.AssetPath + "/Paints");
        }

        private static TexturePaintAtlas Atlas(TexturePaintTarget target)
        {
            var atlas = new TexturePaintAtlas(target.Paint.Mesh, 0, 256);
            atlas.SetTransform(target.transform.localToWorldMatrix); return atlas;
        }

        private static VertexBrushCommand Command() => new VertexBrushCommand(Vector3.zero, 0.2f, 1, 1,
            new Color32(), AnimationCurve.Constant(0, 1, 1), VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, true, VertexBrushMask.None);
    }
}
