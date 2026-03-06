using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Combat;
using CrimsonDraft.Health;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    public sealed class CombatOperatorHealthBridgeTests
    {
        private sealed class FakeOperatorHealthService : IOperatorHealthService
        {
            private int[] hpBySlot = System.Array.Empty<int>();
            private bool[] aliveBySlot = System.Array.Empty<bool>();
            private readonly List<int> scratch = new();

            public void Initialize(int slotCount, int defaultHp)
            {
                this.hpBySlot = new int[slotCount];
                this.aliveBySlot = new bool[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    this.hpBySlot[i] = defaultHp;
                    this.aliveBySlot[i] = defaultHp > 0;
                }
            }

            public int GetHp(int slotIndex) =>
                slotIndex >= 0 && slotIndex < this.hpBySlot.Length ? this.hpBySlot[slotIndex] : 0;

            public int GetMaxHp(int slotIndex) =>
                slotIndex >= 0 && slotIndex < this.hpBySlot.Length ? 100 : 0;

            public float GetHpRatio(int slotIndex)
            {
                int maxHp = GetMaxHp(slotIndex);
                if (maxHp <= 0)
                    return 0f;
                return Mathf.Clamp01((float)GetHp(slotIndex) / maxHp);
            }

            public bool IsAlive(int slotIndex) =>
                slotIndex >= 0 && slotIndex < this.aliveBySlot.Length && this.aliveBySlot[slotIndex];

            public IReadOnlyList<int> GetAliveSlots()
            {
                this.scratch.Clear();
                for (int i = 0; i < this.aliveBySlot.Length; i++)
                {
                    if (this.aliveBySlot[i])
                        this.scratch.Add(i);
                }

                return this.scratch;
            }

            public OperatorDamageResult ApplyDamage(int slotIndex, int damage)
            {
                if (slotIndex < 0 || slotIndex >= this.hpBySlot.Length)
                    return new OperatorDamageResult(slotIndex, 0, 0, true);

                if (!this.aliveBySlot[slotIndex])
                    return new OperatorDamageResult(slotIndex, 0, 0, true);

                int applied = UnityEngine.Mathf.Max(0, damage);
                int nextHp = UnityEngine.Mathf.Max(0, this.hpBySlot[slotIndex] - applied);
                this.hpBySlot[slotIndex] = nextHp;
                this.aliveBySlot[slotIndex] = nextHp > 0;
                return new OperatorDamageResult(slotIndex, applied, nextHp, !this.aliveBySlot[slotIndex]);
            }
        }

        [Test]
        public void ApplyDamage_clampsToZero_andMarksDead()
        {
            var bridge = new CombatOperatorHealthBridge(new FakeOperatorHealthService());
            bridge.Initialize(3, defaultHp: 100);

            var result = bridge.ApplyDamage(1, 150);

            Assert.AreEqual(0, result.RemainingHp);
            Assert.IsTrue(result.IsDead);
            Assert.IsFalse(bridge.IsAlive(1));
        }

        [Test]
        public void GetAliveSlots_excludesDeadOperators()
        {
            var bridge = new CombatOperatorHealthBridge(new FakeOperatorHealthService());
            bridge.Initialize(3, defaultHp: 100);
            bridge.ApplyDamage(0, 100);

            var alive = bridge.GetAliveSlots();
            Assert.AreEqual(2, alive.Count);
            CollectionAssert.DoesNotContain(alive, 0);
        }
    }
}
