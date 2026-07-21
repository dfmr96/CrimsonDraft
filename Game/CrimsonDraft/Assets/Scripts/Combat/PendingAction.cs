#nullable enable

namespace CrimsonDraft.Combat
{
    public enum PendingActionType { Shoot, UseItem, EnemyAttack, EnemyRecover, FocusFire }

    public readonly struct PendingAction
    {
        public PendingActionType Type               { get; }
        public int               SlotIndex          { get; }
        public int               ItemIndex          { get; }
        public int               TargetOperatorSlot { get; }
        public int               Damage             { get; }
        public int[]             FocusFireParticipants { get; }

        private PendingAction(
            PendingActionType type,
            int slotIndex,
            int itemIndex          = -1,
            int targetOperatorSlot = -1,
            int damage             = 0,
            int[]? focusFireParticipants = null)
        {
            this.Type               = type;
            this.SlotIndex          = slotIndex;
            this.ItemIndex          = itemIndex;
            this.TargetOperatorSlot = targetOperatorSlot;
            this.Damage             = damage;
            this.FocusFireParticipants = focusFireParticipants ?? System.Array.Empty<int>();
        }

        public static PendingAction Shoot(int operatorSlot) =>
            new PendingAction(PendingActionType.Shoot, operatorSlot);

        public static PendingAction UseItem(int operatorSlot, int itemIndex) =>
            new PendingAction(PendingActionType.UseItem, operatorSlot, itemIndex: itemIndex);

        public static PendingAction EnemyAttack(int enemySlot, int targetOperatorSlot, int damage) =>
            new PendingAction(PendingActionType.EnemyAttack, enemySlot,
                targetOperatorSlot: targetOperatorSlot, damage: damage);

        public static PendingAction EnemyRecover(int enemySlot) =>
            new PendingAction(PendingActionType.EnemyRecover, enemySlot);

        public static PendingAction FocusFire(int triggerOperatorSlot, int[] participants) =>
            new PendingAction(PendingActionType.FocusFire, triggerOperatorSlot, focusFireParticipants: participants);
    }
}
