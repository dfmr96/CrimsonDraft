#nullable enable

namespace CrimsonDraft.Combat
{
    public enum PendingActionType { Shoot, UseItem, EnemyAttack }

    public readonly struct PendingAction
    {
        public PendingActionType Type               { get; }
        public int               SlotIndex          { get; }
        public int               ItemIndex          { get; }
        public int               TargetOperatorSlot { get; }
        public int               Damage             { get; }

        private PendingAction(
            PendingActionType type,
            int slotIndex,
            int itemIndex          = -1,
            int targetOperatorSlot = -1,
            int damage             = 0)
        {
            this.Type               = type;
            this.SlotIndex          = slotIndex;
            this.ItemIndex          = itemIndex;
            this.TargetOperatorSlot = targetOperatorSlot;
            this.Damage             = damage;
        }

        public static PendingAction Shoot(int operatorSlot) =>
            new PendingAction(PendingActionType.Shoot, operatorSlot);

        public static PendingAction UseItem(int operatorSlot, int itemIndex) =>
            new PendingAction(PendingActionType.UseItem, operatorSlot, itemIndex: itemIndex);

        public static PendingAction EnemyAttack(int enemySlot, int targetOperatorSlot, int damage) =>
            new PendingAction(PendingActionType.EnemyAttack, enemySlot,
                targetOperatorSlot: targetOperatorSlot, damage: damage);
    }
}
