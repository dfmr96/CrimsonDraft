#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    internal sealed class TargetSelectionState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICommandPanelView    commandPanel;
        private readonly IBattlefieldView     battlefieldView;
        private readonly IAimView             aimView;

        private int[] occupiedSlots = System.Array.Empty<int>();
        private int   cursor        = 0;

        internal TargetSelectionState(
            CombatMenuController context,
            ICommandPanelView    commandPanel,
            IBattlefieldView     battlefieldView,
            IAimView             aimView)
        {
            this.context         = context;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
        }

        public void Enter()
        {
            this.occupiedSlots = this.battlefieldView.GetOccupiedEnemySlots();
            this.cursor        = 0;
            if (this.occupiedSlots.Length > 0)
                this.battlefieldView.SetEnemyTargetIndicator(this.occupiedSlots[0]);
        }

        public void OnCancel()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.context.TransitionTo(this.context.CommandPanelState);
        }

        public void OnConfirm()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.context.CurrentTargetSlot = this.occupiedSlots.Length > 0
                ? this.occupiedSlots[this.cursor] : -1;
            this.aimView.ConfigureHitMask(
                this.context.CurrentTargetSlot >= 0
                    ? this.battlefieldView.GetEnemyHitMaskProfile(this.context.CurrentTargetSlot)
                    : null);
            this.aimView.SetShotCount(this.context.SelectedShotCount);
            this.context.TransitionTo(this.context.AimingState);
        }

        public void OnNavigate(Vector2 dir)
        {
            if (this.occupiedSlots.Length == 0) return;
            if      (dir.x > 0.5f)  this.cursor = (this.cursor + 1) % this.occupiedSlots.Length;
            else if (dir.x < -0.5f) this.cursor = (this.cursor - 1 + this.occupiedSlots.Length) % this.occupiedSlots.Length;
            this.battlefieldView.SetEnemyTargetIndicator(this.occupiedSlots[this.cursor]);
        }
    }
}
