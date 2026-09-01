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
            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);

                if (this.context.FocusFireMarked.Count > 0)
                {
                    int[] participants = new int[this.context.FocusFireMarked.Count + 1];
                    this.context.FocusFireMarked.CopyTo(participants, 0);
                    participants[participants.Length - 1] = this.context.SelectedOperator;

                    for (int i = 0; i < this.context.FocusFireMarked.Count; i++)
                        this.menuView.SetOperatorFocusFireMarked(this.context.FocusFireMarked[i], false);
                    this.context.FocusFireMarked.Clear();

                    this.context.Orchestrator.EnqueueAction(PendingAction.FocusFire(this.context.SelectedOperator, participants));
                }
                else
                {
                    this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                }

                this.commandPanel.Hide();
                this.menuView.ExpandOperatorBorder(this.context.SelectedOperator, false);
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.FocusFire)
            {
                // Re-validated here (not just in the view's SetCommandEnabled) so a stale/bypassed
                // UI click can never mark every alive operator and leave no one able to trigger —
                // that would hard-lock combat with the whole party frozen waiting on a Shoot command.
                if (this.context.FocusFireMarked.Count >= this.roster.GetAliveSlots().Count - 1) return;
                // Marking commits this operator to fire once the group triggers, same as Shoot —
                // it must be just as unavailable without ammo, or the group's shared QTE ends up
                // resolving a shot for a weapon that has none left to fire.
                if (GetMaxAvailableShotCount() <= 0) return;

                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                int slot = this.context.SelectedOperator;
                this.context.FocusFireMarked.Add(slot);
                this.context.Orchestrator.MarkOperatorForFocusFire(slot);
                this.menuView.SetOperatorFocusFireMarked(slot, true);
                this.menuView.SetOperatorDimmed(slot, true);
                this.commandPanel.Hide();
                this.menuView.ExpandOperatorBorder(slot, false);
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
            if (this.roster.Count <= op) return CombatMenuController.MaxShotCount;
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].ActiveWeapon?.CurrentAmmo ?? 0);
        }
    }
}
