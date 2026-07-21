#nullable enable

using System;
using System.Collections.Generic;
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

        private readonly List<(int Slot, ResolvedShot[] Shots)> pendingGroupShots = new();

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            this.pendingShots   = shots ?? Array.Empty<ResolvedShot>();
            this.pendingStagger = false;
            this.pendingDeath   = false;

            if (this.context.FocusFireParticipants.Length > 0)
            {
                HandleGroupShotsResolved();
                this.awaitingDismiss = true;
                return;
            }

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
                this.pendingStagger = result.IsStaggered;
                this.pendingDeath   = result.IsDead;
            }

            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);

            this.awaitingDismiss = true;
        }

        private void HandleGroupShotsResolved()
        {
            this.pendingGroupShots.Clear();
            int[] participants = this.context.FocusFireParticipants;

            for (int i = 0; i < participants.Length; i++)
            {
                int slot = participants[i];
                var weapon = this.roster.Count > slot ? this.roster[slot].ActiveWeapon : null;
                int weaponPoiseDamage = weapon?.PoiseDamage ?? 0;
                bool isTrigger = i == participants.Length - 1;
                int shotCount = this.context.FocusFireShotCounts.TryGetValue(slot, out int sc) ? sc : 1;

                ResolvedShot[] participantShots = isTrigger
                    ? this.pendingShots
                    : this.aimView.ResolveShotsForWeapon((weapon as WeaponItem)?.Data, shotCount);

                int totalDamage = 0;
                int totalPoiseDamage = 0;
                foreach (var shot in participantShots)
                {
                    totalDamage += Mathf.Max(0, shot.Damage);
                    if (shot.Zone != ShotZone.Miss)
                        totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
                }

                if (this.context.CurrentTargetSlot >= 0)
                {
                    var result = this.battlefieldView.ApplyDamageToEnemy(
                        this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);
                    this.pendingStagger = result.IsStaggered;
                    this.pendingDeath   = result.IsDead;
                }

                if (weapon != null)
                    weapon.SetAmmo(weapon.CurrentAmmo - shotCount);

                this.pendingGroupShots.Add((slot, participantShots));
            }
        }

        private async UniTaskVoid CloseAimAndReturnToOperatorSelectionAsync()
        {
            this.awaitingDismiss = false;
            this.aimView.Hide();
            this.commandPanel.Hide();

            bool isGroup = this.context.FocusFireParticipants.Length > 0;

            this.isPlayingBurst = true;
            if (isGroup)
            {
                var bursts = new UniTask[this.pendingGroupShots.Count];
                for (int i = 0; i < this.pendingGroupShots.Count; i++)
                {
                    var participant = this.pendingGroupShots[i];
                    bursts[i] = this.battlefieldView.PlayOperatorShootBurstAsync(
                        participant.Slot, this.context.CurrentTargetSlot, participant.Shots);
                }
                await UniTask.WhenAll(bursts);
            }
            else
            {
                await this.battlefieldView.PlayOperatorShootBurstAsync(
                    this.context.SelectedOperator,
                    this.context.CurrentTargetSlot,
                    this.pendingShots);
            }
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

            if (isGroup)
            {
                this.context.Orchestrator.NotifyFocusFireCompleted();
                this.context.FocusFireParticipants = Array.Empty<int>();
                this.context.FocusFireShotCounts.Clear();
            }
            else
            {
                this.context.Orchestrator.NotifyShootCompleted();
            }

            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
