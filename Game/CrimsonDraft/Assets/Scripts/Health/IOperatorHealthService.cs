#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Health
{
    public readonly struct OperatorDamageResult
    {
        public int SlotIndex { get; }
        public int DamageApplied { get; }
        public int RemainingHp { get; }
        public bool IsDead { get; }

        public OperatorDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead)
        {
            this.SlotIndex = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp = remainingHp;
            this.IsDead = isDead;
        }
    }

    public interface IOperatorHealthService
    {
        void Initialize(int slotCount, int defaultHp);
        int GetHp(int slotIndex);
        int GetMaxHp(int slotIndex);
        float GetHpRatio(int slotIndex);
        bool IsAlive(int slotIndex);
        IReadOnlyList<int> GetAliveSlots();
        OperatorDamageResult ApplyDamage(int slotIndex, int damage);
    }
}
