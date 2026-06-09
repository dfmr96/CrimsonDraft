#nullable enable

using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    internal sealed class TargetSelectionState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICommandPanelView    commandPanel;
        private readonly IBattlefieldView     battlefieldView;
        private readonly IAimView             aimView;
        private readonly IOperatorRoster      roster;

        private int[] occupiedSlots = System.Array.Empty<int>();
        private int   cursor        = 0;

        internal TargetSelectionState(
            CombatMenuController context,
            ICommandPanelView    commandPanel,
            IBattlefieldView     battlefieldView,
            IAimView             aimView,
            IOperatorRoster      roster)
        {
            this.context         = context;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            this.occupiedSlots = this.battlefieldView.GetOccupiedEnemySlots();
            this.cursor        = 0;
            if (this.occupiedSlots.Length > 0)
                this.battlefieldView.SetEnemyTargetIndicator(this.occupiedSlots[0]);
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
        }

        public void OnCancel()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.context.TransitionTo(this.context.ShotCountState);
        }

        public void OnConfirm()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.context.CurrentTargetSlot = this.occupiedSlots.Length > 0
                ? this.occupiedSlots[this.cursor] : -1;
            int op = this.context.SelectedOperator;
            WeaponData? weaponData = this.roster.Count > op ? (this.roster[op].ActiveWeapon as WeaponItem)?.Data : null;
            this.aimView.ConfigureWeapon(weaponData);
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
