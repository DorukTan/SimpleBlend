using System;
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
    internal sealed class TexturePaintingTests
    {
        private TestAssetScope _assets;
        private Scene _scene;
        private GameObject _source;
        private SurfacePalette _palette;
        private string PaintPath => _assets.AssetPath + "/Paint.asset";

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssetScope();
            _scene = EditorSceneManager.NewPreviewScene();
            _source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(_source, _scene);
            var mesh = Object.Instantiate(_source.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "Source cube";
            // Deliberately overlapping source UV0; cube faces must not share paint texels.
            mesh.uv2 = mesh.uv;
            AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/Source.asset");
            _source.GetComponent<MeshFilter>().sharedMesh = mesh;
            _palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Palette.asset", new[]
            {
                new SurfaceDefinition { Name = "Stone", Tint = Color.gray },
                new SurfaceDefinition { Name = "Moss", Tint = Color.green },
                new SurfaceDefinition { Name = "Dirt", Tint = Color.red },
                new SurfaceDefinition { Name = "Snow", Tint = Color.white }
            });
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimpleBlendTexturePaintTool.Stop();
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets?.Dispose();
        }

        [Test]
        public void SetupOwnsItsAssetsAndPreservesOriginalMeshPaletteAndCollider()
        {
            var originalMesh = _source.GetComponent<MeshFilter>().sharedMesh;
            var originalUv = originalMesh.uv;
            var materialJson = EditorJsonUtility.ToJson(_palette.Material);
            var target = Create();
            Assert.That(target.gameObject, Is.Not.SameAs(_source));
            Assert.That(target.Paint.Mesh, Is.Not.SameAs(originalMesh));
            Assert.That(target.Paint.Material, Is.Not.SameAs(_palette.Material));
            Assert.That(AssetDatabase.GetAssetPath(target.Paint.Mesh), Is.EqualTo(PaintPath));
            Assert.That(AssetDatabase.GetAssetPath(target.Paint.Mask), Is.EqualTo(PaintPath));
            Assert.That(AssetDatabase.GetAssetPath(target.Paint.Material), Is.EqualTo(PaintPath));
            Assert.That(target.Paint.Material.GetFloat("_UseTextureWeights"), Is.EqualTo(1));
            Assert.That(target.Paint.Material.GetFloat("_HeightBlendStrength"), Is.Zero);
            Assert.That(EditorJsonUtility.ToJson(_palette.Material), Is.EqualTo(materialJson));
            Assert.That(originalMesh.uv, Is.EqualTo(originalUv));
            Assert.That(_source.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(target.GetComponent<Collider>(), Is.Null);
            Assert.That(_source.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(TexturePaintStroke.Problem(target), Is.Empty);
        }

        [Test]
        public void SetupUndoRestoresOriginalRendererAndRedoRestoresThePaintObject()
        {
            var target = Create();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(target == null, Is.True);
            Assert.That(_source.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(PaintPath), Is.Not.Null, "Setup undo retains the saved reusable asset.");
            Undo.PerformRedo();
            Assert.That(_scene.GetRootGameObjects().Count(x => x.GetComponent<TexturePaintTarget>() != null), Is.EqualTo(1));
            Assert.That(_source.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        [TestCase(128)]
        [TestCase(2048)]
        public void SetupRejectsUnsupportedResolutionWithoutChangingSource(int resolution)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TexturePaintSetup.Create(_source, _palette, 0, resolution, PaintPath));
            Assert.That(_source.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(PaintPath), Is.Null);
        }

        [Test]
        public void SetupNeverOverwritesExistingPaintAsset()
        {
            var first = Create();
            Assert.Throws<ArgumentException>(() => TexturePaintSetup.Create(_source, _palette, 0, 256, PaintPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(PaintPath), Is.SameAs(first.Paint));
        }

        [Test]
        public void PaintAtlasPreservesSurfaceAndStaticLightmapUvsAfterVertexSplits()
        {
            var target = Create();
            var mesh = target.Paint.Mesh;
            Assert.That(mesh.uv4.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv2, Is.EqualTo(mesh.uv), "The deliberately identical original UV0 and UV2 remain identical after unwrap remapping.");
            Assert.That(mesh.uv4, Is.Not.EqualTo(mesh.uv));
        }

        [Test]
        public void SmallSoftStrokePaintsFaceInteriorWithoutPaintingVerticesOrOppositeFaces()
        {
            var target = Create();
            var atlas = Atlas(target);
            var stroke = Begin(target, atlas, 1, radius: 0.1f);
            Assert.That(stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back), Is.GreaterThan(10));
            Assert.That(stroke.End(), Is.GreaterThan(10));
            var changed = 0; var soft = 0;
            for (var i = 0; i < atlas.Coverage.Length; i++)
            {
                if (atlas.Coverage[i] != i) continue;
                var red = target.Paint.Pixels[i].r;
                if (red == 0) continue;
                changed++;
                if (red < 250) soft++;
                Assert.That(atlas.Positions[i].z, Is.EqualTo(-0.5f).Within(0.0001f));
                Assert.That(new Vector2(atlas.Positions[i].x, atlas.Positions[i].y).magnitude, Is.LessThan(0.101f));
            }
            Assert.That(changed, Is.GreaterThan(10));
            Assert.That(soft, Is.GreaterThan(5));
            Assert.That(target.Paint.Mesh.vertexCount, Is.LessThan(100), "No subdivision is needed.");
            Assert.That(target.Paint.Mesh.colors32.Length, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedDabsRespectCappedOrBuildUpBehavior(bool accumulate)
        {
            var target = Create(); var atlas = Atlas(target);
            var stroke = Begin(target, atlas, 1, strength: 0.5f, accumulate: accumulate, flat: true);
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back);
            var once = (Color32[])target.Paint.Pixels.Clone();
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back);
            stroke.End();
            var pixel = Array.FindIndex(once, color => color.r > 0);
            Assert.That(pixel, Is.GreaterThanOrEqualTo(0));
            Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(accumulate ? 192 : 128).Within(1));
        }

        [Test]
        public void CappedStrokeNeverWeakensPreviousHigherInfluence()
        {
            var target = Create(); var atlas = Atlas(target);
            var stroke = Begin(target, atlas, 1);
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back);
            var before = (Color32[])target.Paint.Pixels.Clone();
            stroke.Dab(new Vector3(0.1f, 0, -0.5f), Vector3.back);
            stroke.End();
            for (var i = 0; i < before.Length; i++) Assert.That(target.Paint.Pixels[i].r, Is.GreaterThanOrEqualTo(before[i].r));
        }

        [Test]
        public void SurfaceReplacementBaseAndEraseTransferWeightsPredictably()
        {
            var target = Create(); var atlas = Atlas(target);
            Fill(target, atlas, 1);
            Fill(target, atlas, 2, 0.5f);
            var pixel = Array.FindIndex(atlas.Coverage, value => value >= 0);
            Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(127).Within(1));
            Assert.That(target.Paint.Pixels[pixel].g, Is.EqualTo(128).Within(1));
            var erase = Begin(target, atlas, 2, erase: true, flat: true);
            erase.Dab(Vector3.zero, Vector3.up, true); erase.End();
            Assert.That(target.Paint.Pixels[pixel].r, Is.EqualTo(127).Within(1));
            Assert.That(target.Paint.Pixels[pixel].g, Is.Zero);
            Fill(target, atlas, 3);
            Assert.That(target.Paint.Pixels[pixel].b, Is.EqualTo(255));
            Assert.That(target.Paint.Pixels[pixel].r, Is.Zero);
            Fill(target, atlas, 0);
            Assert.That(target.Paint.Pixels[pixel], Is.EqualTo(new Color32()));
        }

        [Test]
        public void StrokeUndoRedoRestoresAuthoritativePixelsAndMaskPixelsTogether()
        {
            var target = Create(); var atlas = Atlas(target);
            Undo.ClearAll();
            var before = (Color32[])target.Paint.Pixels.Clone();
            var stroke = Begin(target, atlas, 1);
            stroke.Dab(new Vector3(0, 0, -0.5f), Vector3.back);
            stroke.Dab(new Vector3(0.05f, 0, -0.5f), Vector3.back);
            stroke.End();
            var after = (Color32[])target.Paint.Pixels.Clone();
            Undo.PerformUndo();
            Assert.That(target.Paint.Pixels, Is.EqualTo(before));
            Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(before));
            Undo.PerformRedo();
            Assert.That(target.Paint.Pixels, Is.EqualTo(after));
            Assert.That(target.Paint.Mask.GetPixels32(), Is.EqualTo(after));
        }

        [Test]
        public void SavedAssetReimportRetainsPixelsMaskAndMaterialReferences()
        {
            var target = Create(); var atlas = Atlas(target);
            Fill(target, atlas, 2, 0.4f);
            var expected = (Color32[])target.Paint.Pixels.Clone();
            AssetDatabase.SaveAssetIfDirty(target.Paint);
            AssetDatabase.ImportAsset(PaintPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(PaintPath);
            Assert.That(loaded.Pixels, Is.EqualTo(expected));
            Assert.That(loaded.Mask.GetPixels32(), Is.EqualTo(expected));
            Assert.That(loaded.Material.GetTexture("_BlendMask"), Is.EqualTo(loaded.Mask));
            Assert.That(target.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(loaded.Mesh));
        }

        [Test]
        public void GuttersFollowCoveredPixelsWithoutPaintingUnusedAtlasSpace()
        {
            var target = Create(); var atlas = Atlas(target);
            Fill(target, atlas, 1);
            var gutters = 0; var unused = 0;
            for (var i = 0; i < atlas.Coverage.Length; i++)
            {
                if (atlas.Coverage[i] < 0) { unused++; Assert.That(target.Paint.Pixels[i].r, Is.Zero); }
                else if (atlas.Coverage[i] != i)
                { gutters++; Assert.That(target.Paint.Pixels[i], Is.EqualTo(target.Paint.Pixels[atlas.Coverage[i]])); }
            }
            Assert.That(gutters, Is.GreaterThan(0));
            Assert.That(unused, Is.GreaterThan(0));
        }

        [Test]
        public void BrushUsesWorldRadiusOnNonUniformlyScaledTargets()
        {
            var target = Create(); target.transform.localScale = new Vector3(2, 3, 0.5f);
            var atlas = Atlas(target);
            var stroke = Begin(target, atlas, 1, radius: 0.15f);
            stroke.Dab(new Vector3(0, 0, -0.25f), Vector3.back); stroke.End();
            var changed = 0;
            for (var i = 0; i < atlas.Coverage.Length; i++)
            {
                if (target.Paint.Pixels[i].r == 0) continue;
                var world = target.transform.TransformPoint(atlas.Positions[i]);
                Assert.That(new Vector2(world.x, world.y).magnitude, Is.LessThan(0.151f)); changed++;
            }
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void ZeroFlowDoesNotCreateAnUndoStepOrChangePixels()
        {
            var target = Create(); var atlas = Atlas(target);
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, false, new VertexBrushCommand(Vector3.zero, 1, 1, 0, new Color32(), null,
                VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, false, VertexBrushMask.None));
            Assert.That(stroke.Dab(Vector3.zero, Vector3.up, true), Is.Zero);
            Assert.That(stroke.End(), Is.Zero);
        }

        [Test]
        public void MissingOwnedMeshOrMaterialBlocksPaintingWithoutWrites()
        {
            var target = Create();
            target.GetComponent<MeshFilter>().sharedMesh = _source.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(TexturePaintStroke.Problem(target), Does.Contain("owned paint mesh"));
            target.GetComponent<MeshFilter>().sharedMesh = target.Paint.Mesh;
            target.GetComponent<MeshRenderer>().sharedMaterial = _palette.Material;
            Assert.That(TexturePaintStroke.Problem(target), Does.Contain("painted material slot"));
        }

        private TexturePaintTarget Create() => TexturePaintSetup.Create(_source, _palette, 0, 256, PaintPath);

        [Test]
        public void WorldProjectedDabCrossesSeparatedUvChartsWithoutRepeatingTheOtherFace()
        {
            var mesh = SplitPlane(false);
            _source.GetComponent<MeshFilter>().sharedMesh = mesh;
            var target = Create();
            var owned = target.Paint.Mesh;
            var positions = owned.vertices;
            var triangles = owned.triangles;
            var uv = new Vector2[owned.vertexCount];
            for (var t = 0; t < triangles.Length; t++)
            {
                var index = triangles[t]; var right = t >= 6;
                uv[index] = new Vector2((positions[index].x + (right ? 0 : 0.5f)) * 0.6f + (right ? 0.6f : 0.05f),
                    (positions[index].y + 0.5f) * 0.8f + 0.1f);
            }
            owned.uv4 = uv;
            var atlas = Atlas(target);
            var stroke = Begin(target, atlas, 1, radius: 0.15f);
            stroke.Dab(Vector3.zero, Vector3.forward); stroke.End();
            var left = 0; var rightCount = 0;
            for (var i = 0; i < atlas.Coverage.Length; i++)
            {
                if (atlas.Coverage[i] != i || target.Paint.Pixels[i].r == 0) continue;
                if (atlas.Positions[i].x < 0) left++; else rightCount++;
                Assert.That(atlas.Positions[i].magnitude, Is.LessThan(0.151f));
            }
            Assert.That(left, Is.GreaterThan(20));
            Assert.That(rightCount, Is.GreaterThan(20));
        }

        [Test]
        public void TextureFillIsIsolatedToConfiguredSectionAndPreservesOtherMaterialSlots()
        {
            _source.GetComponent<MeshFilter>().sharedMesh = SplitPlane(true);
            _source.GetComponent<MeshRenderer>().sharedMaterials = new[] { _palette.Material, _palette.Material };
            var target = Create(); var atlas = Atlas(target);
            Fill(target, atlas, 1);
            Assert.That(target.GetComponent<MeshRenderer>().sharedMaterials[1], Is.EqualTo(_palette.Material));
            foreach (var index in atlas.Pixels) Assert.That(atlas.Positions[index].x, Is.LessThanOrEqualTo(0.00001f));
            Assert.That(atlas.Pixels.Count, Is.GreaterThan(100));
        }

        [Test]
        public void LegacySectionLookupsDoNotMutateThePrimaryBinding()
        {
            var target = Create(); var before = EditorJsonUtility.ToJson(target);
            Assert.That(target.PaintForSection(0), Is.SameAs(target.Paint));
            Assert.That(target.PaintForSection(-1), Is.Null);
            Assert.That(target.PaintForSection(1), Is.Null);
            Assert.That(target.PaintForSection(int.MaxValue), Is.Null);
            Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
        }

        [Test]
        public void PreparingAnotherSectionPreservesExistingMeshUvsPaintAndSourceInputs()
        {
            var target = MultiSectionTarget(); var primary = target.Paint;
            Fill(target, Atlas(target), 1, 0.6f); TexturePaintSaveQueue.Flush();
            var mesh = primary.Mesh; var uv = mesh.uv4;
            var pixels = (Color32[])primary.Pixels.Clone();
            var primaryBytes = System.IO.File.ReadAllBytes(PaintPath);
            var sourceMeshJson = EditorJsonUtility.ToJson(_source.GetComponent<MeshFilter>().sharedMesh);
            var paletteJson = EditorJsonUtility.ToJson(_palette.Material);
            var second = TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            Assert.That(target.Paint, Is.SameAs(primary)); Assert.That(target.MaterialSection, Is.Zero);
            Assert.That(target.PaintForSection(1), Is.SameAs(second));
            Assert.That(second.Mesh, Is.SameAs(mesh)); Assert.That(mesh.uv4, Is.EqualTo(uv));
            Assert.That(second.Material, Is.Not.SameAs(primary.Material)); Assert.That(second.Mask, Is.Not.SameAs(primary.Mask));
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(PaintPath));
            Assert.That(AssetDatabase.GetAssetPath(second.Material), Is.EqualTo(SectionPath));
            Assert.That(AssetDatabase.GetAssetPath(second.Mask), Is.EqualTo(SectionPath));
            Assert.That(second.Mask.width, Is.EqualTo(primary.Mask.width));
            Assert.That(primary.Pixels, Is.EqualTo(pixels)); Assert.That(primary.Mask.GetPixels32(), Is.EqualTo(pixels));
            Assert.That(System.IO.File.ReadAllBytes(PaintPath), Is.EqualTo(primaryBytes));
            Assert.That(EditorJsonUtility.ToJson(_source.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(sourceMeshJson));
            Assert.That(EditorJsonUtility.ToJson(_palette.Material), Is.EqualTo(paletteJson));
            Assert.That(_source.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(_source.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(new[] { _palette.Material, _palette.Material }));
            Assert.That(target.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(new[] { primary.Material, second.Material }));
            Assert.That(TexturePaintStroke.Problem(target, 1), Is.Empty);
        }

        [Test]
        public void SectionPreparationIsOneUndoAndRetainsItsSavedAssetForRedo()
        {
            var target = MultiSectionTarget(); Undo.FlushUndoRecordObjects(); Undo.ClearAll();
            var second = TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(target.PaintForSection(1), Is.Null);
            Assert.That(target.GetComponent<MeshRenderer>().sharedMaterials[1].GetInstanceID(), Is.EqualTo(_palette.Material.GetInstanceID()));
            Assert.That(AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(SectionPath), Is.EqualTo(second));
            Assert.That(TexturePaintStroke.Problem(target), Is.Empty);
            Undo.PerformRedo();
            Assert.That(target.PaintForSection(1), Is.EqualTo(second));
            Assert.That(TexturePaintStroke.Problem(target, 1), Is.Empty);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SectionStrokeWritesOnlyItsOwnMaskAndHasIndependentUndo(bool erase, bool fill)
        {
            var target = MultiSectionTarget();
            Fill(target, Atlas(target), 1, 0.6f);
            var primaryPixels = (Color32[])target.Paint.Pixels.Clone();
            var second = TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            var atlas = new TexturePaintAtlas(second.Mesh, 1, 256);
            foreach (var pixel in atlas.Pixels) second.Pixels[pixel] = new Color32((byte)(erase ? 255 : 0), 0, 0, 37);
            second.CommitPixels(); Undo.FlushUndoRecordObjects(); Undo.ClearAll();
            var before = (Color32[])second.Pixels.Clone();
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, 1, erase, SectionCommand(), materialSection: 1);
            stroke.Dab(new Vector3(0.25f, 0, 0), Vector3.forward, fill); Assert.That(stroke.End(), Is.GreaterThan(20));
            var after = (Color32[])second.Pixels.Clone();
            Assert.That(target.Paint.Pixels, Is.EqualTo(primaryPixels));
            foreach (var pixel in atlas.Pixels)
            {
                Assert.That(second.Pixels[pixel].a, Is.EqualTo(37));
                Assert.That(atlas.Positions[pixel].x, Is.GreaterThanOrEqualTo(-0.00001f));
                if (fill) Assert.That(second.Pixels[pixel].r, Is.EqualTo(128).Within(1));
            }
            Undo.PerformUndo(); Assert.That(second.Pixels, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(second.Pixels, Is.EqualTo(after));
            Assert.That(second.Mask.GetPixels32(), Is.EqualTo(after));
            Assert.That(target.Paint.Pixels, Is.EqualTo(primaryPixels));
        }

        [TestCase(0, 256)]
        [TestCase(1, 128)]
        public void SectionStrokeRejectsMismatchedAtlasBeforeItBecomesActive(int section, int resolution)
        {
            var target = MultiSectionTarget();
            var second = TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            var before = (Color32[])second.Pixels.Clone();
            var stroke = new TexturePaintStroke();
            Assert.Throws<ArgumentException>(() => stroke.Begin(target,
                new TexturePaintAtlas(second.Mesh, section, resolution), 1, false, SectionCommand(), materialSection: 1));
            Assert.That(stroke.Active, Is.False); Assert.That(second.Pixels, Is.EqualTo(before));
        }

        [TestCase("Duplicate")]
        [TestCase("InvalidSlot")]
        [TestCase("NegativeSlot")]
        [TestCase("NullPalette")]
        [TestCase("ExistingPath")]
        [TestCase("EscapingPath")]
        [TestCase("EmptySection")]
        public void InvalidSectionPreparationLeavesBindingsMaterialsAndSavedPaintUntouched(string problem)
        {
            var target = MultiSectionTarget(); var section = 1; var path = SectionPath; var palette = _palette;
            var exception = typeof(ArgumentException);
            switch (problem)
            {
                case "Duplicate": section = 0; exception = typeof(InvalidOperationException); break;
                case "InvalidSlot": section = 2; exception = typeof(ArgumentOutOfRangeException); break;
                case "NegativeSlot": section = -1; exception = typeof(ArgumentOutOfRangeException); break;
                case "NullPalette": palette = null; exception = typeof(ArgumentNullException); break;
                case "ExistingPath": path = PaintPath; break;
                case "EscapingPath": path = _assets.AssetPath + "/../Escaping.asset"; break;
                case "EmptySection": target.Paint.Mesh.SetTriangles(Array.Empty<int>(), 1); exception = typeof(InvalidOperationException); break;
            }
            var before = EditorJsonUtility.ToJson(target);
            var materials = target.GetComponent<MeshRenderer>().sharedMaterials;
            var bytes = System.IO.File.ReadAllBytes(PaintPath);
            Assert.Throws(exception, () => TexturePaintSetup.PrepareSection(target, palette, section, path));
            Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
            Assert.That(target.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(materials));
            Assert.That(System.IO.File.ReadAllBytes(PaintPath), Is.EqualTo(bytes));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(SectionPath), Is.Null);
        }

        [Test]
        public void UnpreparedOrDamagedSectionCannotPaintThroughAnotherSlotsBinding()
        {
            var target = MultiSectionTarget();
            Assert.That(TexturePaintStroke.Problem(target, 1), Does.Contain("Prepare this material section"));
            TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            var materials = target.GetComponent<MeshRenderer>().sharedMaterials;
            materials[1] = _palette.Material; target.GetComponent<MeshRenderer>().sharedMaterials = materials;
            Assert.That(TexturePaintStroke.Problem(target, 1), Does.Contain("restore its painted material slot"));
            Assert.That(TexturePaintStroke.Problem(target), Is.Empty);
        }

        [Test]
        public void PreparingSectionZeroDoesNotReplaceANonzeroPrimaryBinding()
        {
            var target = MultiSectionTarget(1); var primary = target.Paint;
            var second = TexturePaintSetup.PrepareSection(target, _palette, 0, SectionPath);
            Assert.That(target.MaterialSection, Is.EqualTo(1)); Assert.That(target.Paint, Is.SameAs(primary));
            Assert.That(target.PaintForSection(0), Is.SameAs(second));
            Assert.That(target.PaintForSection(1), Is.SameAs(primary));
            Assert.That(TexturePaintStroke.Problem(target, 0), Is.Empty);
        }

        [Test]
        public void PreparedSectionUsesItsOwnPaletteAndSurfaceLimits()
        {
            var target = MultiSectionTarget();
            var palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/SecondPalette.asset", new[]
            {
                new SurfaceDefinition { Name = "Sand" }, new SurfaceDefinition { Name = "Wet sand" }, new SurfaceDefinition { Name = "Shells" }
            });
            var second = TexturePaintSetup.PrepareSection(target, palette, 1, SectionPath);
            Assert.That(second.SurfaceCount, Is.EqualTo(3)); Assert.That(second.SurfaceName(0), Is.EqualTo("Sand"));
            Assert.That(target.Paint.SurfaceCount, Is.EqualTo(4)); Assert.That(target.Paint.SurfaceName(0), Is.EqualTo("Stone"));
            var stroke = new TexturePaintStroke();
            Assert.Throws<ArgumentOutOfRangeException>(() => stroke.Begin(target,
                new TexturePaintAtlas(second.Mesh, 1, 256), 3, false, SectionCommand(), materialSection: 1));
            Assert.That(stroke.Active, Is.False);
        }

        private string SectionPath => _assets.AssetPath + "/Section1.asset";

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PreparingFromPrimaryCopiesNamedSurfacesButNotExistingPaint(int count)
        {
            var target = MultiSectionTarget();
            while (target.Paint.SurfaceCount > count) TextureSurfaceAuthoring.Remove(target.Paint, target.Paint.SurfaceCount - 1);
            target.Paint.Material.SetFloat("_HeightBlendStrength", 1.7f);
            Fill(target, Atlas(target), count > 1 ? 1 : 0, 0.6f);
            var before = (Color32[])target.Paint.Pixels.Clone();
            var material = EditorJsonUtility.ToJson(target.Paint.Material); var uv = target.Paint.Mesh.uv4;
            var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            Assert.That(second.SurfaceCount, Is.EqualTo(count));
            Assert.That(second.Material.GetFloat("_HeightBlendStrength"), Is.EqualTo(1.7f));
            for (var i = 0; i < count; i++)
            {
                Assert.That(second.SurfaceName(i), Is.EqualTo(target.Paint.SurfaceName(i)));
                Assert.That(second.Material.GetColor($"_Layer{i}Color"), Is.EqualTo(target.Paint.Material.GetColor($"_Layer{i}Color")));
            }
            Assert.That(second.Pixels.All(pixel => pixel.Equals(new Color32())), Is.True);
            Assert.That(target.Paint.Pixels, Is.EqualTo(before)); Assert.That(target.Paint.Mesh.uv4, Is.EqualTo(uv));
            Assert.That(EditorJsonUtility.ToJson(target.Paint.Material), Is.EqualTo(material));
            Assert.That(AssetDatabase.GetAssetPath(second), Does.StartWith(_assets.AssetPath + "/"));
            Assert.That(TexturePaintStroke.Problem(target, 1), Is.Empty);
        }

        [Test]
        public void RepreparingAfterUndoRetainsOldAssetAndChoosesANewPath()
        {
            var target = MultiSectionTarget(); Undo.FlushUndoRecordObjects(); Undo.ClearAll();
            var first = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var path = AssetDatabase.GetAssetPath(first);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            Assert.That(AssetDatabase.GetAssetPath(second), Is.Not.EqualTo(path));
            Assert.That(AssetDatabase.LoadAssetAtPath<TexturePaintAsset>(path), Is.EqualTo(first));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticHitSelectsPreparedNearestSectionWhileLockRejectsIt(bool automatic)
        {
            var target = MultiSectionTarget(); TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var files = AssetDatabase.FindAssets("", new[] { _assets.AssetPath });
            var before = EditorJsonUtility.ToJson(target);
            var resolved = SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), automatic,
                0, 1, null, out var hit, out var surface, out var problem);
            Assert.That(resolved, Is.EqualTo(automatic)); Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
            if (automatic) { Assert.That(surface, Is.EqualTo(1)); Assert.That(problem, Is.Null); }
            else Assert.That(problem, Does.Contain("locked"));
            Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
            Assert.That(AssetDatabase.FindAssets("", new[] { _assets.AssetPath }), Is.EqualTo(files));
        }

        [Test]
        public void UnpreparedHitBlocksAutomaticRoutingWithoutCreatingAssets()
        {
            var target = MultiSectionTarget(); var files = AssetDatabase.FindAssets("", new[] { _assets.AssetPath });
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, 1, null, out var hit, out _, out var problem), Is.False);
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1)); Assert.That(problem, Does.Contain("Prepare this material section"));
            Assert.That(target.PaintForSection(1), Is.Null);
            Assert.That(AssetDatabase.FindAssets("", new[] { _assets.AssetPath }), Is.EqualTo(files));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticRoutingHandlesMirroredAndNonuniformTargetTransforms(bool mirrored)
        {
            var target = MultiSectionTarget(); TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            target.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(10, 20, 30));
            target.transform.localScale = new Vector3(mirrored ? -2 : 2, 0.5f, 1.3f);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, 1, null, out var hit, out _, out _), Is.True);
            Assert.That(hit.SubmeshIndex, Is.EqualTo(1));
        }

        [Test]
        public void AutomaticSurfaceMappingUsesNamesInsteadOfReorderedChannelIndices()
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            second.SetSurfaceNames(new[] { "Stone", "Dirt", "Moss", "Snow" });
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, 1, null, out _, out var surface, out _), Is.True);
            Assert.That(surface, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DifferentPalettesBlockUnmatchedOverlaysButAllowExplicitBase(bool useBase)
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            second.SetSurfaceNames(new[] { "Sand", "Wet sand", "Shells" });
            var resolved = SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, useBase ? 0 : 1, null, out _, out var surface, out var problem);
            Assert.That(resolved, Is.EqualTo(useBase));
            if (useBase) Assert.That(surface, Is.Zero);
            else Assert.That(problem, Does.Contain("choose a surface"));
        }

        [Test]
        public void RememberedSurfaceOverridesNameMappingAndAmbiguousNamesAreBlocked()
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            second.SetSurfaceNames(new[] { "Stone", "Moss", "Moss", "Snow" });
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, 1, null, out _, out _, out _), Is.False);
            var choices = new Dictionary<int, string> { [1] = "Snow" };
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, Atlas(target), SectionRay(target, 0.25f), true,
                0, 1, choices, out _, out var surface, out _), Is.True);
            Assert.That(surface, Is.EqualTo(3));
        }

        [Test]
        public void RememberedSurfaceNameSurvivesRemovalUndoAndRedoWithoutChangingMeaning()
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            SimpleBlendTexturePaintTool.Start(target, 3, false, 1, true);
            Undo.FlushUndoRecordObjects(); Undo.ClearAll();
            TextureSurfaceAuthoring.Remove(second, 1);
            SimpleBlendTexturePaintTool.Configure(target, 2, false);
            Assert.That(second.SurfaceName(SimpleBlendTexturePaintTool.Surface), Is.EqualTo("Snow"));
            Undo.PerformUndo();
            Assert.That(SimpleBlendTexturePaintTool.Surface, Is.EqualTo(3));
            Assert.That(second.SurfaceName(SimpleBlendTexturePaintTool.Surface), Is.EqualTo("Snow"));
            Undo.PerformRedo();
            Assert.That(SimpleBlendTexturePaintTool.Surface, Is.EqualTo(2));
            Assert.That(second.SurfaceName(SimpleBlendTexturePaintTool.Surface), Is.EqualTo("Snow"));
        }

        [Test]
        public void SectionSwitchFillAndModeChangesKeepToolActiveAndRememberLocalSurfaces()
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var settings = SimpleBlendBrushSettings.instance; var beforeSettings = EditorJsonUtility.ToJson(settings);
            try
            {
                settings.Strength = 0.5f; settings.Flow = 1;
                SimpleBlendTexturePaintTool.Start(target, 2, false, automatic: true);
                SimpleBlendTexturePaintTool.SelectSection(target, 1);
                Assert.That(SimpleBlendTexturePaintTool.Fill(target, 1), Is.GreaterThan(0));
                Assert.That(SimpleBlendTexturePaintTool.Section, Is.EqualTo(1));
                Assert.That(SimpleBlendTexturePaintTool.Paint, Is.EqualTo(second));
                Assert.That(SimpleBlendTexturePaintTool.LastPaint, Is.EqualTo(second));
                Assert.That(target.Paint.Pixels.All(pixel => pixel.r == 0), Is.True);
                SimpleBlendTexturePaintTool.Configure(target, 3, true);
                SimpleBlendTexturePaintTool.SelectSection(target, 0);
                Assert.That(SimpleBlendTexturePaintTool.Surface, Is.EqualTo(2));
                SimpleBlendTexturePaintTool.SelectSection(target, 1);
                Assert.That(SimpleBlendTexturePaintTool.Surface, Is.EqualTo(3));
                SimpleBlendTexturePaintTool.SetSectionMode(target, false);
                Assert.That(SimpleBlendTexturePaintTool.AutomaticSections, Is.False);
                Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            }
            finally { EditorJsonUtility.FromJsonOverwrite(beforeSettings, settings); }
        }

        [Test]
        public void UndoingPreparedActiveSectionFallsBackToPrimaryAndInvalidatesCachedAtlas()
        {
            var target = MultiSectionTarget(); Undo.FlushUndoRecordObjects(); Undo.ClearAll();
            SimpleBlendTexturePaintTool.Start(target, 1, false, automatic: true);
            TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            SimpleBlendTexturePaintTool.SelectSection(target, 1);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
            Assert.That(SimpleBlendTexturePaintTool.Section, Is.Zero);
            Assert.That(SimpleBlendTexturePaintTool.Paint, Is.EqualTo(target.Paint));
            Assert.That(target.PaintForSection(1), Is.Null);
            Undo.PerformRedo(); SimpleBlendTexturePaintTool.SelectSection(target, 1);
            Assert.That(SimpleBlendTexturePaintTool.Section, Is.EqualTo(1));
        }

        [Test]
        public void AutomaticDestinationWritesOnlyItsMaskAndRejectsBoundaryCrossingWithinOneUndo()
        {
            var target = MultiSectionTarget(); var second = TexturePaintSetup.PrepareSectionFromPrimary(target, 1);
            var atlas = Atlas(target);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, atlas, SectionRay(target, 0.25f), true,
                0, 1, null, out var hit, out var surface, out _), Is.True);
            var primary = (Color32[])target.Paint.Pixels.Clone(); var before = (Color32[])second.Pixels.Clone(); Undo.ClearAll();
            var stroke = new TexturePaintStroke(); var destination = new TexturePaintAtlas(second.Mesh, hit.SubmeshIndex, 256);
            stroke.Begin(target, destination, surface, false, SectionCommand(), materialSection: hit.SubmeshIndex);
            stroke.Dab(hit.Point, hit.Normal);
            Assert.That(SimpleBlendTexturePaintTool.ResolveHit(target, destination, SectionRay(target, -0.25f), false,
                1, surface, null, out _, out _, out _), Is.False);
            Assert.That(stroke.End(), Is.GreaterThan(0));
            Assert.That(target.Paint.Pixels, Is.EqualTo(primary)); Assert.That(second.Pixels, Is.Not.EqualTo(before));
            Undo.PerformUndo(); Assert.That(second.Pixels, Is.EqualTo(before)); Assert.That(target.Paint.Pixels, Is.EqualTo(primary));
        }

        [Test]
        public void StoppingForSectionPreparationRetainsSceneContextAfterProjectAssetSelection()
        {
            var selection = Selection.objects;
            try
            {
                var target = MultiSectionTarget();
                SimpleBlendTexturePaintTool.Start(target, 1, false, automatic: true);
                Selection.activeObject = target.Paint;
                Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.True);
                SimpleBlendTexturePaintTool.StopForSectionSetup(target);
                Assert.That(SimpleBlendTexturePaintTool.IsActiveFor(target), Is.False);
                Assert.That(Selection.activeGameObject, Is.EqualTo(target.gameObject));
                Assert.That(target.PaintForSection(1), Is.Null);
            }
            finally { Selection.objects = selection; }
        }

        private static Ray SectionRay(TexturePaintTarget target, float x)
        {
            var point = target.transform.TransformPoint(new Vector3(x, 0, 0));
            var front = target.transform.TransformDirection(Vector3.forward);
            return new Ray(point + front * 2, -front);
        }
        private TexturePaintTarget MultiSectionTarget(int section = 0)
        {
            _source.GetComponent<MeshFilter>().sharedMesh = SplitPlane(true);
            _source.GetComponent<MeshRenderer>().sharedMaterials = new[] { _palette.Material, _palette.Material };
            return TexturePaintSetup.Create(_source, _palette, section, 256, PaintPath);
        }
        private static VertexBrushCommand SectionCommand() => new VertexBrushCommand(Vector3.zero, 0.15f, 0.5f, 1,
            new Color32(), AnimationCurve.Constant(0, 1, 1), VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, false, VertexBrushMask.None);

        [Test]
        public void ThreeSurfaceTexturePaintingKeepsUnusedBlueAndUsesOnlyRgWeights()
        {
            var palette = SurfacePaletteAuthoring.Create(_assets.AssetPath + "/Three.asset", new[]
            {
                new SurfaceDefinition { Name = "Base" }, new SurfaceDefinition { Name = "Moss" }, new SurfaceDefinition { Name = "Stone" }
            });
            var target = TexturePaintSetup.Create(_source, palette, 0, 256, PaintPath);
            var atlas = Atlas(target);
            foreach (var pixel in atlas.Pixels) target.Paint.Pixels[pixel] = new Color32(0, 0, 73, 91);
            target.Paint.CommitPixels();
            Fill(target, atlas, 2);
            foreach (var pixel in atlas.Pixels) Assert.That(target.Paint.Pixels[pixel], Is.EqualTo(new Color32(0, 255, 73, 91)));
        }

        [Test]
        public void UnrelatedUndoDoesNotDirtyOrReuploadUnchangedPaintAssets()
        {
            var target = Create();
            AssetDatabase.SaveAssetIfDirty(target.Paint);
            Assert.That(target.Paint.RebuildMask(), Is.False);
            Assert.That(EditorUtility.IsDirty(target.Paint), Is.False);
            Assert.That(EditorUtility.IsDirty(target.Paint.Mask), Is.False);
            TexturePaintUndo.RebuildLoadedMasks();
            Assert.That(target.Paint.RebuildMask(), Is.False);
            Assert.That(EditorUtility.IsDirty(target.Paint), Is.False);
            Assert.That(EditorUtility.IsDirty(target.Paint.Mask), Is.False);
        }

        [Test]
        public void PaintedObjectAndOwnedReferencesSurviveSceneSaveAndReopen()
        {
            var target = MultiSectionTarget(); var atlas = Atlas(target);
            Fill(target, atlas, 1, 0.6f);
            var expected = (Color32[])target.Paint.Pixels.Clone();
            var second = TexturePaintSetup.PrepareSection(target, _palette, 1, SectionPath);
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, new TexturePaintAtlas(second.Mesh, 1, 256), 1, false, SectionCommand(), materialSection: 1);
            stroke.Dab(Vector3.zero, Vector3.up, true); stroke.End();
            var expectedSecond = (Color32[])second.Pixels.Clone();
            AssetDatabase.SaveAssetIfDirty(second);
            AssetDatabase.SaveAssetIfDirty(target.Paint);
            var savedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scenePath = _assets.AssetPath + "/Painted.unity";
            try
            {
                SceneManager.MoveGameObjectToScene(target.gameObject, savedScene);
                Assert.That(EditorSceneManager.SaveScene(savedScene, scenePath), Is.True);
                savedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var loaded = savedScene.GetRootGameObjects().Single().GetComponent<TexturePaintTarget>();
                Assert.That(TexturePaintStroke.Problem(loaded), Is.Empty);
                Assert.That(loaded.Paint.Pixels, Is.EqualTo(expected));
                Assert.That(loaded.Paint.Mask.GetPixels32(), Is.EqualTo(expected));
                Assert.That(TexturePaintStroke.Problem(loaded, 1), Is.Empty);
                Assert.That(loaded.PaintForSection(1).Mesh, Is.SameAs(loaded.Paint.Mesh));
                Assert.That(loaded.PaintForSection(1).Pixels, Is.EqualTo(expectedSecond));
                Assert.That(loaded.PaintForSection(1).Mask.GetPixels32(), Is.EqualTo(expectedSecond));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        private Mesh SplitPlane(bool twoSections)
        {
            var mesh = new Mesh { name = "Split plane" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0, -0.5f, 0), new Vector3(0, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0, 0.5f, 0)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            if (twoSections)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 1);
            }
            else mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, _assets.AssetPath + "/Split.asset");
            return mesh;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SharedShaderFunctionReadsLinearMaskOrLegacyVertexWeightsOnGpu(bool textureMode)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.simpletools.simpleblend/Tests/Editor/Shaders/TexturePaintProbe.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            var target = Create();
            Fill(target, Atlas(target), 1, 0.5f);
            var pixel = Array.FindIndex(target.Paint.Pixels, color => color.r > 0);
            material.SetTexture("_BlendMask", target.Paint.Mask);
            material.SetFloat("_UseTextureWeights", textureMode ? 1 : 0);
            material.SetVector("_ProbeUv", new Vector4((pixel % 256 + 0.5f) / 256, (pixel / 256 + 0.5f) / 256, 0, 0));
            material.SetVector("_ProbeVertex", new Vector4(0, 1, 0, 0));
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(Texture2D.whiteTexture, rt, material);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply();
                var actual = readback.GetPixel(0, 0);
                Assert.That(actual.r, Is.EqualTo(textureMode ? 128f / 255 : 0).Within(0.004f));
                Assert.That(actual.g, Is.EqualTo(textureMode ? 0 : 1).Within(0.004f));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(readback);
            }
        }

        private static TexturePaintAtlas Atlas(TexturePaintTarget target) => new TexturePaintAtlas(target.Paint.Mesh, target.MaterialSection, target.Paint.Mask.width);
        private static TexturePaintStroke Begin(TexturePaintTarget target, TexturePaintAtlas atlas, int surface,
            float radius = 0.2f, float strength = 1, bool accumulate = false, bool erase = false, bool flat = false)
        {
            var stroke = new TexturePaintStroke();
            stroke.Begin(target, atlas, surface, erase, new VertexBrushCommand(Vector3.zero, radius, strength, 1,
                new Color32(), flat ? AnimationCurve.Constant(0, 1, 1) : AnimationCurve.Linear(0, 1, 1, 0),
                VertexPaintBrushMode.Paint, VertexPaintChannel.RGB, accumulate, VertexBrushMask.None));
            return stroke;
        }
        private static void Fill(TexturePaintTarget target, TexturePaintAtlas atlas, int surface, float strength = 1)
        {
            var stroke = Begin(target, atlas, surface, strength: strength);
            stroke.Dab(Vector3.zero, Vector3.up, true); stroke.End();
        }
    }
}
