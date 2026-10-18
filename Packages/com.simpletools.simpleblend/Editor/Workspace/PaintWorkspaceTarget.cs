using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleBlend.Editor
{
    internal enum PaintWorkspaceState { Empty, Mixed, Unsupported, NeedsSetup, Invalid, Ready }

    internal sealed class PaintWorkspaceTarget
    {
        internal PaintWorkspaceState State { get; private set; }
        internal string Message { get; private set; }
        internal Renderer Renderer { get; private set; }
        internal VertexPaintData Data { get; private set; }
        internal Mesh Mesh { get; private set; }
        internal bool CanPrepare => State == PaintWorkspaceState.NeedsSetup;
        internal bool CanPaint => State == PaintWorkspaceState.Ready;
        internal bool IsSparse => Mesh != null && Mesh.vertexCount <= 64;

        // Selection preflight is deliberately read-only. In particular, never call Apply or Enable here.
        internal static PaintWorkspaceTarget Inspect(Object[] selection)
        {
            var result = new PaintWorkspaceTarget();
            if (selection == null || selection.Length == 0)
                return result.With(PaintWorkspaceState.Empty, "Select one mesh object in the Hierarchy to begin.");
            if (selection.Length != 1)
                return result.With(PaintWorkspaceState.Mixed, "Select one object at a time. Nothing has been changed on this selection.");
            if (selection[0] == null)
                return result.With(PaintWorkspaceState.Empty, "The target is unavailable. Select a scene mesh object to continue.");
            var gameObject = selection[0] as GameObject;
            if (selection[0] is Component component) gameObject = component.gameObject;
            if (gameObject == null)
                return result.With(PaintWorkspaceState.Unsupported, "Select a scene mesh object, not a mesh or material asset.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return result.With(PaintWorkspaceState.Unsupported, "Leave Play Mode before setting up or painting an object.");
            if (EditorUtility.IsPersistent(gameObject) || !gameObject.scene.IsValid())
                return result.With(PaintWorkspaceState.Unsupported, "Open the prefab in Prefab Mode or select a scene instance. The prefab asset is unchanged.");
            if ((gameObject.hideFlags & HideFlags.NotEditable) != 0 || !gameObject.activeInHierarchy)
                return result.With(PaintWorkspaceState.Unsupported, "Select an active, editable scene object.");
            result.Renderer = gameObject.GetComponent<Renderer>();
            result.Data = gameObject.GetComponent<VertexPaintData>();
            if (!(result.Renderer is MeshRenderer) && !(result.Renderer is SkinnedMeshRenderer))
                return result.With(PaintWorkspaceState.Unsupported, "This object needs a Mesh Renderer or Skinned Mesh Renderer.");
            var filter = gameObject.GetComponent<MeshFilter>();
            result.Mesh = result.Renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter == null ? null : filter.sharedMesh;
            if (result.Mesh == null)
                return result.With(PaintWorkspaceState.Unsupported, "Assign a mesh to this object's Mesh Filter or Skinned Mesh Renderer first.");
            long indices = 0;
            for (var section = 0; section < result.Mesh.subMeshCount; section++)
            {
                if (result.Mesh.GetTopology(section) != MeshTopology.Triangles)
                    return result.With(PaintWorkspaceState.Unsupported, "Painting needs triangle geometry in every mesh section. Lines and points cannot be painted.");
                indices += result.Mesh.GetIndexCount(section);
            }
            if (indices == 0)
                return result.With(PaintWorkspaceState.Unsupported, "This mesh has no triangles to paint.");
            if ((result.Renderer.hideFlags & HideFlags.NotEditable) != 0
                || (filter != null && (filter.hideFlags & HideFlags.NotEditable) != 0)
                || (result.Data != null && (!result.Data.enabled || (result.Data.hideFlags & HideFlags.NotEditable) != 0)))
                return result.With(PaintWorkspaceState.Unsupported, "Enable the paint component and make the renderer and mesh components editable first.");
            if (result.Data == null || result.Data.SourceMesh == null)
            {
                if (result.Data != null && result.Data.ColorCount != 0)
                    return result.With(PaintWorkspaceState.Invalid, "Saved paint has lost its source mesh. Inspect the component to recover it; setup will not replace this paint.");
                var signature = MeshSignatureFactory.TryCreate(result.Mesh, MeshAssetIdentity.GetStableIdentity(result.Mesh), out _);
                return signature.IsValid
                    ? result.With(PaintWorkspaceState.NeedsSetup, "Set up painting for this object. Its original mesh and materials stay unchanged.")
                    : result.With(PaintWorkspaceState.Unsupported, signature.Message);
            }
            var validation = VertexPaintValidator.Validate(result.Data, MeshAssetIdentity.GetStableIdentity(result.Data.SourceMesh));
            result.Mesh = result.Data.SourceMesh;
            if (!validation.IsValid) return result.With(PaintWorkspaceState.Invalid, validation.Message);
            if (!SimpleBlendPaintTool.IsSupportedTarget(result.Data))
                return result.With(PaintWorkspaceState.Invalid, result.Data.ValidationMessage);
            return result.With(PaintWorkspaceState.Ready, "Ready to paint. Start painting, then drag over this object in the Scene view.");
        }

        internal static VertexPaintValidationResult Prepare(Object[] selection, bool? gpuResidentDrawerEnabled, out VertexPaintData data)
        {
            var current = Inspect(selection);
            data = current.Data;
            if (!current.CanPrepare)
                return VertexPaintValidationResult.Invalid(VertexPaintValidationFailure.MissingSourceMesh,
                    current.CanPaint ? "This object already has paint data. Setup will not replace it." : current.Message);
            return VertexPaintSetupUtility.Enable(current.Renderer, gpuResidentDrawerEnabled, out data);
        }

        private PaintWorkspaceTarget With(PaintWorkspaceState state, string message)
        {
            State = state;
            Message = message;
            return this;
        }
    }
}
