#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, IDisposable
    {
        #region State

        private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel, ShotCountSelection, TargetSelection, Aiming }
        private CombatMenuState state              = CombatMenuState.OperatorSelection;
        private int             selectedOperator   = 0;
        private int[]           occupiedEnemySlots = Array.Empty<int>();
        private int             enemyTargetCursor  = 0;
        private int             currentTargetSlot  = -1;
        private int             selectedShotCount  = 1;
        private bool            awaitingAimDismiss = false;
        private int[]           operatorAmmo       = Array.Empty<int>();
        private const int       BaseDamage         = 20;
        private const int       MaxShotCount       = 6;
        private const int       DefaultOperatorAmmo = 6;

        #endregion

        #region Dependency Injection

        private readonly ICombatActionMenuView          menuView;
        private readonly ICommandPanelView              commandPanel;
        private readonly ISubPanelView                  subPanel;
        private readonly IShotCountView                 shotCountView;
        private readonly IPublisher<CombatEndedEvent>   combatEndedPublisher;
        private readonly IInputService?                 inputService;
        private readonly IAimView                       aimView;
        private readonly IBattlefieldView               battlefieldView;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IShotCountView               shotCountView,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView,
            IInputService                inputService)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.inputService         = inputService;
        }

        // Internal constructor for tests (no inputService, no battlefieldView wired to input)
        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IShotCountView               shotCountView,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnOperatorSelected    += this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     += this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected += this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    += this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected        += this.HandleItemSelected;
            this.subPanel.OnEntryFocused        += this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed   += this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed  += this.OnConfirmPerformed;
                this.inputService.CombatNavigate.performed += this.OnNavigatePerformed;
            }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected    -= this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     -= this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected -= this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    -= this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected        -= this.HandleItemSelected;
            this.subPanel.OnEntryFocused        -= this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed   -= this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed  -= this.OnConfirmPerformed;
                this.inputService.CombatNavigate.performed -= this.OnNavigatePerformed;
            }
        }

        #endregion

        #region Public (testable)

        internal void HandleCancelPressed()
        {
            switch (this.state)
            {
                case CombatMenuState.SubPanel:
                    this.subPanel.Hide();
                    this.commandPanel.SetDimmed(false);
                    this.commandPanel.Focus();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.ShotCountSelection:
                    this.shotCountView.Hide();
                    this.commandPanel.SetDimmed(false);
                    this.commandPanel.Focus();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.TargetSelection:
                    this.battlefieldView.HideEnemyTargetIndicator();
                    this.commandPanel.SetDimmed(false);
                    this.commandPanel.Focus();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.CommandPanel:
                    this.commandPanel.Hide();
                    this.menuView.SetDimmed(false);
                    this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
                    this.menuView.FocusOperator(this.selectedOperator);
                    this.state = CombatMenuState.OperatorSelection;
                    break;

                case CombatMenuState.OperatorSelection:
                    this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = false });
                    break;
            }
        }

        #endregion

        #region Handlers

        private void OnCancelPerformed(InputAction.CallbackContext _) =>
            this.HandleCancelPressed();

        private void OnConfirmPerformed(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case CombatMenuState.ShotCountSelection:
                    this.ConfirmShotCount();
                    break;
                case CombatMenuState.TargetSelection:
                    this.ConfirmTarget();
                    break;
                case CombatMenuState.Aiming:
                    if (this.awaitingAimDismiss)
                    {
                        this.CloseAimAndReturnToOperatorSelection();
                        break;
                    }
                    this.aimView.Confirm();
                    break;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            var dir = ctx.ReadValue<Vector2>();
            if (this.state == CombatMenuState.TargetSelection)
            {
                if (dir.x > 0.5f)       this.NavigateTarget(1);
                else if (dir.x < -0.5f) this.NavigateTarget(-1);
                return;
            }

            if (this.state == CombatMenuState.ShotCountSelection)
            {
                if (dir.x > 0.5f)       this.shotCountView.Increment();
                else if (dir.x < -0.5f) this.shotCountView.Decrement();
            }
        }

        private void HandleOperatorFocused(int index)
        {
            if (this.state != CombatMenuState.OperatorSelection) return;
            this.EnsureOperatorAmmoCapacity(index);
            this.menuView.SetOperatorAmmo(index, this.operatorAmmo[index], MaxShotCount);
            this.battlefieldView.SetOperatorIndicator(index);
        }

        private void HandleOperatorSelected(int index)
        {
            this.selectedOperator = index;
            this.EnsureOperatorAmmoCapacity(index);
            this.commandPanel.SetCommandEnabled(CombatCommand.Shoot, this.operatorAmmo[index] > 0);
            this.commandPanel.Show(this.menuView.GetOperatorRect(index));
            this.menuView.SetDimmed(true);
            this.battlefieldView.DimOperatorIndicator();
            this.state = CombatMenuState.CommandPanel;
        }

        private void HandleCommandSelected(CombatCommand command)
        {
            if (this.state != CombatMenuState.CommandPanel) return;

            if (command == CombatCommand.Shoot)
            {
                if (this.GetMaxAvailableShotCount() <= 0)
                    return;

                this.commandPanel.SetDimmed(true);
                this.menuView.SetDimmed(true);
                this.EnterShotCountSelection();
                return;
            }

            if (command == CombatCommand.Reload)
            {
                this.ReloadSelectedOperator();
                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
                this.menuView.FocusOperator(this.selectedOperator);
                this.state = CombatMenuState.OperatorSelection;
                return;
            }

            this.commandPanel.SetDimmed(true);
            this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
            this.state = CombatMenuState.SubPanel;
        }

        private void HandleItemSelected(int index) { }

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            int totalDamage = 0;
            if (shots != null)
            {
                foreach (var shot in shots)
                    totalDamage += Mathf.Max(0, shot.Damage);
            }

            if (this.currentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(this.currentTargetSlot, totalDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.currentTargetSlot} bullets={this.selectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
            }

            this.ConsumeAmmoForSelectedOperator(this.selectedShotCount);

            if (!this.battlefieldView.HasAliveEnemies())
                this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = true });

            this.awaitingAimDismiss = true;
        }

        #endregion

        #region Target Selection

        private void EnterShotCountSelection()
        {
            int maxAvailable = this.GetMaxAvailableShotCount();
            if (maxAvailable <= 0)
            {
                this.commandPanel.SetDimmed(false);
                this.commandPanel.Focus();
                this.state = CombatMenuState.CommandPanel;
                return;
            }

            this.selectedShotCount = 1;
            this.shotCountView.Show(this.commandPanel.PanelRect, this.selectedShotCount, maxAvailable);
            this.state = CombatMenuState.ShotCountSelection;
        }

        private void ConfirmShotCount()
        {
            this.selectedShotCount = Mathf.Clamp(this.shotCountView.Value, 1, this.GetMaxAvailableShotCount());
            this.shotCountView.Hide();
            this.EnterTargetSelection();
        }

        private void EnterTargetSelection()
        {
            this.occupiedEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();
            if (this.occupiedEnemySlots.Length == 0)
            {
                // No enemies in scene — go straight to aim
                this.aimView.ConfigureHitMask(null);
                this.aimView.SetShotCount(this.selectedShotCount);
                this.awaitingAimDismiss = false;
                this.aimView.OnShotsResolved += this.HandleShotsResolved;
                this.aimView.Show();
                this.state = CombatMenuState.Aiming;
                return;
            }
            this.enemyTargetCursor = 0;
            this.battlefieldView.SetEnemyTargetIndicator(this.occupiedEnemySlots[0]);
            this.state = CombatMenuState.TargetSelection;
        }

        private void NavigateTarget(int delta)
        {
            if (this.occupiedEnemySlots.Length == 0) return;
            this.enemyTargetCursor =
                (this.enemyTargetCursor + delta + this.occupiedEnemySlots.Length) % this.occupiedEnemySlots.Length;
            this.battlefieldView.SetEnemyTargetIndicator(this.occupiedEnemySlots[this.enemyTargetCursor]);
        }

        private void ConfirmTarget()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.currentTargetSlot = this.occupiedEnemySlots[this.enemyTargetCursor];
            this.aimView.ConfigureHitMask(this.battlefieldView.GetEnemyHitMaskProfile(this.currentTargetSlot));
            this.aimView.SetShotCount(this.selectedShotCount);
            this.awaitingAimDismiss = false;
            this.aimView.OnShotsResolved += this.HandleShotsResolved;
            this.aimView.Show();
            this.state = CombatMenuState.Aiming;
        }

        #endregion

        #region Helpers

        private SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
        {
            CombatCommand.Reload => new[] { new SubPanelItem("9MM FMJ"), new SubPanelItem("9MM RIP") },
            CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
            CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
            _                    => Array.Empty<SubPanelItem>()
        };

        internal static int ComputeShotDamage(ShotZone zone)
        {
            float multiplier = zone switch
            {
                ShotZone.Head  => 2.0f,
                ShotZone.Torso => 1.0f,
                ShotZone.Arms  => 0.7f,
                ShotZone.Legs  => 0.8f,
                ShotZone.Hit   => 1.0f,
                _              => 0.0f,
            };
            return Mathf.RoundToInt(BaseDamage * multiplier);
        }

        private int GetMaxAvailableShotCount()
        {
            this.EnsureOperatorAmmoCapacity(this.selectedOperator);
            return Mathf.Min(MaxShotCount, this.operatorAmmo[this.selectedOperator]);
        }

        private void ConsumeAmmoForSelectedOperator(int bullets)
        {
            this.EnsureOperatorAmmoCapacity(this.selectedOperator);
            this.operatorAmmo[this.selectedOperator] =
                Mathf.Max(0, this.operatorAmmo[this.selectedOperator] - Mathf.Max(0, bullets));
            this.menuView.SetOperatorAmmo(this.selectedOperator, this.operatorAmmo[this.selectedOperator], MaxShotCount);
        }

        private void ReloadSelectedOperator()
        {
            this.EnsureOperatorAmmoCapacity(this.selectedOperator);
            this.operatorAmmo[this.selectedOperator] = MaxShotCount;
            this.menuView.SetOperatorAmmo(this.selectedOperator, this.operatorAmmo[this.selectedOperator], MaxShotCount);
        }

        private void EnsureOperatorAmmoCapacity(int operatorIndex)
        {
            if (operatorIndex < 0) return;

            int required = operatorIndex + 1;
            if (this.operatorAmmo.Length >= required) return;

            int oldLength = this.operatorAmmo.Length;
            Array.Resize(ref this.operatorAmmo, required);
            for (int i = oldLength; i < this.operatorAmmo.Length; i++)
            {
                this.operatorAmmo[i] = DefaultOperatorAmmo;
                this.menuView.SetOperatorAmmo(i, this.operatorAmmo[i], MaxShotCount);
            }
        }

        private void CloseAimAndReturnToOperatorSelection()
        {
            this.currentTargetSlot = -1;
            this.selectedShotCount = 1;
            this.awaitingAimDismiss = false;
            this.aimView.OnShotsResolved -= this.HandleShotsResolved;
            this.aimView.Hide();
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
            this.menuView.FocusOperator(this.selectedOperator);
            this.state = CombatMenuState.OperatorSelection;
        }

        #endregion
    }
}
