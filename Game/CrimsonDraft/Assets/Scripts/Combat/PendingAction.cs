#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.Combat
{
    public enum PendingActionType { Shoot, UseItem, EnemyAttack, EnemyRecover, Melee }

    public readonly struct PendingAction
    {
        public PendingActionType Type               { get; }
        public int               SlotIndex          { get; }
        public InventoryItem?    Item               { get; }
        public int               TargetOperatorSlot { get; }
        public int               Damage             { get; }

        private PendingAction(
            PendingActionType type,
            int slotIndex,
            InventoryItem? item     = null,
            int targetOperatorSlot = -1,
            int damage             = 0)
        {
            this.Type               = type;
            this.SlotIndex          = slotIndex;
            this.Item               = item;
            this.TargetOperatorSlot = targetOperatorSlot;
            this.Damage             = damage;
        }

        public static PendingAction Shoot(int operatorSlot) =>
            new PendingAction(PendingActionType.Shoot, operatorSlot);

        public static PendingAction UseItem(int operatorSlot, InventoryItem? item) =>
            new PendingAction(PendingActionType.UseItem, operatorSlot, item: item);

        public static PendingAction EnemyAttack(int enemySlot, int targetOperatorSlot, int damage) =>
            new PendingAction(PendingActionType.EnemyAttack, enemySlot,
                targetOperatorSlot: targetOperatorSlot, damage: damage);

        public static PendingAction EnemyRecover(int enemySlot) =>
            new PendingAction(PendingActionType.EnemyRecover, enemySlot);

        public static PendingAction Melee(int operatorSlot) =>
            new PendingAction(PendingActionType.Melee, operatorSlot);
    }
}
