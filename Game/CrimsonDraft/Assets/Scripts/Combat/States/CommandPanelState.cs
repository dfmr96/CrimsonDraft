#nullable enable

using UnityEngine;
using CrimsonDraft.Audio;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class CommandPanelState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ICombatActionMenuView menuView;
        private readonly ICommandPanelView     commandPanel;
        private readonly IBattlefieldView      battlefieldView;
        private readonly IOperatorRoster       roster;
        private readonly CombatSfxData?        sfx;

        internal CommandPanelState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            IBattlefieldView      battlefieldView,
            IOperatorRoster       roster,
            CombatSfxData?        sfx = null)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.roster          = roster;
            this.sfx             = sfx;
        }

        public void Enter()
        {
            this.context.SuppressNextCommandFocusSfx();
            this.commandPanel.Show(this.menuView.GetOperatorOverviewRect(this.context.SelectedOperator));
            this.commandPanel.SetDimmed(false);
            this.commandPanel.Focus();
        }

        public void OnCancel()
        {
            this.sfx?.PlayCancel(this.commandPanel.PanelRect.gameObject);
            this.commandPanel.Hide();
            this.context.TransitionTo(this.context.OperatorSelState);
        }

        public void OnCommandSelected(CombatCommand command)
        {
            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.Items)
            {
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.commandPanel.Hide();
                this.context.TransitionTo(this.context.CombatInventoryState);
            }
        }

        private int GetMaxAvailableShotCount()
        {
            int op = this.context.SelectedOperator;
            if (this.roster.Count <= op) return CombatMenuController.MaxShotCount;
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].ActiveWeapon?.CurrentAmmo ?? 0);
        }
    }
}
