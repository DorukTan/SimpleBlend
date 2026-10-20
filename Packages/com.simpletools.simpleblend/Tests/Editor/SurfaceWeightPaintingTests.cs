using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class SurfaceWeightPaintingTests
    {
        [TestCase(2)]
        [TestCase(3)]
        public void FullInfluenceReplacesThePreviousSurfaceAndPreservesFreeChannels(int channels)
        {
            var result = VertexSurfaceWeightUtility.Blend(new Color32(255, 0, 87, 42),
                new Color32(0, 255, 0, 255), VertexPaintChannel.Green, 1, channels, false);
            Assert.That(result.r, Is.Zero);
            Assert.That(result.g, Is.EqualTo(255));
            Assert.That(result.b, Is.EqualTo(channels == 2 ? 87 : 0));
            Assert.That(result.a, Is.EqualTo(42));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void PartialInfluenceTransfersWeightInsteadOfAddingAnotherFullMask(int channels)
        {
            var result = VertexSurfaceWeightUtility.Blend(new Color32(255, 0, 0, 42),
                new Color32(0, 255, 0, 0), VertexPaintChannel.Green, 0.5f, channels, false);
            Assert.That(result.r, Is.EqualTo(127).Within(1));
            Assert.That(result.g, Is.EqualTo(128).Within(1));
            Assert.That(result.r + result.g + result.b, Is.LessThanOrEqualTo(255));
            Assert.That(result.a, Is.EqualTo(42));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void BaseReducesActiveOverlaysButNeverWritesAlpha(int channels)
        {
            var result = VertexSurfaceWeightUtility.Blend(new Color32(64, 96, 40, 66),
                new Color32(), VertexPaintChannel.RGB, 1, channels, false);
            Assert.That(result.r, Is.Zero);
            Assert.That(result.g, Is.Zero);
            Assert.That(result.b, Is.EqualTo(channels == 2 ? 40 : 0));
            Assert.That(result.a, Is.EqualTo(66));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void EraseReducesOnlyTheSelectedOverlay(int channels)
        {
            var result = VertexSurfaceWeightUtility.Blend(new Color32(64, 96, 40, 66),
                new Color32(), VertexPaintChannel.Green, 1, channels, true);
            Assert.That(result, Is.EqualTo(new Color32(64, 0, 40, 66)));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void TouchedOversaturatedLegacyValuesBecomeBoundedWithoutChangingFreeChannels(int channels)
        {
            var result = VertexSurfaceWeightUtility.Blend(new Color32(255, 255, 255, 99),
                new Color32(255, 0, 0, 0), VertexPaintChannel.Red, 0.1f, channels, false);
            Assert.That(result.r + result.g + (channels == 3 ? result.b : 0), Is.LessThanOrEqualTo(255));
            Assert.That(result.a, Is.EqualTo(99));
            if (channels == 2) Assert.That(result.b, Is.EqualTo(255));
        }

        [Test]
        public void ResamplingPreservesExplicitSurfaceModeWhileLegacyCommandsRemainIndependent()
        {
            var command = new VertexBrushCommand(Vector3.zero, 1, 1, 1, new Color32(0, 255, 0, 0),
                null, VertexPaintBrushMode.Paint, VertexPaintChannel.Green, true, VertexBrushMask.None, 3);
            Assert.That(command.WithCenter(Vector3.one).SurfaceWeightChannels, Is.EqualTo(3));
            var legacy = new VertexBrushCommand(Vector3.zero, 1, 1, 1, new Color32(0, 255, 0, 0),
                null, VertexPaintBrushMode.Paint, VertexPaintChannel.Green, true, VertexBrushMask.None);
            Assert.That(legacy.SurfaceWeightChannels, Is.Zero);
            Assert.That(VertexColorChannelUtility.BlendSelected(new Color32(255, 0, 0, 42),
                legacy.Color, legacy.Channel, 1), Is.EqualTo(new Color32(255, 255, 0, 42)));
        }
    }
}
