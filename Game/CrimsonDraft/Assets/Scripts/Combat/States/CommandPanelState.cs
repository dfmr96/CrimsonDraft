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
            int slot = this.context.SelectedOperator;

            // Position/activate with content hidden; the operator's own border grows to
            // make room, and only once THAT finishes does the panel reveal its text —
            // otherwise the options would appear before there's space drawn for them.
            this.commandPanel.Show(this.menuView.GetOperatorOverviewRect(slot));
            this.commandPanel.SetDimmed(false);
            this.menuView.ExpandOperatorBorder(slot, true, this.commandPanel.RevealContent);
        }

        public void OnCancel()
        {
            this.sfx?.PlayCancel(this.commandPanel.PanelRect.gameObject);
            this.commandPanel.Hide();
            this.menuView.ExpandOperatorBorder(this.context.SelectedOperator, false);
            this.context.TransitionTo(this.context.OperatorSelState);
        }

        public void OnCommandSelected(CombatCommand command)
        {
            if (command == CombatCommand.Melee)
            {
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.context.Orchestrator.EnqueueAction(PendingAction.Melee(this.context.SelectedOperator));
                this.commandPanel.Hide();
                this.menuView.ExpandOperatorBorder(this.context.SelectedOperator, false);
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                this.commandPanel.Hide();
                this.menuView.ExpandOperatorBorder(this.context.SelectedOperator, false);
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.Items)
            {
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.commandPanel.Hide();
                this.menuView.ExpandOperatorBorder(this.context.SelectedOperator, false);
                this.context.TransitionTo(this.context.CombatInventoryState);
            }
        }

        private int GetMaxAvailableShotCount()
        {
            int op = this.context.SelectedOperator;
            if (op < 0 || this.roster.Count <= op) return 0;
            var weapon = this.roster[op].ActiveWeapon;
            return weapon == null ? 0 : Mathf.Min(weapon.MaxShotCount, weapon.CurrentAmmo);
        }
    }
}
