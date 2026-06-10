#nullable enable

using MessagePipe;
using CrimsonDraft.Infrastructure.Events;
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

        private float canAcceptSubmitAt;

        internal OperatorSelectionState(
            CombatMenuController         context,
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            IBattlefieldView             battlefieldView,
            IPublisher<CombatEndedEvent> publisher,
            IOperatorRoster              roster)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.publisher       = publisher;
            this.roster          = roster;
        }

        public void Enter()
        {
            this.canAcceptSubmitAt = UnityEngine.Application.isPlaying
                ? UnityEngine.Time.unscaledTime + 0.15f
                : 0f;
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            SyncAllOperatorAmmo();

            for (int i = 0; i < this.roster.Count; i++)
            {
                if (this.context.Orchestrator.IsOperatorReady(i))
                {
                    this.battlefieldView.SetOperatorIndicator(i);
                    this.menuView.MoveSelectorTo(this.menuView.GetOperatorAnchor(i));
                    this.menuView.FocusOperator(i);
                    return;
                }
            }

            this.menuView.ClearFocus();
        }

        public void OnCancel() =>
            this.publisher.Publish(new CombatEndedEvent { Victory = false });

        public void OnOperatorFocused(int index)
        {
            if (this.roster.Count == 0) return;
            var weapon = this.roster[index].ActiveWeapon;
            this.menuView.SetOperatorAmmo(index, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
            this.battlefieldView.SetOperatorIndicator(index);
        }

        public void OnOperatorSelected(int index)
        {
            if (UnityEngine.Time.unscaledTime < this.canAcceptSubmitAt) return;
            if (!this.context.Orchestrator.IsOperatorReady(index)) return;
            this.context.SelectedOperator = index;
            bool hasAmmo = this.roster.Count > index && (this.roster[index].ActiveWeapon?.CurrentAmmo ?? 0) > 0;
            this.commandPanel.SetCommandEnabled(CombatCommand.Shoot, hasAmmo);
            this.commandPanel.Show(this.menuView.GetOperatorOverviewRect(index));
            this.menuView.SetDimmed(true);
            this.battlefieldView.DimOperatorIndicator();
            this.context.TransitionTo(this.context.CommandPanelState);
        }

        private void SyncAllOperatorAmmo()
        {
            for (int i = 0; i < this.roster.Count; i++)
            {
                var weapon = this.roster[i].ActiveWeapon;
                this.menuView.SetOperatorAmmo(i, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
            }
        }
    }
}
