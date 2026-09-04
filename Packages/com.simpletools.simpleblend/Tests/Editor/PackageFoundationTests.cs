using NUnit.Framework;
using SimpleBlend.Editor;
using UnityEditor.PackageManager;

namespace SimpleBlend.Editor.Tests
{
    public sealed class PackageFoundationTests
    {
        [Test]
        public void EmbeddedPackageHasExpectedIdentityAndAssemblyBoundaries()
        {
            var runtimeAssembly = typeof(RuntimeAssemblyMarker).Assembly;
            var editorAssembly = typeof(EditorAssemblyMarker).Assembly;
            var package = PackageInfo.FindForAssembly(runtimeAssembly);

            Assert.That(runtimeAssembly.GetName().Name, Is.EqualTo("SimpleBlend.Runtime"));
            Assert.That(editorAssembly.GetName().Name, Is.EqualTo("SimpleBlend.Editor"));
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.simpletools.simpleblend"));
            Assert.That(package.version, Is.EqualTo("0.4.0-beta.1"));
        }
    }
}
