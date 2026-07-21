#nullable enable

using CrimsonDraft.Audio;
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
        private readonly CombatSfxData?       sfx;

        internal ShotCountSelectionState(
            CombatMenuController context,
            ICommandPanelView    commandPanel,
            IShotCountView       shotCountView,
            IBattlefieldView     battlefieldView,
            IAimView             aimView,
            IOperatorRoster      roster,
            CombatSfxData?       sfx = null)
        {
            this.context         = context;
            this.commandPanel    = commandPanel;
            this.shotCountView   = shotCountView;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
            this.sfx             = sfx;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            int max = GetMaxAvailable();
            this.context.SelectedShotCount = 1;
            this.shotCountView.Show(this.commandPanel.PanelRect, 1, max);
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
            this.shotCountView.Hide();
        }

        public void OnCancel() { }

        public void OnConfirm()
        {
            this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
            int max = GetMaxAvailable();
            this.context.SelectedShotCount = Mathf.Clamp(this.shotCountView.Value, 1, max);
            this.shotCountView.Hide();

            if (this.context.FocusFireParticipants.Length > 0)
            {
                this.context.FocusFireShotCounts[this.context.SelectedOperator] = this.context.SelectedShotCount;

                int nextIndex = this.context.FocusFireParticipantIndex + 1;
                if (nextIndex < this.context.FocusFireParticipants.Length)
                {
                    this.context.FocusFireParticipantIndex = nextIndex;
                    this.context.SelectedOperator          = this.context.FocusFireParticipants[nextIndex];
                    this.context.TransitionTo(this);
                    return;
                }

                this.context.TransitionTo(this.context.TargetSelState);
                return;
            }

            int[] enemies = this.battlefieldView.GetOccupiedEnemySlots();
            if (enemies.Length == 0)
            {
                int op = this.context.SelectedOperator;
                WeaponData? weaponData = this.roster.Count > op ? (this.roster[op].ActiveWeapon as WeaponItem)?.Data : null;
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
            if (dir.x > 0.5f)
            {
                this.shotCountView.Increment();
                this.sfx?.PlayCursor(this.commandPanel.PanelRect.gameObject);
            }
            else if (dir.x < -0.5f)
            {
                this.shotCountView.Decrement();
                this.sfx?.PlayCursor(this.commandPanel.PanelRect.gameObject);
            }
        }

        private int GetMaxAvailable()
        {
            int op = this.context.SelectedOperator;
            if (this.roster.Count <= op) return CombatMenuController.MaxShotCount;
            return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].ActiveWeapon?.CurrentAmmo ?? 0);
        }
    }
}
