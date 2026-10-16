using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor
{
    internal static class SimpleBlendEditorIcons
    {
        internal const string Folder = "Packages/com.simpletools.simpleblend/Editor/Icons/";

        internal static Texture2D Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name + ".png");
        }

        internal static GUIContent Content(string name, string label, string tooltip)
        {
            return new GUIContent(label, Load(name), tooltip);
        }
    }
}
