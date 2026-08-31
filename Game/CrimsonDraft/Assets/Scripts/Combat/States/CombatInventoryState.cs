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
        }

        private void HandleItemUsed(int slotIndex)
        {
            this.context.Orchestrator.EnqueueAction(
                PendingAction.UseItem(this.context.SelectedOperator, slotIndex));
            this.view.Hide();

            // Only release the card's focus-lift here, on an actual commit — mirrors
            // Shoot/FocusFire, which also only release once the turn is spent. Cancelling
            // back to CommandPanelState (HandleCancelled below) deliberately leaves focus
            // untouched: releasing it there eased "Visual" back down over ~0.18s while
            // CommandPanelState.Enter() repositioned the command list from "Visual"'s
            // still-lifted position in the same frame, leaving the list floating ~liftAmount
            // px above the border once the card finished easing down underneath it.
            this.menuView.ReleaseOperatorFocus(this.context.SelectedOperator);
            this.context.TransitionTo(this.context.OperatorSelState);
        }

        private void HandleCancelled()
        {
            this.view.Hide();
            this.context.TransitionTo(this.context.CommandPanelState);
        }
    }
}
