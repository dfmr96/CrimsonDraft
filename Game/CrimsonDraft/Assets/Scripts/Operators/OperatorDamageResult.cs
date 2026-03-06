#nullable enable

namespace CrimsonDraft.Operators
{
    public readonly struct OperatorDamageResult
    {
        public int  SlotIndex     { get; }
        public int  DamageApplied { get; }
        public int  RemainingHp   { get; }
        public bool IsDead        { get; }

        public OperatorDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead)
        {
            this.SlotIndex     = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp   = remainingHp;
            this.IsDead        = isDead;
        }
    }
}
