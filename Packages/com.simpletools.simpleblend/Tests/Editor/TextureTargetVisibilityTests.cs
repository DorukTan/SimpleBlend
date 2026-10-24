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

        [TestCase(false)]
        [TestCase(true)]
        public void FillRemainsSectionWideDespiteViewOcclusion(bool throughPaint)
        {
            var target = Target(false); var atlas = Atlas(target);
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility, throughPaint: throughPaint);
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

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticRoutingNeverSkipsANearerUnpreparedOrLockedSection(bool prepared)
        {
            var target = Target(true);
            if (prepared) TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var ray = new Ray(_camera.transform.position, Vector3.forward);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, true,
                0, 1, null, out var hit, out _, out _), Is.EqualTo(prepared));
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), ray, false,
                0, 1, null, out hit, out _, out _), Is.False);
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ThroughPaintReachesBothShellsWithReservedChannelsAndOneUndo(bool orthographic, bool erase)
        {
            _camera.orthographic = orthographic;
            var target = Target(false); var atlas = Atlas(target);
            var paint = target.Paint;
            for (var i = 0; i < paint.Pixels.Length; i++) paint.Pixels[i] = new Color32(erase ? (byte)180 : (byte)0, 0, 91, 37);
            paint.CommitPixels();
            var before = (Color32[])paint.Pixels.Clone();
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, Command(), visibility: visibility, throughPaint: true);
            Assert.That(stroke.Dab(Vector3.zero, Vector3.back), Is.GreaterThan(0));
            Assert.That(stroke.End(), Is.GreaterThan(0));
            var after = (Color32[])paint.Pixels.Clone();
            var front = 0; var hidden = 0;
            foreach (var pixel in atlas.Pixels)
            {
                Assert.That(after[pixel].b, Is.EqualTo(91)); Assert.That(after[pixel].a, Is.EqualTo(37));
                if (after[pixel].Equals(before[pixel])) continue;
                var point = atlas.Positions[pixel];
                Assert.That(new Vector2(point.x, point.y).magnitude, Is.LessThanOrEqualTo(Command().Radius));
                if (point.z > 0.01f) hidden++; else front++;
            }
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20));
            Assert.That(visibility.QueryCount, Is.Zero, "Through strokes bypass only per-texel visibility queries.");
            for (var i = 0; i < before.Length; i++)
                if (atlas.Coverage[i] < 0) Assert.That(after[i], Is.EqualTo(before[i]));
            Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(after));
            Undo.PerformUndo(); Assert.That(paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(paint.Pixels, Is.EqualTo(after));
            Assert.That(target.GetComponent<Collider>(), Is.Null);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ThroughPaintStillRejectsOutOfDepthOrOpposingFaces(bool orthographic, bool opposing)
        {
            _camera.orthographic = orthographic;
            var target = Target(false, opposing ? 0.02f : 0.2f, reverseHiddenNormals: opposing);
            var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(),
                visibility: new TexturePaintVisibility(atlas, new TexturePaintView(_camera)), throughPaint: true);
            stroke.Dab(Vector3.zero, Vector3.back); stroke.End();
            var front = 0; var hidden = 0;
            foreach (var pixel in atlas.Pixels)
                if (atlas.Positions[pixel].z > 0.01f) { Assert.That(target.Paint.Pixels[pixel].r, Is.Zero); hidden++; }
                else if (target.Paint.Pixels[pixel].r > 0) front++;
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThroughPaintRetainsSectionOwnershipAndNearestHitLock(bool orthographic)
        {
            _camera.orthographic = orthographic;
            var target = Target(true); var secondary = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var before = (Color32[])secondary.Pixels.Clone(); var atlas = Atlas(target);
            var ray = new Ray(_camera.transform.position, Vector3.forward);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, atlas, ray, false, 0, 1, null,
                out var hit, out _, out _), Is.False);
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            Assert.That(visibility.Project(new Vector3(0, 0, 0.02f), out _), Is.False);
            var stroke = new TexturePaintStroke();
            // A direct backend dab can address the hidden section; the Scene pointer still cannot start there.
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility, throughPaint: true);
            Assert.That(stroke.Dab(new Vector3(0, 0, 0.02f), Vector3.back), Is.GreaterThan(0)); stroke.End();
            Assert.That(secondary.Pixels, Is.EqualTo(before));
            Assert.That(target.GetComponent<MeshRenderer>().sharedMaterials[1], Is.EqualTo(secondary.Material));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThroughPaintUsesWorldReachUnderNonuniformAndMirroredTransforms(bool mirrored)
        {
            var target = Target(false); target.transform.localScale = new Vector3(mirrored ? -2 : 2, 0.5f, 1.5f);
            target.transform.rotation = Quaternion.Euler(0, 15, 0);
            var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(),
                visibility: new TexturePaintVisibility(atlas, new TexturePaintView(_camera)), throughPaint: true);
            stroke.Dab(target.transform.TransformPoint(Vector3.zero), target.transform.TransformDirection(Vector3.back)); stroke.End();
            var front = 0; var hidden = 0;
            foreach (var pixel in atlas.Pixels)
                if (target.Paint.Pixels[pixel].r > 0) { if (atlas.Positions[pixel].z > 0.01f) hidden++; else front++; }
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20));
        }

        [Test]
        public void ReusedStrokeReturnsToVisibleDefaultAfterThroughPaint()
        {
            var target = Target(false); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            var visibility = new TexturePaintVisibility(atlas, new TexturePaintView(_camera));
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility, throughPaint: true);
            stroke.Dab(Vector3.zero, Vector3.back); stroke.End(); Undo.PerformUndo();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: visibility);
            stroke.Dab(Vector3.zero, Vector3.back); stroke.End();
            foreach (var pixel in atlas.Pixels)
                if (atlas.Positions[pixel].z > 0.01f) Assert.That(target.Paint.Pixels[pixel].r, Is.Zero);
            Assert.That(visibility.QueryCount, Is.GreaterThan(0));
        }

        [Test]
        public void ThroughPaintSerializationStaysSeparateFromBrushPresets()
        {
            var settings = SimpleBlendBrushSettings.instance;
            var original = EditorJsonUtility.ToJson(settings);
            var preset = ScriptableObject.CreateInstance<SimpleBlendBrushPreset>();
            try
            {
                settings.TextureThroughPaint = true;
                settings.TextureConnectedOnly = true;
                var serialized = EditorJsonUtility.ToJson(settings);
                settings.TextureThroughPaint = false;
                settings.TextureConnectedOnly = false;
                EditorJsonUtility.FromJsonOverwrite(serialized, settings);
                Assert.That(settings.TextureThroughPaint, Is.True);
                Assert.That(settings.TextureConnectedOnly, Is.True);
                preset.Capture(settings);
                settings.TextureThroughPaint = false; settings.TextureConnectedOnly = false; preset.Apply(settings);
                Assert.That(settings.TextureThroughPaint, Is.False);
                Assert.That(settings.TextureConnectedOnly, Is.False);
                settings.TextureThroughPaint = true; settings.TextureConnectedOnly = true; preset.Apply(settings);
                Assert.That(settings.TextureThroughPaint, Is.True);
                Assert.That(settings.TextureConnectedOnly, Is.True);
                for (var i = 1; i < SimpleBlendBrushPreset.BuiltInNames.Length; i++)
                {
                    settings.TextureThroughPaint = false; settings.TextureConnectedOnly = false; SimpleBlendBrushPreset.ApplyBuiltIn(i, settings);
                    Assert.That(settings.TextureThroughPaint, Is.False);
                    Assert.That(settings.TextureConnectedOnly, Is.False);
                    settings.TextureThroughPaint = true; settings.TextureConnectedOnly = true; SimpleBlendBrushPreset.ApplyBuiltIn(i, settings);
                    Assert.That(settings.TextureThroughPaint, Is.True);
                    Assert.That(settings.TextureConnectedOnly, Is.True);
                }
            }
            finally { Object.DestroyImmediate(preset); EditorJsonUtility.FromJsonOverwrite(original, settings); }
        }

        [Test]
        public void ThroughPaintCapturesReachAndWritesHiddenShellGutters()
        {
            var target = Target(false); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            var settings = SimpleBlendBrushSettings.instance; var original = settings.TextureThroughPaint;
            try
            {
                settings.TextureThroughPaint = true;
                stroke.Begin(target, atlas, 1, false, Command(),
                    visibility: new TexturePaintVisibility(atlas, new TexturePaintView(_camera)), throughPaint: settings.TextureThroughPaint);
                settings.TextureThroughPaint = false;
                stroke.Dab(new Vector3(0.48f, 0, 0), Vector3.back); stroke.End();
                var hiddenGutters = 0;
                foreach (var pixel in atlas.Pixels)
                    if (atlas.Positions[pixel].z > 0.01f && atlas.Coverage[pixel] != pixel && target.Paint.Pixels[pixel].r > 0)
                    {
                        Assert.That(target.Paint.Pixels[pixel], Is.EqualTo(target.Paint.Pixels[atlas.Coverage[pixel]]));
                        hiddenGutters++;
                    }
                Assert.That(hiddenGutters, Is.GreaterThan(0));
            }
            finally { stroke.End(); settings.TextureThroughPaint = original; }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ConnectedStrokeNeverRetargetsToAnotherShell(bool through, bool erase)
        {
            var target = Target(false); var atlas = Atlas(target); var paint = target.Paint;
            for (var i = 0; i < paint.Pixels.Length; i++) paint.Pixels[i] = new Color32(erase ? (byte)180 : (byte)0, 0, 91, 37);
            paint.CommitPixels(); var before = (Color32[])paint.Pixels.Clone();
            Assert.That(atlas.Raycast(new Ray(_camera.transform.position, Vector3.forward), out var seed), Is.True);
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, Command(), visibility: new TexturePaintVisibility(atlas, new TexturePaintView(_camera)),
                throughPaint: through, connectedTriangle: seed.TriangleIndex);
            Assert.That(stroke.AllowsHit(seed), Is.True);
            Assert.That(stroke.AllowsHit(new MeshRaycastHit(0, Vector3.zero, Vector3.back, Vector3.zero, 2, 0)), Is.False);
            Assert.That(stroke.AllowsHit(new MeshRaycastHit(0, Vector3.zero, Vector3.back, Vector3.zero, seed.TriangleIndex, 1)), Is.False);
            stroke.Dab(new Vector3(.48f, 0, 0), Vector3.back);
            // A later dab on the other shell cannot change the captured component.
            stroke.Dab(new Vector3(.48f, 0, .02f), Vector3.back); stroke.End();
            var after = (Color32[])paint.Pixels.Clone(); var front = 0; var gutters = 0; var hidden = 0;
            foreach (var pixel in atlas.Pixels)
            {
                Assert.That(after[pixel].b, Is.EqualTo(91)); Assert.That(after[pixel].a, Is.EqualTo(37));
                if (atlas.Positions[pixel].z > .01f) { Assert.That(after[pixel], Is.EqualTo(before[pixel])); hidden++; }
                else if (!after[pixel].Equals(before[pixel])) { front++; if (atlas.Coverage[pixel] != pixel) gutters++; }
            }
            Assert.That(front, Is.GreaterThan(20)); Assert.That(hidden, Is.GreaterThan(20)); Assert.That(gutters, Is.GreaterThan(0));
            Assert.That(paint.Mask.GetPixels32(), Is.EqualTo(after));
            Undo.PerformUndo(); Assert.That(paint.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(paint.Pixels, Is.EqualTo(after));
            // Reusing this stroke without a seed restores nearby-surface behavior.
            stroke.Begin(target, atlas, 1, erase, Command(), throughPaint: true);
            stroke.Dab(Vector3.zero, Vector3.back); stroke.End();
            Assert.That(atlas.Pixels.Any(pixel => atlas.Positions[pixel].z > .01f && !paint.Pixels[pixel].Equals(before[pixel])), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectedFillStillCoversBothParts(bool through)
        {
            var target = Target(false); var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), visibility: new TexturePaintVisibility(atlas, new TexturePaintView(_camera)),
                throughPaint: through, connectedTriangle: 0);
            stroke.Dab(Vector3.zero, Vector3.back, fill: true); stroke.End();
            foreach (var pixel in atlas.Pixels) Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(255));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidConnectedSeedRejectsBeforeStrokeOrUndo(int seed)
        {
            var target = Target(false); var before = (Color32[])target.Paint.Pixels.Clone(); var stroke = new TexturePaintStroke();
            var undo = Undo.GetCurrentGroup();
            Assert.Throws<System.ArgumentException>(() => stroke.Begin(target, Atlas(target), 1, false, Command(), connectedTriangle: seed));
            Assert.That(stroke.Active, Is.False); Assert.That(stroke.End(), Is.Zero);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undo)); Assert.That(target.Paint.Pixels, Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectedStrokePreservesPartUnderNonuniformOrMirroredTransforms(bool mirrored)
        {
            var target = Target(false); target.transform.localScale = new Vector3(mirrored ? -2 : 2, .5f, 1.5f);
            target.transform.rotation = Quaternion.Euler(0, 15, 0);
            var atlas = Atlas(target); var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, Command(), throughPaint: true, connectedTriangle: 0);
            stroke.Dab(target.transform.TransformPoint(Vector3.zero), target.transform.TransformDirection(Vector3.back)); stroke.End();
            var front = 0;
            foreach (var pixel in atlas.Pixels)
                if (atlas.Positions[pixel].z > .01f) Assert.That(target.Paint.Pixels[pixel].r, Is.Zero);
                else if (target.Paint.Pixels[pixel].r > 0) front++;
            Assert.That(front, Is.GreaterThan(20));
        }

        private TexturePaintTarget Target(bool occludingSection, float gap = 0.02f, bool reverseOccluder = false, bool reverseHiddenNormals = false)
        {
            var mesh = new Mesh { name = "Overlapping shells" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0), new Vector3(0.5f,-0.5f,0),
                new Vector3(-0.5f,-0.5f,gap), new Vector3(-0.5f,0.5f,gap), new Vector3(0.5f,0.5f,gap), new Vector3(0.5f,-0.5f,gap)
            };
            mesh.normals = Enumerable.Range(0, 8).Select(i => reverseHiddenNormals && i >= 4 ? Vector3.forward : Vector3.back).ToArray();
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
