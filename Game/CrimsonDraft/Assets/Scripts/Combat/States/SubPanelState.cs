#nullable enable

using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class SubPanelState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ISubPanelView         subPanel;
        private readonly IInventoryService     inventory;
        private readonly IOperatorRoster       roster;
        private readonly ICombatActionMenuView menuView;

        internal SubPanelState(
            CombatMenuController  context,
            ISubPanelView         subPanel,
            IInventoryService     inventory,
            IOperatorRoster       roster,
            ICombatActionMenuView menuView)
        {
            this.context   = context;
            this.subPanel  = subPanel;
            this.inventory = inventory;
            this.roster    = roster;
            this.menuView  = menuView;
        }

        public void OnCancel()
        {
            this.subPanel.Hide();
            this.context.TransitionTo(this.context.CommandPanelState);
        }

        public void OnItemSelected(int index)
        {
            int[] indices = this.context.ReloadAmmoBoxIndices;
            if (index >= indices.Length) return; // "NO AMMO" selected — do nothing

            int op = this.context.SelectedOperator;
            this.inventory.ReloadOperator(indices[index], op);

            var weapon = this.roster.Count > op ? this.roster[op].EquippedWeapon : null;
            this.menuView.SetOperatorAmmo(op, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);

            this.subPanel.Hide();
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
