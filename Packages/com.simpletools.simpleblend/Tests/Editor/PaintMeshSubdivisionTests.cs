using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    public sealed class PaintMeshSubdivisionTests
    {
        [Test]
        public void CubeGainsFaceInteriorVerticesWithoutChangingSourceOrHardEdges()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var source = cube.GetComponent<MeshFilter>().sharedMesh;
            var vertices = source.vertices;
            var triangles = source.triangles;
            Mesh result = null;
            try
            {
                result = PaintMeshSubdivision.Create(source, null, 3);
                Assert.That(result.vertexCount, Is.GreaterThan(source.vertexCount));
                Assert.That(result.triangles.Length, Is.EqualTo(triangles.Length * 64));
                var center = Array.FindIndex(result.vertices, p => (p - new Vector3(0, 0, 0.5f)).sqrMagnitude < 0.00001f);
                Assert.That(center, Is.GreaterThanOrEqualTo(0), "The front face must have a paintable center vertex.");
                Assert.That(result.normals[center], Is.EqualTo(Vector3.forward));
                Assert.That(result.normals.All(n => Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z) == 1f), Is.True);
                Assert.That(source.vertices, Is.EqualTo(vertices));
                Assert.That(source.triangles, Is.EqualTo(triangles));
                Assert.That(result.bounds, Is.EqualTo(source.bounds));

                cube.GetComponent<MeshFilter>().sharedMesh = result;
                Assert.That(VertexPaintSetupUtility.Enable(cube.GetComponent<MeshRenderer>(), false, out var data).IsValid, Is.True);
                var cache = new MeshPaintCache();
                Assert.That(cache.Ensure(data).IsValid, Is.True);
                var session = new VertexPaintStrokeSession();
                var command = new VertexBrushCommand(new Vector3(0, 0, 0.5f), 0.15f, 1, 1,
                    new Color32(255, 0, 0, 255), AnimationCurve.Constant(0, 1, 1),
                    VertexPaintBrushMode.Paint, VertexPaintChannel.Red, true, VertexBrushMask.None);
                Assert.That(session.Begin(data), Is.True);
                Assert.That(session.Paint(cache, command), Is.GreaterThan(0));
                session.End();
                cache.Dispose();
                Assert.That(data.GetColor(center).r, Is.EqualTo(255));
                for (var corner = 0; corner < source.vertexCount; corner++)
                    Assert.That(data.GetColor(corner).r, Is.Zero, "A small face-center stroke must leave corner vertices clear.");
            }
            finally { Undo.ClearAll(); Object.DestroyImmediate(cube); if (result != null) Object.DestroyImmediate(result); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmallNamedSurfaceBrushDistinguishesOrdinaryCubeFromDetailedCopy(bool detailed)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var source = cube.GetComponent<MeshFilter>().sharedMesh;
            var sourceColors = source.colors32;
            var settings = SimpleBlendBrushSettings.instance;
            var beforeSettings = EditorJsonUtility.ToJson(settings);
            Mesh copy = null;
            var cache = new MeshPaintCache();
            using (var assets = new TestAssetScope())
            {
                try
                {
                    if (detailed)
                    {
                        copy = PaintMeshSubdivision.Create(source, null, 3);
                        cube.GetComponent<MeshFilter>().sharedMesh = copy;
                    }
                    var renderer = cube.GetComponent<MeshRenderer>();
                    Assert.That(VertexPaintSetupUtility.Enable(renderer, false, out var data).IsValid, Is.True);
                    var palette = SurfacePaletteAuthoring.Create(assets.AssetPath + "/Brush.asset", new[]
                    {
                        new SurfaceDefinition { Name = "Base" }, new SurfaceDefinition { Name = "Stone" },
                        new SurfaceDefinition { Name = "Moss" }
                    });
                    SurfacePaletteAuthoring.Assign(renderer, 0, palette);
                    SurfacePaletteAuthoring.SelectSurface(palette, 1, 0, settings);
                    settings.Radius = 0.12f;
                    settings.Strength = settings.Flow = 1;
                    settings.NormalMaskEnabled = settings.ThresholdMaskEnabled = false;
                    settings.Falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
                    Assert.That(cache.Ensure(data).IsValid, Is.True);
                    var hit = new Vector3(0, 0, 0.5f);
                    var session = new VertexPaintStrokeSession();
                    session.Begin(data);
                    var changes = session.Paint(cache, settings.CreateCommand(hit));
                    var result = session.End();
                    Assert.That(changes > 0, Is.EqualTo(detailed));
                    Assert.That(result.NoRadiusCoverage, Is.EqualTo(!detailed));
                    for (var corner = 0; corner < source.vertexCount; corner++)
                        Assert.That(data.GetColor(corner).r, Is.Zero, "The small brush must not expand to corner vertices.");
                    settings.Mode = VertexPaintBrushMode.Fill;
                    session.Begin(data);
                    Assert.That(session.Fill(cache, settings.CreateCommand(hit), -1), Is.GreaterThan(0));
                    Assert.That(session.End().NoRadiusCoverage, Is.False);
                    Assert.That(source.colors32, Is.EqualTo(sourceColors));
                }
                finally
                {
                    EditorJsonUtility.FromJsonOverwrite(beforeSettings, settings);
                    cache.Dispose();
                    Undo.ClearAll();
                    Object.DestroyImmediate(cube);
                    if (copy != null) Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void MidpointsShareIndicesAndInterpolatePaintAndUvDimensions()
        {
            var source = TestMeshFactory.CreateQuad();
            source.SetUVs(3, source.vertices.Select(p => new Vector4(p.x, p.y, 3, 4)).ToList());
            var colors = new[] { new Color32(0, 0, 0, 0), new Color32(200, 100, 80, 60), new Color32(40, 80, 120, 160), new Color32(255, 255, 255, 255) };
            Mesh result = null;
            try
            {
                result = PaintMeshSubdivision.Create(source, colors, 1);
                Assert.That(result.vertexCount, Is.EqualTo(9), "The quad diagonal midpoint should be shared.");
                Assert.That(result.GetVertexAttributeDimension(VertexAttribute.TexCoord3), Is.EqualTo(4));
                Assert.That(result.GetVertexAttributeDimension(VertexAttribute.TexCoord0), Is.EqualTo(source.GetVertexAttributeDimension(VertexAttribute.TexCoord0)));
                var a = source.triangles[0]; var b = source.triangles[1];
                var index = Array.FindIndex(result.vertices, p => (p - (source.vertices[a] + source.vertices[b]) * 0.5f).sqrMagnitude < 0.00001f);
                Assert.That(result.colors32[index], Is.EqualTo(Color32.Lerp(colors[a], colors[b], 0.5f)));
                Assert.That(source.colors32, Is.Empty);
                Assert.That(colors[0], Is.EqualTo(new Color32(0, 0, 0, 0)));
            }
            finally { if (result != null) Object.DestroyImmediate(result); Object.DestroyImmediate(source); }
        }

        [Test]
        public void SubmeshesAndTriangleWindingArePreserved()
        {
            var source = TestMeshFactory.CreateQuad();
            var triangles = source.triangles;
            source.subMeshCount = 2;
            source.SetTriangles(triangles.Take(3).ToArray(), 0);
            source.SetTriangles(triangles.Skip(3).ToArray(), 1);
            Mesh result = null;
            try
            {
                result = PaintMeshSubdivision.Create(source, null, 1);
                Assert.That(result.subMeshCount, Is.EqualTo(2));
                var reference = Vector3.Cross(source.vertices[triangles[1]] - source.vertices[triangles[0]], source.vertices[triangles[2]] - source.vertices[triangles[0]]);
                for (var submesh = 0; submesh < 2; submesh++)
                {
                    var output = result.GetTriangles(submesh);
                    Assert.That(output.Length, Is.EqualTo(12));
                    for (var i = 0; i < output.Length; i += 3)
                        Assert.That(Vector3.Dot(reference, Vector3.Cross(result.vertices[output[i + 1]] - result.vertices[output[i]], result.vertices[output[i + 2]] - result.vertices[output[i]])), Is.GreaterThan(0));
                }
            }
            finally { if (result != null) Object.DestroyImmediate(result); Object.DestroyImmediate(source); }
        }

        [TestCase(0)]
        [TestCase(5)]
        public void InvalidLevelsAreRejected(int levels)
        {
            var mesh = TestMeshFactory.CreateQuad();
            try { Assert.Throws<ArgumentOutOfRangeException>(() => PaintMeshSubdivision.Create(mesh, null, levels)); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void UnsupportedTopologyUnreadableMeshesAndWrongPaintAreRejected()
        {
            var mesh = TestMeshFactory.CreateQuad();
            try
            {
                Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.Create(mesh, new Color32[1], 1));
                mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
                Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.Create(mesh, null, 1));
                mesh.UploadMeshData(true);
                Assert.That(PaintMeshSubdivision.UnsupportedReason(mesh), Does.Contain("readable"));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TriangleBudgetIsCheckedBeforeSubdivision()
        {
            var mesh = TestMeshFactory.CreateQuad();
            mesh.triangles = Enumerable.Range(0, 1000).SelectMany(_ => new[] { 0, 1, 2 }).ToArray();
            try { Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.Create(mesh, null, 4)); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SkinningAndBlendShapesAreRejectedWithoutModifyingSource()
        {
            var mesh = TestMeshFactory.CreateQuad(includeSkinning: true);
            try
            {
                Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.Create(mesh, null, 1));
                Assert.That(mesh.blendShapeCount, Is.EqualTo(1));
                Assert.That(mesh.boneWeights, Has.Length.EqualTo(4));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void LargeAllowedResultUses32BitIndices()
        {
            const int side = 65;
            var vertices = new Vector3[side * side];
            for (var y = 0; y < side; y++)
                for (var x = 0; x < side; x++) vertices[y * side + x] = new Vector3(x, 0, y);
            var indices = new System.Collections.Generic.List<int>();
            for (var y = 0; y < side - 1; y++)
                for (var x = 0; x < side - 1; x++)
                {
                    var a = y * side + x;
                    indices.AddRange(new[] { a, a + side, a + 1, a + 1, a + side, a + side + 1 });
                }
            var source = new Mesh { vertices = vertices, triangles = indices.ToArray() };
            Mesh result = null;
            try
            {
                result = PaintMeshSubdivision.Create(source, null, 2);
                Assert.That(result.vertexCount, Is.EqualTo(66049));
                Assert.That(result.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                Assert.That(result.triangles.Max(), Is.LessThan(result.vertexCount));
            }
            finally { if (result != null) Object.DestroyImmediate(result); Object.DestroyImmediate(source); }
        }

        [Test]
        public void CopyPreservesOriginalPaintAndUndoRetainsSavedMesh()
        {
            using (var assets = new TestAssetScope())
            {
                var mesh = TestMeshFactory.CreateQuad();
                AssetDatabase.CreateAsset(mesh, assets.AssetPath + "/Source.asset");
                var original = new GameObject("Original");
                original.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = original.AddComponent<MeshRenderer>();
                GameObject copy = null;
                try
                {
                    Assert.That(VertexPaintSetupUtility.Enable(renderer, false, out var data).IsValid, Is.True);
                    var paint = Enumerable.Repeat(new Color32(120, 40, 80, 200), mesh.vertexCount).ToArray();
                    data.ReplaceColors(paint);
                    VertexPaintDataAuthoring.Apply(data);
                    var sourceMesh = original.GetComponent<MeshFilter>().sharedMesh;
                    var path = assets.AssetPath + "/Subdivided.asset";
                    copy = PaintMeshSubdivision.CreatePaintableCopy(data, 2, path);
                    var copyData = copy.GetComponent<VertexPaintData>();
                    Assert.That(copyData.SourceMesh, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Mesh>(path)));
                    Assert.That(copyData.SerializedColors, Is.All.EqualTo(paint[0]));
                    Assert.That(data.SerializedColors, Is.EqualTo(paint));
                    Assert.That(original.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(sourceMesh));
                    Assert.That(mesh.colors32, Is.Empty);
                    Assert.That(copy.GetComponents<Component>().Length, Is.EqualTo(4));
                    Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.CreatePaintableCopy(data, 1, path));
                    var prefabPath = assets.AssetPath + "/Copy.prefab";
                    PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<VertexPaintData>();
                    Assert.That(saved.SourceMesh, Is.EqualTo(copyData.SourceMesh));
                    Assert.That(saved.SerializedColors, Is.EqualTo(copyData.SerializedColors));
                    Assert.Throws<ArgumentException>(() => PaintMeshSubdivision.CreatePaintableCopy(saved, 1, assets.AssetPath + "/Invalid.asset"));
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(assets.AssetPath + "/Invalid.asset"), Is.Null);
                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();
                    Assert.That(copy == null, Is.True);
                    Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(path), Is.Not.Null);
                    Assert.That(original != null, Is.True);
                    Undo.PerformRedo();
                    copy = GameObject.Find("Original Subdivided");
                    Assert.That(copy, Is.Not.Null);
                    Assert.That(VertexPaintDataAuthoring.Apply(copy.GetComponent<VertexPaintData>()).IsValid, Is.True);
                }
                finally
                {
                    Undo.ClearAll();
                    if (copy != null) Object.DestroyImmediate(copy);
                    Object.DestroyImmediate(original);
                }
            }
        }
    }
}
