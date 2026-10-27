#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.Combat
{
    internal sealed class CombatInventoryState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ICombatInventoryView  view;
        private readonly ICombatActionMenuView menuView;
        private bool active;

        internal CombatInventoryState(CombatMenuController context, ICombatInventoryView view, ICombatActionMenuView menuView)
        {
            this.context  = context;
            this.view     = view;
            this.menuView = menuView;
        }

        public void Enter()
        {
            this.active = true;
            this.context.Orchestrator.SetWaitMode(true);
            this.view.OnItemUsed   += HandleItemUsed;
            this.view.OnCancelled  += HandleCancelled;

            // The acting card drops its focus-lift so it doesn't sit higher than its neighbours.
            this.menuView.ReleaseOperatorFocus(this.context.SelectedOperator);

            // The operator strip grows taller first; only once it reaches full height do the inventories
            // appear underneath it, as if it were the inventory tab. Only the acting operator's one is interactive.
            int slot = this.context.SelectedOperator;
            this.menuView.SetInventoryLift(true, () => this.view.Show(slot, this.menuView.GetOperatorOverviewRects()));

            // The items grid has its own cursor — the roster/command selector box would
            // otherwise just sit frozen at whatever size/position it last had (the "Items"
            // row) for the whole time we're in here. Re-shown automatically once we leave:
            // OperatorSelState/CommandPanelState both re-focus and call MoveSelectorTo on Enter.
            this.menuView.ClearFocus();
        }

        public void Exit()
        {
            this.active = false;
            this.context.Orchestrator.SetWaitMode(false);
            this.view.OnItemUsed  -= HandleItemUsed;
            this.view.OnCancelled -= HandleCancelled;

            // No-op when already lowered; stops the grow (and its pending inventory reveal) if we're interrupted mid-way.
            this.menuView.SetInventoryLift(false);
        }

        private void HandleItemUsed(InventoryItem? item, int targetSlot)
        {
            this.context.Orchestrator.EnqueueAction(
                PendingAction.UseItem(this.context.SelectedOperator, item, targetSlot));

            // Only release the card's focus-lift here, on an actual commit — mirrors
            // Shoot/Melee, which also only release once the turn is spent. Cancelling
            // back to CommandPanelState (HandleCancelled below) deliberately leaves focus
            // untouched: releasing it there eased "Visual" back down over ~0.18s while
            // CommandPanelState.Enter() repositioned the command list from "Visual"'s
            // still-lifted position in the same frame, leaving the list floating ~liftAmount
            // px above the border once the card finished easing down underneath it.
            this.menuView.ReleaseOperatorFocus(this.context.SelectedOperator);
            CloseThen(() => this.context.TransitionTo(this.context.OperatorSelState));
        }

        private void HandleCancelled()
        {
            CloseThen(() =>
            {
                this.menuView.RestoreOperatorFocus(this.context.SelectedOperator);
                this.context.TransitionTo(this.context.CommandPanelState);
            });
        }

        // Closing mirrors opening: the inventories shrink away first, then the operator strip shrinks back,
        // and only then does the next state take over. 'active' drops stale callbacks if we were interrupted.
        private void CloseThen(System.Action next)
        {
            this.view.Close(() =>
            {
                if (!this.active) return;
                this.menuView.SetInventoryLift(false, () =>
                {
                    if (this.active) next();
                });
            });
        }
    }
}
