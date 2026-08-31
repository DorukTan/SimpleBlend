using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class VertexBrushOperationTests
    {
        [TestCase(VertexPaintChannel.Red, 110, 20, 30, 40)]
        [TestCase(VertexPaintChannel.Green, 10, 120, 30, 40)]
        [TestCase(VertexPaintChannel.Blue, 10, 20, 130, 40)]
        [TestCase(VertexPaintChannel.Alpha, 10, 20, 30, 140)]
        [TestCase(VertexPaintChannel.RGB, 110, 120, 130, 40)]
        public void Paint_changes_only_the_selected_channels(
            VertexPaintChannel channel,
            byte red,
            byte green,
            byte blue,
            byte alpha)
        {
            var colors = new[] { new Color32(10, 20, 30, 40) };
            var command = Command(
                VertexPaintBrushMode.Paint,
                channel,
                new Color32(110, 120, 130, 140));

            var changed = ApplySingle(command, colors);

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(colors[0], Is.EqualTo(new Color32(red, green, blue, alpha)));
        }

        [Test]
        public void Strength_flow_and_falloff_combine_into_one_clamped_weight()
        {
            var colors = new[] { new Color32(0, 11, 12, 13) };
            var command = new VertexBrushCommand(
                Vector3.zero,
                2f,
                0.5f,
                0.5f,
                new Color32(255, 200, 201, 202),
                AnimationCurve.Linear(0f, 1f, 1f, 0f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);
            var positions = new[] { new Vector3(1f, 0f, 0f) };

            VertexPaintKernel.Apply(
                command,
                positions,
                UpNormals(1),
                new[] { 0 },
                EmptyTopology(1),
                colors);

            Assert.That(colors[0], Is.EqualTo(new Color32(32, 11, 12, 13)));
            Assert.That(command.EvaluateWeight(command.Radius + 0.01f), Is.Zero);
        }

        [TestCase(VertexPaintChannel.Red, 0, 20, 30, 40)]
        [TestCase(VertexPaintChannel.Green, 10, 0, 30, 40)]
        [TestCase(VertexPaintChannel.Blue, 10, 20, 0, 40)]
        [TestCase(VertexPaintChannel.Alpha, 10, 20, 30, 0)]
        [TestCase(VertexPaintChannel.RGB, 0, 0, 0, 40)]
        public void Erase_moves_only_selected_channels_to_zero(
            VertexPaintChannel channel,
            byte red,
            byte green,
            byte blue,
            byte alpha)
        {
            var colors = new[] { new Color32(10, 20, 30, 40) };

            ApplySingle(Command(VertexPaintBrushMode.Erase, channel, default), colors);

            Assert.That(colors[0], Is.EqualTo(new Color32(red, green, blue, alpha)));
        }

        [Test]
        public void Smooth_uses_immutable_one_ring_neighbors_and_never_crosses_disconnected_indices()
        {
            var topology = VertexPaintTopology.Create(
                6,
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 0 },
                1);
            var colors = new[]
            {
                Red(0), Red(90), Red(180), Red(255), Red(255), Red(255)
            };
            var command = Command(VertexPaintBrushMode.Smooth, VertexPaintChannel.Red, default);

            VertexPaintKernel.Apply(
                command,
                ZeroPositions(6),
                UpNormals(6),
                new[] { 0, 1 },
                topology,
                colors,
                true);

            Assert.That(colors[0].r, Is.EqualTo(135));
            Assert.That(colors[1].r, Is.EqualTo(90));
            Assert.That(colors[3].r, Is.EqualTo(255));
        }

        [Test]
        public void Submesh_fill_visits_shared_vertices_once()
        {
            var topology = QuadTopology();
            var colors = new[] { Red(0), Red(0), Red(0), Red(0) };
            var command = new VertexBrushCommand(
                Vector3.zero,
                1f,
                1f,
                0.5f,
                Red(200),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Fill,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);

            var changed = VertexPaintKernel.Apply(
                command,
                ZeroPositions(4),
                UpNormals(4),
                topology.GetSubmeshVertices(1),
                topology,
                colors,
                true);

            Assert.That(changed, Is.EqualTo(3));
            Assert.That(colors[0].r, Is.EqualTo(100));
            Assert.That(colors[1].r, Is.Zero);
            Assert.That(colors[2].r, Is.EqualTo(100));
            Assert.That(colors[3].r, Is.EqualTo(100));
        }

        [Test]
        public void Whole_mesh_fill_includes_vertices_not_referenced_by_triangles()
        {
            var topology = VertexPaintTopology.Create(
                5,
                new[] { 0, 1, 2, 0, 2, 3 },
                new[] { 0, 1 },
                2);
            var colors = new[] { Red(0), Red(0), Red(0), Red(0), Red(0) };
            var command = Command(VertexPaintBrushMode.Fill, VertexPaintChannel.Red, Red(255));

            var changed = VertexPaintKernel.Apply(
                command,
                ZeroPositions(5),
                UpNormals(5),
                topology.AllVertices,
                topology,
                colors,
                true);

            Assert.That(changed, Is.EqualTo(5));
            Assert.That(colors[4].r, Is.EqualTo(255));
            Assert.That(topology.GetSubmeshVertices(9), Is.Empty);
        }

        [Test]
        public void Normal_submesh_and_existing_value_masks_compose()
        {
            var topology = QuadTopology();
            var colors = new[] { Red(0), Red(128), Red(200), Red(255) };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.down };
            var mask = new VertexBrushMask(
                true,
                Vector3.up,
                10f,
                true,
                1,
                true,
                VertexPaintChannel.Red,
                0.5f,
                0.8f,
                true);
            var command = Command(VertexPaintBrushMode.Paint, VertexPaintChannel.Green, Green(255), mask);

            VertexPaintKernel.Apply(
                command,
                ZeroPositions(4),
                normals,
                topology.AllVertices,
                topology,
                colors,
                true);

            Assert.That(colors[0].g, Is.Zero);
            Assert.That(colors[1].g, Is.Zero, "Vertex 1 is not part of submesh 1.");
            Assert.That(colors[2].g, Is.EqualTo(255));
            Assert.That(colors[3].g, Is.Zero, "The downward normal must fail the normal mask.");
        }

        [Test]
        public void Threshold_bounds_are_explicitly_inclusive_or_exclusive()
        {
            var minimum = 64f / 255f;
            var maximum = 191f / 255f;
            var inclusiveColors = new[] { Red(64), Red(191) };
            var exclusiveColors = (Color32[])inclusiveColors.Clone();
            var inclusive = ThresholdCommand(minimum, maximum, true);
            var exclusive = ThresholdCommand(minimum, maximum, false);
            var topology = EmptyTopology(2);

            VertexPaintKernel.Apply(
                inclusive,
                ZeroPositions(2),
                UpNormals(2),
                topology.AllVertices,
                topology,
                inclusiveColors,
                true);
            VertexPaintKernel.Apply(
                exclusive,
                ZeroPositions(2),
                UpNormals(2),
                topology.AllVertices,
                topology,
                exclusiveColors,
                true);

            Assert.That(inclusiveColors[0].g, Is.EqualTo(255));
            Assert.That(inclusiveColors[1].g, Is.EqualTo(255));
            Assert.That(exclusiveColors[0].g, Is.Zero);
            Assert.That(exclusiveColors[1].g, Is.Zero);
        }

        [Test]
        public void Sample_interpolates_all_channels_barycentrically()
        {
            var colors = new[]
            {
                new Color32(0, 30, 60, 90),
                new Color32(120, 150, 180, 210),
                new Color32(240, 210, 180, 150)
            };

            var sampled = VertexPaintColorSampler.SampleTriangle(
                colors,
                new[] { 0, 1, 2 },
                0,
                new Vector3(0.25f, 0.5f, 0.25f));

            Assert.That(sampled, Is.EqualTo(new Color32(120, 135, 150, 165)));
        }

        [Test]
        public void Non_accumulating_stamps_use_the_strongest_weight_from_the_stroke_baseline()
        {
            var topology = EmptyTopology(1);
            var command = new VertexBrushCommand(
                Vector3.zero,
                1f,
                1f,
                0.5f,
                Red(255),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                false,
                VertexBrushMask.None);
            var colors = new[] { Red(0) };
            var baseline = (Color32[])colors.Clone();
            var strongestWeights = new float[1];

            ApplySingle(command, colors, topology, baseline, strongestWeights);
            ApplySingle(command, colors, topology, baseline, strongestWeights);

            Assert.That(colors[0].r, Is.EqualTo(128));
        }

        [Test]
        public void Accumulating_stamps_compound_at_deterministic_travel_samples()
        {
            var command = new VertexBrushCommand(
                Vector3.zero,
                1f,
                1f,
                0.5f,
                Red(255),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);
            var colors = new[] { Red(0) };

            ApplySingle(command, colors);
            ApplySingle(command, colors);

            Assert.That(colors[0].r, Is.EqualTo(192));
        }

        [Test]
        public void Travel_sampler_is_stable_when_the_same_path_has_different_event_counts()
        {
            var sparse = SamplePath(new[] { 0f, 1f });
            var dense = SamplePath(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f });

            Assert.That(dense.Count, Is.EqualTo(sparse.Count));
            for (var index = 0; index < sparse.Count; index++)
            {
                Assert.That(Vector3.Distance(dense[index], sparse[index]), Is.LessThan(0.00001f));
            }
        }

        [TestCase(VertexPaintBrushMode.Paint)]
        [TestCase(VertexPaintBrushMode.Erase)]
        [TestCase(VertexPaintBrushMode.Smooth)]
        [TestCase(VertexPaintBrushMode.Fill)]
        public void Empty_candidate_sets_leave_every_mode_unchanged(VertexPaintBrushMode mode)
        {
            var topology = VertexPaintTopology.Create(
                3,
                new[] { 0, 1, 2 },
                new[] { 0 },
                1);
            var colors = new[] { Red(10), Red(20), Red(30) };
            var before = (Color32[])colors.Clone();

            var changed = VertexPaintKernel.Apply(
                Command(mode, VertexPaintChannel.Red, Red(255)),
                ZeroPositions(3),
                UpNormals(3),
                new int[0],
                topology,
                colors,
                mode == VertexPaintBrushMode.Fill);

            Assert.That(changed, Is.Zero);
            Assert.That(colors, Is.EqualTo(before));
        }

        [Test]
        public void Zero_strength_or_flow_produces_no_change()
        {
            var colors = new[] { Red(10), Red(20) };
            var before = (Color32[])colors.Clone();
            var zeroStrength = new VertexBrushCommand(
                Vector3.zero,
                1f,
                0f,
                1f,
                Red(255),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);
            var zeroFlow = new VertexBrushCommand(
                Vector3.zero,
                1f,
                1f,
                0f,
                Red(255),
                AnimationCurve.Constant(0f, 1f, 1f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.Red,
                true,
                VertexBrushMask.None);

            Assert.That(ApplySingle(zeroStrength, colors), Is.Zero);
            Assert.That(ApplySingle(zeroFlow, colors), Is.Zero);
            Assert.That(colors, Is.EqualTo(before));
        }

        [Test]
        public void Fully_degenerate_triangle_has_no_self_neighbors_and_smooth_is_stable()
        {
            var topology = VertexPaintTopology.Create(
                1,
                new[] { 0, 0, 0 },
                new[] { 0 },
                1);
            var colors = new[] { Red(123) };

            var changed = ApplySingle(
                Command(VertexPaintBrushMode.Smooth, VertexPaintChannel.Red, default),
                colors,
                topology);

            Assert.That(topology.GetNeighbors(0), Is.Empty);
            Assert.That(changed, Is.Zero);
            Assert.That(colors[0].r, Is.EqualTo(123));
        }

        [Test]
        public void Normal_angle_boundary_is_inclusive()
        {
            var mask = new VertexBrushMask(
                true,
                Vector3.up,
                90f,
                false,
                0,
                false,
                VertexPaintChannel.Red,
                0f,
                1f,
                true);

            Assert.That(mask.Allows(0, Vector3.right, default, EmptyTopology(1)), Is.True);
            Assert.That(mask.Allows(0, Vector3.down, default, EmptyTopology(1)), Is.False);
        }

        private static int ApplySingle(
            VertexBrushCommand command,
            Color32[] colors,
            VertexPaintTopology topology = null,
            Color32[] baseline = null,
            float[] strongestWeights = null)
        {
            topology = topology ?? EmptyTopology(colors.Length);
            return VertexPaintKernel.Apply(
                command,
                ZeroPositions(colors.Length),
                UpNormals(colors.Length),
                topology.AllVertices,
                topology,
                colors,
                true,
                baseline,
                strongestWeights);
        }

        private static VertexBrushCommand Command(
            VertexPaintBrushMode mode,
            VertexPaintChannel channel,
            Color32 color,
            VertexBrushMask? mask = null)
        {
            return new VertexBrushCommand(
                Vector3.zero,
                1f,
                1f,
                1f,
                color,
                AnimationCurve.Constant(0f, 1f, 1f),
                mode,
                channel,
                true,
                mask ?? VertexBrushMask.None);
        }

        private static VertexBrushCommand ThresholdCommand(float minimum, float maximum, bool inclusive)
        {
            var mask = new VertexBrushMask(
                false,
                Vector3.up,
                180f,
                false,
                0,
                true,
                VertexPaintChannel.Red,
                minimum,
                maximum,
                inclusive);
            return Command(VertexPaintBrushMode.Paint, VertexPaintChannel.Green, Green(255), mask);
        }

        private static VertexPaintTopology QuadTopology()
        {
            return VertexPaintTopology.Create(
                4,
                new[] { 0, 1, 2, 0, 2, 3 },
                new[] { 0, 1 },
                2);
        }

        private static VertexPaintTopology EmptyTopology(int vertexCount)
        {
            return VertexPaintTopology.Create(vertexCount, new int[0], new int[0], 0);
        }

        private static Vector3[] ZeroPositions(int count)
        {
            return new Vector3[count];
        }

        private static Vector3[] UpNormals(int count)
        {
            var normals = new Vector3[count];
            for (var index = 0; index < count; index++)
            {
                normals[index] = Vector3.up;
            }

            return normals;
        }

        private static Color32 Red(byte value)
        {
            return new Color32(value, 0, 0, 0);
        }

        private static Color32 Green(byte value)
        {
            return new Color32(0, value, 0, 0);
        }

        private static List<Vector3> SamplePath(IReadOnlyList<float> inputs)
        {
            var sampler = new VertexStrokeSampler();
            var batch = new List<Vector3>();
            var all = new List<Vector3>();
            for (var index = 0; index < inputs.Count; index++)
            {
                sampler.Append(new Vector3(inputs[index], 0f, 0f), 1f, batch);
                all.AddRange(batch);
            }

            return all;
        }
    }
}
