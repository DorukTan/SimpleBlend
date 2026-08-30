using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class RedBrushTests
    {
        [Test]
        public void Kernel_applies_radius_strength_and_falloff_to_red_only()
        {
            var positions = new[]
            {
                Vector3.zero,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(2f, 0f, 0f)
            };
            var candidates = new[] { 0, 1, 2 };
            var colors = new[]
            {
                new Color32(0, 11, 12, 13),
                new Color32(0, 21, 22, 23),
                new Color32(7, 31, 32, 33)
            };
            var command = new RedBrushCommand(
                Vector3.zero,
                1f,
                0.5f,
                new Color32(255, 200, 201, 202),
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var changed = RedVertexPaintKernel.Apply(command, positions, candidates, colors);

            Assert.That(changed, Is.EqualTo(2));
            Assert.That(colors[0], Is.EqualTo(new Color32(128, 11, 12, 13)));
            Assert.That(colors[1], Is.EqualTo(new Color32(64, 21, 22, 23)));
            Assert.That(colors[2], Is.EqualTo(new Color32(7, 31, 32, 33)));
        }

        [Test]
        public void Command_clamps_radius_strength_and_weight()
        {
            var command = new RedBrushCommand(
                Vector3.zero,
                0f,
                2f,
                new Color32(255, 0, 0, 255),
                AnimationCurve.Constant(0f, 1f, 2f));

            Assert.That(command.Radius, Is.GreaterThan(0f));
            Assert.That(command.Strength, Is.EqualTo(1f));
            Assert.That(command.EvaluateWeight(0f), Is.EqualTo(1f));
            Assert.That(command.EvaluateWeight(command.Radius + 0.01f), Is.Zero);
        }
    }
}
