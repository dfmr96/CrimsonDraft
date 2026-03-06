#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Health
{
    public sealed class OperatorHealthService : IOperatorHealthService
    {
        private int[] hpBySlot = System.Array.Empty<int>();
        private int[] maxHpBySlot = System.Array.Empty<int>();
        private bool[] aliveBySlot = System.Array.Empty<bool>();
        private readonly List<int> aliveSlotsScratch = new();

        public void Initialize(int slotCount, int defaultHp)
        {
            int count = Mathf.Max(0, slotCount);
            int clampedDefaultHp = Mathf.Max(0, defaultHp);

            this.hpBySlot = new int[count];
            this.maxHpBySlot = new int[count];
            this.aliveBySlot = new bool[count];
            for (int i = 0; i < count; i++)
            {
                this.hpBySlot[i] = clampedDefaultHp;
                this.maxHpBySlot[i] = clampedDefaultHp;
                this.aliveBySlot[i] = clampedDefaultHp > 0;
            }
        }

        public int GetHp(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.hpBySlot.Length)
                return 0;

            return this.hpBySlot[slotIndex];
        }

        public bool IsAlive(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.aliveBySlot.Length)
                return false;

            return this.aliveBySlot[slotIndex];
        }

        public int GetMaxHp(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.maxHpBySlot.Length)
                return 0;

            return this.maxHpBySlot[slotIndex];
        }

        public float GetHpRatio(int slotIndex)
        {
            int maxHp = GetMaxHp(slotIndex);
            if (maxHp <= 0)
                return 0f;

            return Mathf.Clamp01((float)GetHp(slotIndex) / maxHp);
        }

        public IReadOnlyList<int> GetAliveSlots()
        {
            this.aliveSlotsScratch.Clear();
            for (int i = 0; i < this.aliveBySlot.Length; i++)
            {
                if (this.aliveBySlot[i])
                    this.aliveSlotsScratch.Add(i);
            }

            return this.aliveSlotsScratch;
        }

        public OperatorDamageResult ApplyDamage(int slotIndex, int damage)
        {
            if (slotIndex < 0 || slotIndex >= this.hpBySlot.Length)
                return new OperatorDamageResult(slotIndex, 0, 0, true);

            if (!this.aliveBySlot[slotIndex])
                return new OperatorDamageResult(slotIndex, 0, 0, true);

            int applied = Mathf.Max(0, damage);
            int nextHp = Mathf.Max(0, this.hpBySlot[slotIndex] - applied);
            this.hpBySlot[slotIndex] = nextHp;

            bool dead = nextHp <= 0;
            if (dead)
                this.aliveBySlot[slotIndex] = false;

            return new OperatorDamageResult(slotIndex, applied, nextHp, dead);
        }
    }
}
