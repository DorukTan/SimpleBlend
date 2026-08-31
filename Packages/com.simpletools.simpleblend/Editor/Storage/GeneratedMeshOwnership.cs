using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class GeneratedMeshOwnership
    {
        internal static bool RequiresFork(VertexPaintData data)
        {
            if (data == null
                || data.StorageMode != VertexPaintStorageMode.ClonedMesh
                || data.FallbackMesh == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(data.FallbackOwnerId))
            {
                return true;
            }

            var prefabStage = PrefabStageUtility.GetPrefabStage(data.gameObject);
            var sourceData = PrefabUtility.GetCorrespondingObjectFromSource(data);
            if (sourceData != null && sourceData.FallbackMesh == data.FallbackMesh)
            {
                if (prefabStage == null)
                {
                    return true;
                }

                var sourceAssetPath = AssetDatabase.GetAssetPath(sourceData);
                if (!string.Equals(
                        sourceAssetPath,
                        prefabStage.assetPath,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var loadedData = Resources.FindObjectsOfTypeAll<VertexPaintData>();
            for (var index = 0; index < loadedData.Length; index++)
            {
                var other = loadedData[index];
                if (other == null
                    || other == data
                    || !other.gameObject.scene.IsValid()
                    || other.gameObject.scene != data.gameObject.scene)
                {
                    continue;
                }

                if (other.FallbackMesh == data.FallbackMesh)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
