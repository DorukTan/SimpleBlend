using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class PaintWorkspaceTests
    {
        private Scene _scene;
        private GameObject _object;
        private Mesh _mesh;
        private Object[] _selection;
        private TestAssetScope _assets;

        [SetUp]
        public void SetUp()
        {
            _selection = Selection.objects;
            _scene = EditorSceneManager.NewPreviewScene();
            _mesh = TestMeshFactory.CreateQuad(includeColors: true);
            _assets = new TestAssetScope();
            AssetDatabase.CreateAsset(_mesh, _assets.AssetPath + "/Source.asset");
            _object = new GameObject("Workspace test");
            SceneManager.MoveGameObjectToScene(_object, _scene);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _object.AddComponent<MeshRenderer>();
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _selection;
            Undo.ClearAll();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _assets?.Dispose();
        }

        [Test]
        public void EmptyAndMixedSelectionsCannotPrepareOrPaint()
        {
            Assert.That(PaintWorkspaceTarget.Inspect(null).State, Is.EqualTo(PaintWorkspaceState.Empty));
            var mixed = PaintWorkspaceTarget.Inspect(new Object[] { _object, _mesh });
            Assert.That(mixed.State, Is.EqualTo(PaintWorkspaceState.Mixed));
            Assert.That(mixed.CanPrepare || mixed.CanPaint, Is.False);
        }

        [Test]
        public void AssetSelectionDoesNotBecomeAnEditableTarget()
        {
            var target = PaintWorkspaceTarget.Inspect(new Object[] { _mesh });
            Assert.That(target.State, Is.EqualTo(PaintWorkspaceState.Unsupported));
            Assert.That(target.Message, Does.Contain("scene mesh object"));
        }

        [Test]
        public void PrefabAssetIsRejectedWithoutAddingPaintData()
        {
            using (var assets = new TestAssetScope())
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(_object, assets.AssetPath + "/Target.prefab");
                var target = PaintWorkspaceTarget.Inspect(new Object[] { prefab });
                Assert.That(target.State, Is.EqualTo(PaintWorkspaceState.Unsupported));
                Assert.That(target.Message, Does.Contain("Prefab Mode"));
                Assert.That(prefab.GetComponent<VertexPaintData>(), Is.Null);
            }
        }

        [Test]
        public void SelectingUnpreparedRendererDoesNotAddComponentsOrDirtyTheScene()
        {
            var dirtyBefore = _scene.isDirty;
            var colors = _mesh.colors32;
            var target = Inspect();
            Assert.That(target.State, Is.EqualTo(PaintWorkspaceState.NeedsSetup));
            Assert.That(target.CanPrepare, Is.True);
            Assert.That(target.IsSparse, Is.True);
            Assert.That(_object.GetComponent<VertexPaintData>(), Is.Null);
            Assert.That(_mesh.colors32, Is.EqualTo(colors));
            Assert.That(_scene.isDirty, Is.EqualTo(dirtyBefore));
        }

        [Test]
        public void MissingMeshAndInactiveObjectsExplainWhySetupIsUnavailable()
        {
            _object.GetComponent<MeshFilter>().sharedMesh = null;
            Assert.That(Inspect().State, Is.EqualTo(PaintWorkspaceState.Unsupported));
            _object.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _object.SetActive(false);
            Assert.That(Inspect().Message, Does.Contain("active, editable"));
        }

        [Test]
        public void NonTriangleGeometryIsRejectedBeforeSetup()
        {
            _mesh.subMeshCount = 1;
            _mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
            var dirtyBefore = _scene.isDirty;
            Assert.That(Inspect().State, Is.EqualTo(PaintWorkspaceState.Unsupported));
            Assert.That(PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out _).IsValid, Is.False);
            Assert.That(_object.GetComponent<VertexPaintData>(), Is.Null);
            Assert.That(_scene.isDirty, Is.EqualTo(dirtyBefore));
        }

        [Test]
        public void PreviouslyInitializedNonTriangleGeometryCannotStartPainting()
        {
            _mesh.subMeshCount = 1;
            _mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
            MeshSignatureFactory.TryCreate(_mesh, MeshAssetIdentity.GetStableIdentity(_mesh), out var signature);
            var data = _object.AddComponent<VertexPaintData>();
            data.Configure(_mesh, signature, _mesh.colors32, VertexPaintStorageMode.AdditionalVertexStream, VertexPaintStorageReason.OrdinaryMeshRenderer);
            var target = Inspect();
            Assert.That(target.State, Is.EqualTo(PaintWorkspaceState.Unsupported));
            Assert.That(target.CanPaint || target.CanPrepare, Is.False);
        }

        [Test]
        public void ExplicitSetupPreservesSourceAndMaterialsAndIsUndoable()
        {
            var sourceColors = _mesh.colors32;
            var materials = _object.GetComponent<MeshRenderer>().sharedMaterials;
            var result = PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out var data);
            Assert.That(result.IsValid, Is.True, result.Message);
            Assert.That(data.SourceMesh, Is.SameAs(_mesh));
            Assert.That(data.SerializedColors, Is.EqualTo(sourceColors));
            Assert.That(Inspect().CanPaint, Is.True);
            Assert.That(_mesh.colors32, Is.EqualTo(sourceColors));
            Assert.That(_object.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(materials));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_object.GetComponent<VertexPaintData>(), Is.Null);
            Assert.That(_object.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(_mesh));
        }

        [Test]
        public void ExistingPaintCannotBeReinitializedThroughWorkspaceSetup()
        {
            PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out var data);
            var colors = TestMeshFactory.SolidColors(_mesh.vertexCount, new Color32(15, 45, 85, 125));
            data.ReplaceColors(colors);
            var result = PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out var sameData);
            Assert.That(result.IsValid, Is.False);
            Assert.That(sameData, Is.SameAs(data));
            Assert.That(data.SerializedColors, Is.EqualTo(colors));
        }

        [Test]
        public void ValidPreflightDoesNotApplyOrChangeAuthoritativeData()
        {
            PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out var data);
            var renderer = _object.GetComponent<MeshRenderer>();
            var stream = renderer.additionalVertexStreams;
            var json = EditorJsonUtility.ToJson(data);
            var dirtyBefore = _scene.isDirty;
            EditorUtility.ClearDirty(data);
            for (var i = 0; i < 3; i++) Assert.That(Inspect().CanPaint, Is.True);
            Assert.That(renderer.additionalVertexStreams, Is.SameAs(stream));
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(json));
            Assert.That(EditorUtility.IsDirty(data), Is.False);
            Assert.That(_scene.isDirty, Is.EqualTo(dirtyBefore));
        }

        [Test]
        public void TopologyMismatchIsReadOnlyAndBlocksPaintingAndSetup()
        {
            PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out var data);
            _mesh.SetTriangles(new[] { 0, 2, 1 }, 0);
            var json = EditorJsonUtility.ToJson(data);
            var target = Inspect();
            Assert.That(target.State, Is.EqualTo(PaintWorkspaceState.Invalid));
            Assert.That(target.CanPaint || target.CanPrepare, Is.False);
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(json));
        }

        [Test]
        public void OrphanedPaintIsNotReplacedBySetup()
        {
            var data = _object.AddComponent<VertexPaintData>();
            data.Configure(null, default, _mesh.colors32, VertexPaintStorageMode.Unresolved, VertexPaintStorageReason.None);
            Assert.That(Inspect().State, Is.EqualTo(PaintWorkspaceState.Invalid));
            Assert.That(PaintWorkspaceTarget.Prepare(new Object[] { _object }, false, out _).IsValid, Is.False);
            Assert.That(data.ColorCount, Is.EqualTo(_mesh.vertexCount));
        }

        [Test]
        public void ComponentSelectionResolvesToItsSceneObject()
        {
            var target = PaintWorkspaceTarget.Inspect(new Object[] { _object.GetComponent<MeshRenderer>() });
            Assert.That(target.CanPrepare, Is.True);
            Assert.That(target.Renderer.gameObject, Is.SameAs(_object));
        }

        [Test]
        public void ConstructingAndClosingWindowDoesNotActivateOrStealATool()
        {
            var previousTool = ToolManager.activeToolType;
            var settings = EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance);
            var window = ScriptableObject.CreateInstance<SimpleBlendWorkspace>();
            try
            {
                Assert.That(window.minSize.x, Is.EqualTo(320));
                Assert.That(window.titleContent.image, Is.SameAs(SimpleBlendEditorIcons.Load("Blend")));
                Assert.That(ToolManager.activeToolType, Is.EqualTo(previousTool));
                Assert.That(EditorJsonUtility.ToJson(SimpleBlendBrushSettings.instance), Is.EqualTo(settings));
            }
            finally { Object.DestroyImmediate(window); }
            Assert.That(ToolManager.activeToolType, Is.EqualTo(previousTool));
        }

        private PaintWorkspaceTarget Inspect() => PaintWorkspaceTarget.Inspect(new Object[] { _object });
    }
}
