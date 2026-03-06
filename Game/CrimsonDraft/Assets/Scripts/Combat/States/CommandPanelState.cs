#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class CommandPanelState : ICombatMenuState
    {
        private readonly CombatMenuController  context;
        private readonly ICombatActionMenuView menuView;
        private readonly ICommandPanelView     commandPanel;
        private readonly ISubPanelView         subPanel;
        private readonly IBattlefieldView      battlefieldView;
        private readonly IOperatorRoster       roster;

        internal CommandPanelState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            ISubPanelView         subPanel,
            IBattlefieldView      battlefieldView,
            IOperatorRoster       roster)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.subPanel        = subPanel;
            this.battlefieldView = battlefieldView;
            this.roster          = roster;
        }

        public void Enter()
        {
            this.commandPanel.SetDimmed(false);
            this.commandPanel.Focus();
        }

        public void OnCancel()
        {
            this.commandPanel.Hide();
            this.context.TransitionTo(this.context.OperatorSelState);
        }

        public void OnCommandSelected(CombatCommand command)
        {
            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.commandPanel.SetDimmed(true);
                this.menuView.SetDimmed(true);
                this.context.TransitionTo(this.context.ShotCountState);
                return;
            }

            if (command == CombatCommand.Reload)
            {
                int op = this.context.SelectedOperator;
                if (this.roster.Count > op)
                {
                    this.roster[op].Reload();
                    this.menuView.SetOperatorAmmo(op, this.roster[op].Ammo, this.roster[op].MaxAmmo);
                }
                this.commandPanel.Hide();
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            this.commandPanel.SetDimmed(true);
            this.subPanel.Show(GetItemsFor(command), this.commandPanel.PanelRect);
            this.context.TransitionTo(this.context.SubPanelState);
        }

        private int GetMaxAvailableShotCount()
        {
            int op = this.context.SelectedOperator;
            if (this.roster.Count <= op) return CombatMenuController.MaxShotCount;
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].Ammo);
        }

        private static SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
        {
            CombatCommand.Reload => new[] { new SubPanelItem("9MM FMJ"), new SubPanelItem("9MM RIP") },
            CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
            CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
            _                    => Array.Empty<SubPanelItem>()
        };
    }
}
