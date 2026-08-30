using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class MeshAssetIdentity
    {
        internal static string GetStableIdentity(Mesh mesh)
        {
            if (mesh == null)
            {
                return string.Empty;
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out var guid, out long localId)
                ? $"{guid}:{localId}"
                : string.Empty;
        }
    }
}
