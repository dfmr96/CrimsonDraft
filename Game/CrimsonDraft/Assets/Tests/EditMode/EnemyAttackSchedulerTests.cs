using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyAttackSchedulerTests
    {
        private sealed class FakeRandom : IRandomSource
        {
            private readonly float[] values;
            private int index;

            public FakeRandom(params float[] values)
            {
                this.values = values.Length == 0 ? new[] { 0f } : values;
            }

            public float NextFloat01()
            {
                float value = this.values[this.index % this.values.Length];
                this.index++;
                return Mathf.Clamp01(value);
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                float t = NextFloat01();
                return Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(minInclusive, maxExclusive, t)), minInclusive, maxExclusive - 1);
            }
        }

        [Test]
        public void TryScheduleAttack_doesNotAttackWhileLocked()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0f));
            scheduler.Initialize(
                new[] { new EnemyAttackConfig(0, 2f, 0f, 1f, 10) },
                now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(2f, new[] { 0 }, out var first));
            Assert.AreEqual(0, first.AttackerSlotIndex);
            Assert.IsFalse(scheduler.TryScheduleAttack(2.5f, new[] { 0 }, out _));
        }

        [Test]
        public void TryScheduleAttack_picksSoonestReadyEnemy()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0f, 0f));
            scheduler.Initialize(
                new[]
                {
                    new EnemyAttackConfig(0, 3f, 0f, 1f, 10),
                    new EnemyAttackConfig(1, 2f, 0f, 1f, 10),
                },
                now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(2f, new[] { 0 }, out var attack));
            Assert.AreEqual(1, attack.AttackerSlotIndex);
        }

        [Test]
        public void TryScheduleAttack_selectsRandomAliveOperator()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0.9f));
            scheduler.Initialize(
                new[] { new EnemyAttackConfig(0, 1f, 0f, 0.2f, 10) },
                now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(1f, new[] { 0, 2, 3 }, out var attack));
            Assert.AreEqual(3, attack.TargetOperatorSlotIndex);
        }

        [Test]
        public void TryScheduleAttack_recomputesNextAttackWithJitter()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(1f));
            scheduler.Initialize(
                new[] { new EnemyAttackConfig(0, 2f, 1f, 0.5f, 10) },
                now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(3f, new[] { 0 }, out var attack));
            Assert.AreEqual(6f, attack.NextAttackTime, 0.001f);
        }

        [Test]
        public void TryScheduleAttack_breaksTieRandomly()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0f, 1f));
            scheduler.Initialize(
                new[]
                {
                    new EnemyAttackConfig(0, 1f, 0f, 0.5f, 10),
                    new EnemyAttackConfig(1, 1f, 0f, 0.5f, 10),
                },
                now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(1f, new[] { 0 }, out var first));
            Assert.AreEqual(0, first.AttackerSlotIndex);

            Assert.IsTrue(scheduler.TryScheduleAttack(1.5f, new[] { 0 }, out var second));
            Assert.AreEqual(1, second.AttackerSlotIndex);
        }

        [Test]
        public void TryScheduleAttack_returnsFalseWithoutAliveOperators()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0f));
            scheduler.Initialize(
                new[] { new EnemyAttackConfig(0, 1f, 0f, 0.5f, 10) },
                now: 0f);

            Assert.IsFalse(scheduler.TryScheduleAttack(1f, System.Array.Empty<int>(), out _));
        }

        [Test]
        public void MarkDead_excludesEnemyFromScheduling()
        {
            var scheduler = new EnemyAttackScheduler(new FakeRandom(0f));
            scheduler.Initialize(
                new[] { new EnemyAttackConfig(0, 1f, 0f, 0.5f, 10) },
                now: 0f);
            scheduler.MarkDead(0);

            Assert.IsFalse(scheduler.TryScheduleAttack(1f, new[] { 0 }, out _));
        }
    }
}
