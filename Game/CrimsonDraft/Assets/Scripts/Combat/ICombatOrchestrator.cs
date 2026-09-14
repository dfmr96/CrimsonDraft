#nullable enable

namespace CrimsonDraft.Combat
{
    public interface ICombatOrchestrator
    {
        void EnqueueAction(PendingAction action);
        void SetWaitMode(bool paused);
        bool IsOperatorReady(int slotIndex);
        void NotifyShootCompleted();
        void NotifyMeleeCompleted();
        void ApplyMeleeCounterDamage(int operatorSlot, int enemySlot);
        void NotifyEnemyStaggered(int enemySlot);
        void MarkOperatorForFocusFire(int operatorSlot);
        void NotifyFocusFireCompleted();
    }
}
