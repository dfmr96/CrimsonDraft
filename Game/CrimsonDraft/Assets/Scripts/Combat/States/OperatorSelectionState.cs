#nullable enable

using MessagePipe;
using CrimsonDraft.Audio;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class OperatorSelectionState : ICombatMenuState
    {
        private readonly CombatMenuController         context;
        private readonly ICombatActionMenuView        menuView;
        private readonly ICommandPanelView            commandPanel;
        private readonly IBattlefieldView             battlefieldView;
        private readonly IPublisher<CombatEndedEvent> publisher;
        private readonly IOperatorRoster              roster;
        private readonly CombatSfxData?               sfx;

        private float canAcceptSubmitAt;

        internal OperatorSelectionState(
            CombatMenuController         context,
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            IBattlefieldView             battlefieldView,
            IPublisher<CombatEndedEvent> publisher,
            IOperatorRoster              roster,
            CombatSfxData?               sfx = null)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.publisher       = publisher;
            this.roster          = roster;
            this.sfx             = sfx;
        }

        public void Enter()
        {
            this.canAcceptSubmitAt = UnityEngine.Application.isPlaying
                ? UnityEngine.Time.unscaledTime + 0.15f
                : 0f;
            // The operator that ends up auto-focused may not be ready yet at this exact
            // point (ATB marks actors "awaiting command" on the next orchestrator tick, not
            // synchronously here) — so suppress whichever focus event arrives first, whenever
            // it arrives, rather than trying to predict which operator it will be.
            this.context.SuppressNextOperatorFocusSfx();
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            SyncAllOperatorAmmo();
            SyncAllOperatorHealth();

            // Cancelling out of an operator's command panel (still ready — their turn wasn't
            // consumed) should return focus to that same operator rather than restarting the
            // scan from 0. After an operator actually acts their gauge gets reset, so this
            // naturally falls through to the scan below and picks whoever is ready next.
            int preferred = this.context.SelectedOperator;
            if (preferred >= 0 && preferred < this.roster.Count && this.context.Orchestrator.IsOperatorReady(preferred))
            {
                FocusReadyOperator(preferred);
                return;
            }

            for (int i = 0; i < this.roster.Count; i++)
            {
                if (this.context.Orchestrator.IsOperatorReady(i))
                {
                    FocusReadyOperator(i);
                    return;
                }
            }

            this.menuView.ClearFocus();
        }

        public void OnCancel()
        {
            this.sfx?.PlayCancel(this.commandPanel.PanelRect.gameObject);
            this.publisher.Publish(new CombatEndedEvent { Victory = false });
        }

        public void OnOperatorFocused(int index)
        {
            if (this.roster.Count == 0) return;
            var weapon = this.roster[index].ActiveWeapon;
            this.menuView.SetOperatorAmmo(index, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
            this.menuView.SetOperatorWeapon(index, weapon as WeaponItem);
            this.battlefieldView.SetOperatorIndicator(index);
        }

        public void OnOperatorSelected(int index)
        {
            if (UnityEngine.Time.unscaledTime < this.canAcceptSubmitAt) return;
            if (!this.context.Orchestrator.IsOperatorReady(index)) return;
            this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
            this.context.SelectedOperator = index;
            bool hasAmmo = this.roster.Count > index && (this.roster[index].ActiveWeapon?.CurrentAmmo ?? 0) > 0;
            this.commandPanel.SetCommandEnabled(CombatCommand.Shoot, hasAmmo);
            bool wouldExhaustFocusFireGroup = this.context.FocusFireMarked.Count >= this.roster.GetAliveSlots().Count - 1;
            this.commandPanel.SetCommandEnabled(CombatCommand.FocusFire, !wouldExhaustFocusFireGroup);
            this.commandPanel.Show(this.menuView.GetOperatorOverviewRect(index));
            this.menuView.SetDimmed(true);
            this.battlefieldView.DimOperatorIndicator();
            this.context.TransitionTo(this.context.CommandPanelState);
        }

        private void FocusReadyOperator(int index)
        {
            this.battlefieldView.SetOperatorIndicator(index);
            this.menuView.MoveSelectorTo(this.menuView.GetOperatorAnchor(index));
            this.menuView.FocusOperator(index);
        }

        private void SyncAllOperatorAmmo()
        {
            for (int i = 0; i < this.roster.Count; i++)
            {
                var weapon = this.roster[i].ActiveWeapon;
                this.menuView.SetOperatorAmmo(i, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
                this.menuView.SetOperatorWeapon(i, weapon as WeaponItem);
            }
        }

        private void SyncAllOperatorHealth()
        {
            for (int i = 0; i < this.roster.Count; i++)
                this.menuView.SetOperatorHealth(i, this.roster[i].HpRatio);
        }
    }
}
