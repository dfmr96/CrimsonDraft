#nullable enable

using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    internal sealed class ShotCountSelectionState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICommandPanelView    commandPanel;
        private readonly IShotCountView       shotCountView;
        private readonly IBattlefieldView     battlefieldView;
        private readonly IAimView             aimView;
        private readonly IOperatorRoster      roster;

        internal ShotCountSelectionState(
            CombatMenuController context,
            ICommandPanelView    commandPanel,
            IShotCountView       shotCountView,
            IBattlefieldView     battlefieldView,
            IAimView             aimView,
            IOperatorRoster      roster)
        {
            this.context         = context;
            this.commandPanel    = commandPanel;
            this.shotCountView   = shotCountView;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
        }

        public void Enter()
        {
            int max = GetMaxAvailable();
            this.context.SelectedShotCount = 1;
            this.shotCountView.Show(this.commandPanel.PanelRect, 1, max);
        }

        public void Exit() => this.shotCountView.Hide();

        public void OnCancel() =>
            this.context.TransitionTo(this.context.CommandPanelState);

        public void OnConfirm()
        {
            int max = GetMaxAvailable();
            this.context.SelectedShotCount = Mathf.Clamp(this.shotCountView.Value, 1, max);
            this.shotCountView.Hide();

            int[] enemies = this.battlefieldView.GetOccupiedEnemySlots();
            if (enemies.Length == 0)
            {
                int op = this.context.SelectedOperator;
                WeaponData? weaponData = this.roster.Count > op ? (this.roster[op].EquippedWeapon as WeaponItem)?.Data : null;
                this.aimView.ConfigureWeapon(weaponData);
                this.aimView.ConfigureHitMask(null);
                this.aimView.SetShotCount(this.context.SelectedShotCount);
                this.context.TransitionTo(this.context.AimingState);
                return;
            }

            this.context.TransitionTo(this.context.TargetSelState);
        }

        public void OnNavigate(Vector2 dir)
        {
            if      (dir.x > 0.5f)  this.shotCountView.Increment();
            else if (dir.x < -0.5f) this.shotCountView.Decrement();
        }

        private int GetMaxAvailable()
        {
            int op = this.context.SelectedOperator;
            if (this.roster.Count <= op) return CombatMenuController.MaxShotCount;
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].EquippedWeapon?.CurrentAmmo ?? 0);
        }
    }
}
