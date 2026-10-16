using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class EditorIconTests
    {
        [TestCase("Blend")]
        [TestCase("Paint")]
        [TestCase("Sculpt")]
        [TestCase("Create")]
        [TestCase("Materials")]
        [TestCase("Output")]
        [TestCase("Terrain")]
        public void IconsHaveGuiImportSettingsAndTransparentOriginalPixels(string name)
        {
            var path = SimpleBlendEditorIcons.Folder + name + ".png";
            var texture = SimpleBlendEditorIcons.Load(name);
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(texture.width, Is.EqualTo(64));
            Assert.That(texture.height, Is.EqualTo(64));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.GUI));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

            var decoded = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(decoded, File.ReadAllBytes(path)), Is.True);
                var pixels = decoded.GetPixels32();
                Assert.That(pixels[0].a, Is.Zero, "Icon corners must remain transparent.");
                Assert.That(pixels.Any(pixel => pixel.a > 240 && pixel.b > pixel.r + 30), Is.True, "Blue family accent is missing.");
                Assert.That(pixels.Any(pixel => pixel.a > 240 && pixel.g > pixel.b + 30), Is.True, "Green family accent is missing.");
            }
            finally { Object.DestroyImmediate(decoded); }
        }

        [TestCase("Runtime/Data/VertexPaintData.cs", "Blend")]
        [TestCase("Editor/Painting/SimpleBlendPaintTool.cs", "Paint")]
        [TestCase("Editor/Terrain/TerrainBindingWindow.cs", "Terrain")]
        public void ScriptImportersUsePersistentPackageIcons(string scriptPath, string icon)
        {
            var importer = (MonoImporter)AssetImporter.GetAtPath("Packages/com.simpletools.simpleblend/" + scriptPath);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetIcon(), Is.SameAs(SimpleBlendEditorIcons.Load(icon)));
        }

        [Test]
        public void MissingIconRetainsTextAndTooltip()
        {
            var content = SimpleBlendEditorIcons.Content("Missing", "Paint", "Paint a surface");
            Assert.That(content.image, Is.Null);
            Assert.That(content.text, Is.EqualTo("Paint"));
            Assert.That(content.tooltip, Is.EqualTo("Paint a surface"));
        }

        [Test]
        public void TerrainWindowUsesFamilyIconWithoutOpeningUi()
        {
            var window = ScriptableObject.CreateInstance<TerrainBindingWindow>();
            try
            {
                Assert.That(window.titleContent.image, Is.SameAs(SimpleBlendEditorIcons.Load("Terrain")));
                Assert.That(window.titleContent.text, Is.EqualTo("SimpleBlend Terrain"));
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
