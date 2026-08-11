#nullable enable

namespace CrimsonDraft.Combat
{
    internal sealed class CombatInventoryState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ICombatInventoryView  view;
        private readonly ICombatActionMenuView menuView;

        internal CombatInventoryState(CombatMenuController context, ICombatInventoryView view, ICombatActionMenuView menuView)
        {
            this.context  = context;
            this.view     = view;
            this.menuView = menuView;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            this.view.OnItemUsed   += HandleItemUsed;
            this.view.OnCancelled  += HandleCancelled;
            this.view.Show(this.context.SelectedOperator, this.menuView.GetOperatorOverviewRect(this.context.SelectedOperator));

            // The items grid has its own cursor — the roster/command selector box would
            // otherwise just sit frozen at whatever size/position it last had (the "Items"
            // row) for the whole time we're in here. Re-shown automatically once we leave:
            // OperatorSelState/CommandPanelState both re-focus and call MoveSelectorTo on Enter.
            this.menuView.ClearFocus();
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
            this.view.OnItemUsed  -= HandleItemUsed;
            this.view.OnCancelled -= HandleCancelled;
            this.menuView.ReleaseOperatorFocus(this.context.SelectedOperator);
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
