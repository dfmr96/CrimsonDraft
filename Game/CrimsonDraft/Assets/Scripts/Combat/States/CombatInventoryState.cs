#nullable enable

namespace CrimsonDraft.Combat
{
    internal sealed class CombatInventoryState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICombatInventoryView view;

        internal CombatInventoryState(CombatMenuController context, ICombatInventoryView view)
        {
            this.context = context;
            this.view    = view;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            this.view.OnItemUsed   += HandleItemUsed;
            this.view.OnCancelled  += HandleCancelled;
            this.view.Show(this.context.SelectedOperator);
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
            this.view.OnItemUsed  -= HandleItemUsed;
            this.view.OnCancelled -= HandleCancelled;
        }

        private void HandleItemUsed(int slotIndex)
        {
            this.context.Orchestrator.EnqueueAction(
                PendingAction.UseItem(this.context.SelectedOperator, slotIndex));
            this.view.Hide();
            this.context.TransitionTo(this.context.OperatorSelState);
        }

        private void HandleCancelled()
        {
            this.view.Hide();
            this.context.TransitionTo(this.context.CommandPanelState);
        }
    }
}
