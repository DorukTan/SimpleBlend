using System;
using UnityEditor;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TestAssetScope : IDisposable
    {
        internal TestAssetScope()
        {
            var folderName = $"__SimpleBlendTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", folderName);
            AssetPath = $"Assets/{folderName}";
        }

        internal string AssetPath { get; }

        public void Dispose()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.Refresh();
        }
    }
}
