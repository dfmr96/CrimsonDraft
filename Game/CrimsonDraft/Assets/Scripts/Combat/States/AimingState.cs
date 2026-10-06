#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Audio;
using CrimsonDraft.Inventory;
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
        private bool pendingStagger;
        private bool pendingDeath;
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

            int op = this.context.SelectedOperator;
            float hpRatio = this.roster.Count > op ? this.roster[op].HpRatio : 1f;
            this.aimView.SetOperatorHpRatio(hpRatio);

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
            this.pendingShots   = shots ?? Array.Empty<ResolvedShot>();
            this.pendingStagger = false;
            this.pendingDeath   = false;

            int op = this.context.SelectedOperator;
            IWeaponSlot? weapon = null;
            int weaponPoiseDamage;
            if (this.context.IsMeleeAttack)
            {
                var melee = this.roster.Count > op ? this.roster[op].MeleeWeapon as MeleeWeaponData : null;
                weaponPoiseDamage = melee?.PoiseDamage ?? 0;
            }
            else
            {
                weapon = this.roster.Count > op ? this.roster[op].ActiveWeapon : null;
                weaponPoiseDamage = weapon?.PoiseDamage ?? 0;
            }

            int totalDamage = 0;
            int totalPoiseDamage = 0;
            int headshotPellets = 0;
            int damagingPellets = 0;
            bool anyMiss = false;
            foreach (var shot in this.pendingShots)
            {
                totalDamage += Mathf.Max(0, shot.Damage);
                if (shot.Zone != ShotZone.Miss)
                    totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
                else
                    anyMiss = true;
                if (shot.Zone == ShotZone.Head)
                    headshotPellets++;
                if (shot.Damage > 0)
                    damagingPellets++;
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(
                    this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage, headshotPellets);
#if UNITY_EDITOR
                // pellets == bullets for non-shotgun weapons (1 pellet each); headshots/damaging
                // are only interesting when a shotgun's PelletSpreadStrategy fired several per bullet.
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} pellets={this.pendingShots.Length} headshotPellets={headshotPellets} damagingPellets={damagingPellets} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead} decapitated={result.IsDecapitated}");
#endif
                this.pendingStagger = result.IsStaggered;
                this.pendingDeath   = result.IsDead;

                // Whiffing even one point of a melee swing leaves the operator open -- a single
                // counter-hit regardless of how many points missed, worth half that enemy's own
                // attack damage (CombatOrchestrator.ApplyMeleeCounterDamage).
                if (this.context.IsMeleeAttack && anyMiss)
                    this.context.Orchestrator.ApplyMeleeCounterDamage(op, this.context.CurrentTargetSlot);
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
                this.pendingShots, this.context.IsMeleeAttack);
            this.isPlayingBurst = false;

            if (this.pendingDeath)
            {
                this.battlefieldView.FinalizeEnemyDeath(this.context.CurrentTargetSlot);
                this.pendingDeath = false;
            }
            else if (this.pendingStagger)
            {
                this.battlefieldView.TriggerEnemyStagger(this.context.CurrentTargetSlot);
                this.context.Orchestrator.NotifyEnemyStaggered(this.context.CurrentTargetSlot);
                this.pendingStagger = false;
            }

            if (this.context.IsMeleeAttack)
            {
                this.context.Orchestrator.NotifyMeleeCompleted();
            }
            else
            {
                this.context.Orchestrator.NotifyShootCompleted();
            }

            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.IsMeleeAttack     = false;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
