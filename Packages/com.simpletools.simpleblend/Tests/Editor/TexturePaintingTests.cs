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
            var target = Create(); var atlas = Atlas(target);
            Fill(target, atlas, 1, 0.6f);
            var expected = (Color32[])target.Paint.Pixels.Clone();
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
