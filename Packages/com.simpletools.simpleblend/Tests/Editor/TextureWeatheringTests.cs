using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TextureWeatheringTests
    {
        private TestAssetScope _assets;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope(); _scene = EditorSceneManager.NewPreviewScene(); Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop(); TexturePaintSaveQueue.Flush(); Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); _assets.Dispose();
        }

        [TestCase(TextureWeatheringSource.Noise)]
        [TestCase(TextureWeatheringSource.MeshBorders)]
        [TestCase(TextureWeatheringSource.UvSeams)]
        public void PreviewRegenerationAndCancelLeavePaintFilesDirtyFlagsAndUndoUnchanged(TextureWeatheringSource source)
        {
            var target = Target(); var paint = target.Paint;
            AssetDatabase.SaveAssetIfDirty(paint); Undo.ClearAll();
            var path = AssetDatabase.GetAssetPath(paint); var bytes = System.IO.File.ReadAllBytes(path);
            var pixels = (Color32[])paint.Pixels.Clone(); var mask = paint.Mask.GetPixels32();
            var group = Undo.GetCurrentGroup(); var dirty = EditorUtility.IsDirty(paint); var maskDirty = EditorUtility.IsDirty(paint.Mask);
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                var settings = new TextureWeatheringSettings { Source = source, Coverage = 1, Width = 0.2f };
                preview.Generate(settings); var first = preview.CopyPixels(); preview.Generate(settings);
                Assert.That(preview.CopyPixels(), Is.EqualTo(first));
                preview.Cancel(); Assert.Throws<InvalidOperationException>(() => preview.Apply());
            }
            Assert.That(paint.Pixels, Is.EqualTo(pixels)); Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(mask));
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(EditorUtility.IsDirty(paint), Is.EqualTo(dirty)); Assert.That(EditorUtility.IsDirty(paint.Mask), Is.EqualTo(maskDirty));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
            Undo.PerformUndo(); Assert.That(paint.Pixels, Is.EqualTo(pixels));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ApplyMatchesDetachedPreviewPreservesReservedChannelsAndIsOneUndo(bool erase)
        {
            var target = Target(); var paint = target.Paint;
            for (var i = 0; i < paint.Pixels.Length; i++) paint.Pixels[i] = new Color32(erase ? (byte)180 : (byte)0, 0,
                (byte)(83 + i % 97), (byte)(29 + i % 131));
            paint.CommitPixels(); Undo.ClearAll(); var before = (Color32[])paint.Pixels.Clone();
            using (var preview = new TextureWeatheringPreview(target, 1, erase))
            {
                preview.Generate(new TextureWeatheringSettings { Coverage = 1, Strength = 0.4f });
                var expected = preview.CopyPixels(); Assert.That(preview.ChangedTexels, Is.GreaterThan(0));
                Assert.That(preview.Apply(), Is.GreaterThan(0)); Assert.That(paint.Pixels, Is.EqualTo(expected));
                for (var i = 0; i < before.Length; i++)
                { Assert.That(paint.Pixels[i].a, Is.EqualTo(before[i].a)); Assert.That(paint.Pixels[i].b, Is.EqualTo(before[i].b)); }
                Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(expected));
                Assert.Throws<InvalidOperationException>(() => preview.Apply());
                Undo.PerformUndo(); Assert.That(paint.Pixels, Is.EqualTo(before));
                Undo.PerformRedo(); Assert.That(paint.Pixels, Is.EqualTo(expected));
            }
        }

        [Test]
        public void NoiseIsRepeatableAcrossAssetReloadAndSeedDoesNotTouchGlobalRandom()
        {
            var target = Target(); var settings = new TextureWeatheringSettings { Seed = 137, NoiseScale = 7, Softness = 0.1f };
            var state = UnityEngine.Random.state;
            using (var first = new TextureWeatheringPreview(target, 1, false))
            {
                first.Generate(settings); var expected = first.CopyPixels();
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(target.Paint), ImportAssetOptions.ForceSynchronousImport);
                using (var second = new TextureWeatheringPreview(target, 1, false))
                {
                    settings = JsonUtility.FromJson<TextureWeatheringSettings>(JsonUtility.ToJson(settings));
                    second.Generate(settings); Assert.That(second.CopyPixels(), Is.EqualTo(expected));
                    settings.Seed = TextureWeatheringMask.NextSeed(settings.Seed); second.Generate(settings);
                    Assert.That(second.CopyPixels(), Is.Not.EqualTo(expected));
                }
            }
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(state)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PhysicalBordersAndOriginalUvSeamsAreDifferentSources(bool materialSplit)
        {
            var target = Target(materialSplit); var mesh = target.Paint.Mesh;
            var borders = new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.MeshBorders);
            var seams = new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.UvSeams);
            Assert.That(seams.Count, Is.EqualTo(1));
            Assert.That(borders.Count, Is.EqualTo(materialSplit ? 2 : 4));
            Assert.That(seams.Distance(Vector3.zero, 1), Is.LessThan(0.00001f));
            Assert.That(borders.Distance(Vector3.zero, 1), Is.EqualTo(0.5f).Within(0.00001f));
            var uv = mesh.uv;
            var positions = mesh.vertices;
            for (var i = 0; i < uv.Length; i++) uv[i] = new Vector2(positions[i].x, positions[i].y);
            mesh.uv = uv;
            Assert.That(new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.UvSeams).Count, Is.Zero);
            Assert.That(new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.MeshBorders).Count, Is.EqualTo(materialSplit ? 2 : 4));
        }

        [Test]
        public void ClosedCubeHasNoPhysicalBordersDespiteSplitVertexNormalsAndGeneratedAtlas()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cube, _scene);
            var target = TexturePaintSetup.CreateAutomatic(cube, resolution: 256, directory: _assets.AssetPath + "/Cube");
            Assert.That(new TextureWeatheringEdges(target.Paint.Mesh, 0, TextureWeatheringSource.MeshBorders).Count, Is.Zero);
        }

        [Test]
        public void ApplyExcludesUnselectedSectionAndUpdatesOnlyChangedSourceGutters()
        {
            var target = Target(true); var paint = target.Paint; var before = (Color32[])paint.Pixels.Clone();
            var atlas = new TexturePaintAtlas(paint.Mesh, 0, 256);
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                preview.Generate(new TextureWeatheringSettings { Coverage = 1 }); preview.Apply();
            }
            for (var i = 0; i < before.Length; i++)
            {
                if (atlas.Coverage[i] < 0) Assert.That(paint.Pixels[i], Is.EqualTo(before[i]));
                else Assert.That(paint.Pixels[i], Is.EqualTo(paint.Pixels[atlas.Coverage[i]]));
            }
        }

        [TestCase("paint")]
        [TestCase("surface")]
        [TestCase("geometry")]
        [TestCase("uv")]
        [TestCase("atlas")]
        [TestCase("topology")]
        [TestCase("dimensions")]
        [TestCase("material")]
        [TestCase("target")]
        public void StalePreviewCannotOverwriteLaterEdits(string edit)
        {
            var target = Target(); var paint = target.Paint;
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                preview.Generate(new TextureWeatheringSettings { Coverage = 1 });
                if (edit == "paint") { paint.Pixels[0] = new Color32(12, 0, 0, 0); paint.CommitPixels(); }
                else if (edit == "surface") TextureSurfaceAuthoring.Add(paint);
                else if (edit == "geometry") { var points = paint.Mesh.vertices; points[0] += Vector3.right * 0.01f; paint.Mesh.vertices = points; }
                else if (edit == "uv") { var uv = paint.Mesh.uv; uv[0] += Vector2.one; paint.Mesh.uv = uv; }
                else if (edit == "atlas") { var uv = paint.Mesh.uv4; uv[0] += Vector2.one; paint.Mesh.uv4 = uv; }
                else if (edit == "topology") { var triangles = paint.Mesh.triangles; var swap = triangles[0]; triangles[0] = triangles[1]; triangles[1] = swap; paint.Mesh.triangles = triangles; }
                else if (edit == "dimensions") paint.Mask.Reinitialize(128, 128);
                else if (edit == "material") paint.Material.SetColor("_Layer1Color", Color.red);
                else target.gameObject.SetActive(false);
                var pixels = (Color32[])paint.Pixels.Clone(); var group = Undo.GetCurrentGroup();
                Assert.Throws<InvalidOperationException>(() => preview.Apply());
                Assert.That(paint.Pixels, Is.EqualTo(pixels)); Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
                Assert.Throws<InvalidOperationException>(() => preview.Generate(new TextureWeatheringSettings()));
            }
        }

        [Test]
        public void CancellationKeepsPreviousPreviewAndNeverWritesPaint()
        {
            var target = Target(); var baseline = (Color32[])target.Paint.Pixels.Clone();
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                preview.Generate(new TextureWeatheringSettings()); var first = preview.CopyPixels();
                var polls = 0;
                Assert.Throws<OperationCanceledException>(() => preview.Generate(new TextureWeatheringSettings { Source = TextureWeatheringSource.MeshBorders }, () => ++polls > 3));
                Assert.That(preview.CopyPixels(), Is.EqualTo(first)); Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
            }
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void ZeroCoverageOrStrengthIsAnExactNoOp(float coverage, float strength)
        {
            var target = Target(); target.Paint.Pixels[0] = new Color32(0, 0, 0, 73); target.Paint.CommitPixels();
            var baseline = (Color32[])target.Paint.Pixels.Clone(); var group = Undo.GetCurrentGroup();
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                preview.Generate(new TextureWeatheringSettings { Coverage = coverage, Strength = strength });
                Assert.That(preview.CopyPixels(), Is.EqualTo(baseline)); Assert.That(preview.Apply(), Is.Zero);
            }
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group)); Assert.That(target.Paint.Pixels, Is.EqualTo(baseline));
        }

        [Test]
        public void SettingsRejectNonfiniteOrOutOfRangeValuesAndDistanceFeatherHasKnownWeights()
        {
            Assert.Throws<ArgumentException>(() => new TextureWeatheringSettings { NoiseScale = float.NaN }.Snapshot());
            Assert.Throws<ArgumentException>(() => new TextureWeatheringSettings { Width = -1 }.Snapshot());
            Assert.Throws<ArgumentException>(() => new TextureWeatheringSettings { Coverage = 2 }.Snapshot());
            Assert.Throws<ArgumentException>(() => TextureWeatheringMask.Noise(new Vector3(float.PositiveInfinity, 0, 0), 1));
            var settings = new TextureWeatheringSettings { Source = TextureWeatheringSource.MeshBorders, Coverage = 1, Width = 1, Softness = 0.5f };
            Assert.That(TextureWeatheringMask.Evaluate(Vector3.zero, settings, 0.5f), Is.EqualTo(1));
            Assert.That(TextureWeatheringMask.Evaluate(Vector3.zero, settings, 0.75f), Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(TextureWeatheringMask.Evaluate(Vector3.zero, settings, 1), Is.Zero);
            settings.Width = 0; Assert.That(TextureWeatheringMask.Evaluate(Vector3.zero, settings, 0), Is.Zero);
        }

        [Test]
        public void EdgeBoundsQueriesMatchBruteForceSegmentDistances()
        {
            var mesh = new Mesh();
            try
            {
                const int sides = 32;
                var vertices = new Vector3[sides + 1]; var triangles = new int[sides * 3];
                for (var i = 0; i < sides; i++)
                {
                    vertices[i] = new Vector3(Mathf.Cos(i * Mathf.PI * 2 / sides), Mathf.Sin(i * Mathf.PI * 2 / sides), 0);
                    triangles[i * 3] = sides; triangles[i * 3 + 1] = i; triangles[i * 3 + 2] = (i + 1) % sides;
                }
                mesh.vertices = vertices; mesh.triangles = triangles;
                var edges = new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.MeshBorders);
                Assert.That(edges.Count, Is.EqualTo(sides));
                for (var i = 0; i < 100; i++)
                {
                    var point = new Vector3((i * 37 % 101) * 0.04f - 2, (i * 17 % 97) * 0.04f - 2, 0.1f);
                    var expected = 1.3f;
                    for (var edge = 0; edge < sides; edge++)
                    {
                        var a = vertices[edge]; var delta = vertices[(edge + 1) % sides] - a;
                        var closest = a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude);
                        expected = Mathf.Min(expected, Vector3.Distance(point, closest));
                    }
                    Assert.That(edges.Distance(point, 1.3f), Is.EqualTo(expected).Within(0.00001f));
                }
                Assert.Throws<ArgumentException>(() => new TextureWeatheringEdges(mesh, 0, TextureWeatheringSource.UvSeams));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DetachedCopiesCannotChangePreviewAndAppliedPixelsSurviveReimport()
        {
            var target = Target(); Color32[] expected;
            using (var preview = new TextureWeatheringPreview(target, 1, false))
            {
                preview.Generate(new TextureWeatheringSettings { Coverage = 1 }); expected = preview.CopyPixels();
                var editedCopy = preview.CopyPixels(); editedCopy[0] = new Color32(0, 0, 0, 123);
                Assert.That(preview.CopyPixels(), Is.EqualTo(expected)); preview.Apply();
            }
            TexturePaintSaveQueue.Flush();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(target.Paint), ImportAssetOptions.ForceSynchronousImport);
            Assert.That(target.Paint.Pixels, Is.EqualTo(expected)); Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(expected));
        }

        private TexturePaintTarget Target(bool splitSections = false)
        {
            var mesh = new Mesh { name = "Seamed square" };
            mesh.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0),
                new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,0.5f,0), new Vector3(0.5f,-0.5f,0) };
            mesh.normals = Enumerable.Repeat(Vector3.back, 6).ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right, new Vector2(2, 1), new Vector2(2, 0) };
            if (splitSections) { mesh.subMeshCount = 2; mesh.SetTriangles(new[] { 0, 1, 2 }, 0); mesh.SetTriangles(new[] { 3, 4, 5 }, 1); }
            else mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/Source.asset");
            var source = new GameObject("Square", typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(source, _scene);
            source.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, _assets.AssetPath + "/Source.mat");
            source.GetComponent<MeshRenderer>().sharedMaterials = splitSections ? new[] { material, material } : new[] { material };
            return TexturePaintSetup.CreateAutomatic(source, resolution: 256, directory: _assets.AssetPath + "/Paints");
        }
    }
}
