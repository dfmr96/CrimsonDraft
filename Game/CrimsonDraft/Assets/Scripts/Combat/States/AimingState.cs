#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Audio;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class AimingState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICombatActionMenuView menuView;
        private readonly ICommandPanelView     commandPanel;
        private readonly IBattlefieldView      battlefieldView;
        private readonly IAimView              aimView;
        private readonly IOperatorRoster       roster;
        private readonly CombatSfxData?        sfx;

        private bool awaitingDismiss;
        private bool isPlayingBurst;
        private ResolvedShot[] pendingShots = Array.Empty<ResolvedShot>();

        internal AimingState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            IBattlefieldView      battlefieldView,
            IAimView              aimView,
            IOperatorRoster       roster,
            CombatSfxData?        sfx = null)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
            this.sfx             = sfx;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            this.awaitingDismiss = false;
            this.isPlayingBurst  = false;
            this.aimView.OnShotsResolved += HandleShotsResolved;
            this.aimView.Show();
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
            this.aimView.OnShotsResolved -= HandleShotsResolved;
        }

        public void OnConfirm()
        {
            if (this.isPlayingBurst) return;

            this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);

            if (this.awaitingDismiss)
            {
                CloseAimAndReturnToOperatorSelectionAsync().Forget();
                return;
            }
            this.aimView.Confirm();
        }

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            this.pendingShots = shots ?? Array.Empty<ResolvedShot>();

            int op = this.context.SelectedOperator;
            var weapon = this.roster.Count > op ? this.roster[op].ActiveWeapon : null;
            int weaponPoiseDamage = weapon?.PoiseDamage ?? 0;

            int totalDamage = 0;
            int totalPoiseDamage = 0;
            foreach (var shot in this.pendingShots)
            {
                totalDamage += Mathf.Max(0, shot.Damage);
                if (shot.Zone != ShotZone.Miss)
                    totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(
                    this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
                if (result.IsStaggered)
                    this.context.Orchestrator.NotifyEnemyStaggered(this.context.CurrentTargetSlot);
            }

            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);

            this.awaitingDismiss = true;
        }

        private async UniTaskVoid CloseAimAndReturnToOperatorSelectionAsync()
        {
            this.awaitingDismiss = false;
            this.aimView.Hide();
            this.commandPanel.Hide();

            this.isPlayingBurst = true;
            await this.battlefieldView.PlayOperatorShootBurstAsync(
                this.context.SelectedOperator,
                this.context.CurrentTargetSlot,
                this.pendingShots);
            this.isPlayingBurst = false;

            this.context.Orchestrator.NotifyShootCompleted();
            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
