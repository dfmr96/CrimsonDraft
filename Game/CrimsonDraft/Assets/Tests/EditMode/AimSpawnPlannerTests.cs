#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class AimSpawnPlannerTests
    {
        private const float HalfExtent = 100f;

        private static AimSpawnConfig MakeConfig(
            float minOffsetRatio = 0.15f,
            float baseMaxOffsetRatio = 0.45f,
            float lowHpMaxOffsetRatio = 0.9f,
            float edgeZoneRatio = 0.75f,
            float baseEdgeSpawnChance = 0.1f,
            float lowHpEdgeSpawnChance = 0.6f,
            float minSeparationRatio = 0.1f,
            int maxRerollAttempts = 6)
        {
            var config = ScriptableObject.CreateInstance<AimSpawnConfig>();
            config.MinOffsetRatio        = minOffsetRatio;
            config.BaseMaxOffsetRatio    = baseMaxOffsetRatio;
            config.LowHpMaxOffsetRatio   = lowHpMaxOffsetRatio;
            config.EdgeZoneRatio         = edgeZoneRatio;
            config.BaseEdgeSpawnChance   = baseEdgeSpawnChance;
            config.LowHpEdgeSpawnChance  = lowHpEdgeSpawnChance;
            config.MinSeparationRatio    = minSeparationRatio;
            config.MaxRerollAttempts     = maxRerollAttempts;
            return config;
        }

        [Test]
        public void ComputeSignedOffset_fullHp_magnitudeStaysWithinBaseRange()
        {
            var config = MakeConfig();
            // edgeRoll=0.99 (never forced edge), magnitudeT=0.5 (midpoint), sign roll=1 (positive).
            var random = new QueueRandomSource(new[] { 0.99f, 0.5f }, new[] { 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, null, config, random);

            float expectedMagnitudeRatio = Mathf.Lerp(config.MinOffsetRatio, config.BaseMaxOffsetRatio, 0.5f);
            Assert.AreEqual(expectedMagnitudeRatio * HalfExtent, offset, 0.001f);
        }

        [Test]
        public void ComputeSignedOffset_zeroHp_usesLowHpMaxRatio()
        {
            var config = MakeConfig();
            // edgeRoll=0.99 (never forced edge), magnitudeT=1.0 (top of range), sign roll=1 (positive).
            var random = new QueueRandomSource(new[] { 0.99f, 1f }, new[] { 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 1f, null, config, random);

            Assert.AreEqual(config.LowHpMaxOffsetRatio * HalfExtent, offset, 0.001f);
        }

        [Test]
        public void ComputeSignedOffset_negativeSignRoll_producesNegativeOffset()
        {
            var config = MakeConfig();
            var random = new QueueRandomSource(new[] { 0.99f, 0.5f }, new[] { 0 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, null, config, random);

            Assert.Less(offset, 0f);
        }

        [Test]
        public void ComputeSignedOffset_neverBelowMinOffsetRatio()
        {
            var config = MakeConfig();
            // magnitudeT=0 -> lower bound of the range should be MinOffsetRatio, not zero.
            var random = new QueueRandomSource(new[] { 0.99f, 0f }, new[] { 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, null, config, random);

            Assert.AreEqual(config.MinOffsetRatio * HalfExtent, offset, 0.001f);
        }

        [Test]
        public void ComputeSignedOffset_forcedEdgeRoll_landsAtOrAboveEdgeFloor()
        {
            var config = MakeConfig();
            // edgeRoll=0.0 (< edgeChance, forces edge), magnitudeT=0 -> lower bound becomes the edge floor.
            var random = new QueueRandomSource(new[] { 0f, 0f }, new[] { 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, null, config, random);

            float edgeFloorRatio = config.BaseMaxOffsetRatio * config.EdgeZoneRatio;
            Assert.AreEqual(edgeFloorRatio * HalfExtent, offset, 0.001f);
        }

        [Test]
        public void ComputeSignedOffset_tooCloseToPrevious_rerollsUntilSeparated()
        {
            var config = MakeConfig();
            float previous = 30f; // 0.3 ratio

            // First attempt: edgeRoll=0.99, magnitudeT=0.3 -> ratio 0.3 -> offset 30 (too close, |30-30|=0 < separation).
            // Second attempt: edgeRoll=0.99, magnitudeT=1.0 -> ratio 0.9? wait base max is 0.45 at hp=0.
            var random = new QueueRandomSource(
                new[] { 0.99f, MagnitudeTFor(config, 0.3f), 0.99f, MagnitudeTFor(config, 0.44f) },
                new[] { 1, 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, previous, config, random);

            Assert.AreNotEqual(previous, offset);
            Assert.Greater(Mathf.Abs(offset - previous), config.MinSeparationRatio * HalfExtent - 0.01f);
        }

        [Test]
        public void ComputeSignedOffset_exhaustsRerolls_mirrorsPreviousOffset()
        {
            var config = MakeConfig(maxRerollAttempts: 3);
            float previous = 30f;
            float sameRatioT = MagnitudeTFor(config, 0.3f);

            // Every attempt redraws the exact same candidate (30), always too close to previous (30).
            var floats = new List<float>();
            var ints = new List<int>();
            for (int i = 0; i < config.MaxRerollAttempts; i++)
            {
                floats.Add(0.99f);
                floats.Add(sameRatioT);
                ints.Add(1);
            }
            var random = new QueueRandomSource(floats, ints);

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, previous, config, random);

            Assert.AreEqual(-previous, offset, 0.001f);
        }

        [Test]
        public void ComputeSignedOffset_noPreviousOffset_neverRerolls()
        {
            var config = MakeConfig();
            // Only one attempt's worth of rolls queued -- if the planner tried to reroll with no
            // previous offset to compare against, it would run out of queued values and fall back to 0.
            var random = new QueueRandomSource(new[] { 0.99f, 0.5f }, new[] { 1 });

            float offset = AimSpawnPlanner.ComputeSignedOffset(HalfExtent, 0f, null, config, random);

            Assert.Greater(offset, 0f);
        }

        [Test]
        public void ComputeSignedOffset_invalidHalfExtent_returnsZero()
        {
            var config = MakeConfig();
            var random = new QueueRandomSource(new[] { 0.5f, 0.5f }, new[] { 1 });

            Assert.AreEqual(0f, AimSpawnPlanner.ComputeSignedOffset(0f, 0f, null, config, random));
            Assert.AreEqual(0f, AimSpawnPlanner.ComputeSignedOffset(-10f, 0f, null, config, random));
        }

        // Back-solves the magnitudeT (the random value fed to Mathf.Lerp) needed to land on a
        // given target ratio within [MinOffsetRatio, BaseMaxOffsetRatio] at hp=0 (danger=0), so
        // tests can express intent as "I want ratio 0.3" instead of raw lerp fractions.
        private static float MagnitudeTFor(AimSpawnConfig config, float targetRatio)
        {
            float range = config.BaseMaxOffsetRatio - config.MinOffsetRatio;
            return range <= 0f ? 0f : (targetRatio - config.MinOffsetRatio) / range;
        }

        private sealed class QueueRandomSource : IRandomSource
        {
            private readonly Queue<float> floats;
            private readonly Queue<int>   ints;

            public QueueRandomSource(IEnumerable<float> floats, IEnumerable<int> ints)
            {
                this.floats = new Queue<float>(floats);
                this.ints   = new Queue<int>(ints);
            }

            public float NextFloat01() => this.floats.Count > 0 ? this.floats.Dequeue() : 0f;

            public int NextInt(int minInclusive, int maxExclusive) =>
                this.ints.Count > 0 ? this.ints.Dequeue() : minInclusive;
        }
    }
}
