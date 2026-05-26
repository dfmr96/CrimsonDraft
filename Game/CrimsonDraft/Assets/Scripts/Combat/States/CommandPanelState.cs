#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
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
        private readonly IInventoryService     inventory;

        internal CommandPanelState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            ISubPanelView         subPanel,
            IBattlefieldView      battlefieldView,
            IOperatorRoster       roster,
            IInventoryService     inventory)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.subPanel        = subPanel;
            this.battlefieldView = battlefieldView;
            this.roster          = roster;
            this.inventory       = inventory;
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
                this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.Reload)
            {
                int op = this.context.SelectedOperator;

                var compatibleIndices = new List<int>();
                var items             = new List<SubPanelItem>();

                int start = op * 4;
                for (int i = start; i < start + 4; i++)
                {
                    var slot = this.inventory.Slots[i];
                    if (this.inventory.CanReload(i, op) && slot.Item is AmmoBoxItem box)
                    {
                        compatibleIndices.Add(i);
                        items.Add(new SubPanelItem($"{box.Data.DisplayName} \u00d7{box.Quantity}"));
                    }
                }

                if (items.Count == 0)
                    items.Add(new SubPanelItem("NO AMMO"));

                this.context.ReloadAmmoBoxIndices = compatibleIndices.ToArray();
                this.commandPanel.SetDimmed(true);
                this.subPanel.Show(items.ToArray(), this.commandPanel.PanelRect);
                this.context.TransitionTo(this.context.SubPanelState);
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
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].EquippedWeapon?.CurrentAmmo ?? 0);
        }

        private static SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
        {
            CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
            CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
            _                    => Array.Empty<SubPanelItem>()
        };
    }
}
