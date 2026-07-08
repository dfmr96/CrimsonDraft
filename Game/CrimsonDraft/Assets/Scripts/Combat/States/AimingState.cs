#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
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

        private bool awaitingDismiss;
        private bool isPlayingBurst;

        internal AimingState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            IBattlefieldView      battlefieldView,
            IAimView              aimView,
            IOperatorRoster       roster)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
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

            if (this.awaitingDismiss)
            {
                CloseAimAndReturnToOperatorSelectionAsync().Forget();
                return;
            }
            this.aimView.Confirm();
        }

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            int totalDamage = 0;
            if (shots != null)
            {
                foreach (var shot in shots)
                    totalDamage += Mathf.Max(0, shot.Damage);
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(this.context.CurrentTargetSlot, totalDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
            }

            int op = this.context.SelectedOperator;
            if (this.roster.Count > op)
            {
                var weapon = this.roster[op].ActiveWeapon;
                if (weapon != null)
                    weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);
            }

            this.awaitingDismiss = true;
        }

        private async UniTaskVoid CloseAimAndReturnToOperatorSelectionAsync()
        {
            this.awaitingDismiss = false;
            this.aimView.Hide();
            this.commandPanel.Hide();

            this.isPlayingBurst = true;
            await this.battlefieldView.PlayOperatorShootBurstAsync(this.context.SelectedOperator, this.context.SelectedShotCount);
            this.isPlayingBurst = false;

            this.context.Orchestrator.NotifyShootCompleted();
            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
