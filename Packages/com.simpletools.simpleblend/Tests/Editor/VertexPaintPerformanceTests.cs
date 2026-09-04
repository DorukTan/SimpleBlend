using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class VertexPaintPerformanceTests
    {
        private const int SampleCount = 250;
        private const double MedianBudgetMilliseconds = 8d;
        private const double LongSampleMilliseconds = 16d;

        [Test]
        public void Forty_thousand_vertex_drag_meets_managed_time_and_allocation_budget()
        {
            var cacheStart = Stopwatch.GetTimestamp();
            var fixture = FortyThousandVertexFixture.Create();
            Assert.That(fixture.Checksum, Is.EqualTo(374687139120000L));

            var grid = new UniformVertexGrid();
            grid.Rebuild(fixture.Positions, 0.4f);
            var cacheBuildMilliseconds = TicksToMilliseconds(Stopwatch.GetTimestamp() - cacheStart);
            var candidates = new List<int>(1024);
            var colors = new Color32[fixture.Positions.Length];
            var command = new VertexBrushCommand(
                Vector3.zero,
                1.25f,
                0.8f,
                0.4f,
                new Color32(255, 64, 0, 255),
                AnimationCurve.Linear(0f, 1f, 1f, 0f),
                VertexPaintBrushMode.Paint,
                VertexPaintChannel.RGB,
                true,
                VertexBrushMask.None);

            WarmUp(fixture, grid, candidates, colors, command);

            var queryTicks = new long[SampleCount];
            var kernelTicks = new long[SampleCount];
            var totalTicks = new long[SampleCount];
            for (var sample = 0; sample < SampleCount; sample++)
            {
                var center = SampleCenter(sample);
                var queryStart = Stopwatch.GetTimestamp();
                grid.QuerySphere(center, command.Radius, candidates);
                var queryEnd = Stopwatch.GetTimestamp();
                VertexPaintKernel.Apply(
                    command.WithCenter(center),
                    fixture.Positions,
                    fixture.Normals,
                    candidates,
                    fixture.Topology,
                    colors);
                var kernelEnd = Stopwatch.GetTimestamp();
                queryTicks[sample] = queryEnd - queryStart;
                kernelTicks[sample] = kernelEnd - queryEnd;
                totalTicks[sample] = kernelEnd - queryStart;
            }

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0; sample < SampleCount; sample++)
            {
                var center = SampleCenter(sample);
                grid.QuerySphere(center, command.Radius, candidates);
                VertexPaintKernel.Apply(
                    command.WithCenter(center),
                    fixture.Positions,
                    fixture.Normals,
                    candidates,
                    fixture.Topology,
                    colors);
            }

            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var queryMedian = MedianMilliseconds(queryTicks);
            var kernelMedian = MedianMilliseconds(kernelTicks);
            var totalMedian = MedianMilliseconds(totalTicks);
            var maximum = MaximumMilliseconds(totalTicks);
            var longSamples = CountOverBudget(totalTicks, LongSampleMilliseconds);

            TestContext.WriteLine(
                $"SimpleBlend 40k fixture: one-time cache build {cacheBuildMilliseconds:F3} ms; " +
                $"query median {queryMedian:F3} ms; " +
                $"kernel median {kernelMedian:F3} ms; total median {totalMedian:F3} ms; " +
                $"maximum {maximum:F3} ms; samples >16 ms {longSamples}/{SampleCount}; " +
                $"managed allocation {allocatedBytes} bytes.");

            Assert.That(totalMedian, Is.LessThan(MedianBudgetMilliseconds));
            Assert.That(longSamples, Is.LessThanOrEqualTo(2),
                "More than one percent of drag samples exceeded the 16 ms frame budget.");
            Assert.That(allocatedBytes, Is.Zero,
                "The warmed drag path must not create recurring managed allocations.");
        }

        private static void WarmUp(
            FortyThousandVertexFixture fixture,
            UniformVertexGrid grid,
            List<int> candidates,
            Color32[] colors,
            VertexBrushCommand command)
        {
            for (var sample = 0; sample < 20; sample++)
            {
                var center = SampleCenter(sample);
                grid.QuerySphere(center, command.Radius, candidates);
                VertexPaintKernel.Apply(
                    command.WithCenter(center),
                    fixture.Positions,
                    fixture.Normals,
                    candidates,
                    fixture.Topology,
                    colors);
            }
        }

        private static Vector3 SampleCenter(int sample)
        {
            var phase = sample / (float)SampleCount;
            return new Vector3(
                2f + phase * 15.8f,
                0f,
                9.95f + Mathf.Sin(phase * Mathf.PI * 4f) * 4f);
        }

        private static double MedianMilliseconds(long[] ticks)
        {
            var sorted = (long[])ticks.Clone();
            Array.Sort(sorted);
            return TicksToMilliseconds(sorted[sorted.Length / 2]);
        }

        private static double MaximumMilliseconds(long[] ticks)
        {
            long maximum = 0;
            for (var index = 0; index < ticks.Length; index++)
            {
                maximum = Math.Max(maximum, ticks[index]);
            }

            return TicksToMilliseconds(maximum);
        }

        private static int CountOverBudget(long[] ticks, double milliseconds)
        {
            var budgetTicks = milliseconds * Stopwatch.Frequency / 1000d;
            var count = 0;
            for (var index = 0; index < ticks.Length; index++)
            {
                if (ticks[index] > budgetTicks)
                {
                    count++;
                }
            }

            return count;
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }

        private sealed class FortyThousandVertexFixture
        {
            private FortyThousandVertexFixture(
                Vector3[] positions,
                Vector3[] normals,
                VertexPaintTopology topology,
                long checksum)
            {
                Positions = positions;
                Normals = normals;
                Topology = topology;
                Checksum = checksum;
            }

            internal Vector3[] Positions { get; }

            internal Vector3[] Normals { get; }

            internal VertexPaintTopology Topology { get; }

            internal long Checksum { get; }

            internal static FortyThousandVertexFixture Create()
            {
                const int width = 200;
                const float spacing = 0.1f;
                var positions = new Vector3[width * width];
                var normals = new Vector3[positions.Length];
                long checksum = 0;
                for (var y = 0; y < width; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var index = y * width + x;
                        positions[index] = new Vector3(x * spacing, 0f, y * spacing);
                        normals[index] = Vector3.up;
                        checksum += (long)(x + 1) * 73856093L + (long)(y + 1) * 19349663L;
                    }
                }

                var triangleCount = (width - 1) * (width - 1) * 2;
                var triangles = new int[triangleCount * 3];
                var submeshIds = new int[triangleCount];
                var triangleOffset = 0;
                for (var y = 0; y < width - 1; y++)
                {
                    for (var x = 0; x < width - 1; x++)
                    {
                        var first = y * width + x;
                        var second = first + 1;
                        var third = first + width;
                        var fourth = third + 1;
                        triangles[triangleOffset++] = first;
                        triangles[triangleOffset++] = third;
                        triangles[triangleOffset++] = second;
                        triangles[triangleOffset++] = second;
                        triangles[triangleOffset++] = third;
                        triangles[triangleOffset++] = fourth;
                    }
                }

                return new FortyThousandVertexFixture(
                    positions,
                    normals,
                    VertexPaintTopology.Create(positions.Length, triangles, submeshIds, 1),
                    checksum);
            }
        }
    }
}
