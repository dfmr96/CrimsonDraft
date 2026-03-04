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

        private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel, TargetSelection, Aiming }
        private CombatMenuState state              = CombatMenuState.OperatorSelection;
        private int             selectedOperator   = 0;
        private int[]           occupiedEnemySlots = Array.Empty<int>();
        private int             enemyTargetCursor  = 0;

        #endregion

        #region Dependency Injection

        private readonly ICombatActionMenuView          menuView;
        private readonly ICommandPanelView              commandPanel;
        private readonly ISubPanelView                  subPanel;
        private readonly IPublisher<CombatEndedEvent>   combatEndedPublisher;
        private readonly IInputService?                 inputService;
        private readonly IAimView                       aimView;
        private readonly IBattlefieldView               battlefieldView;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView,
            IInputService                inputService)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
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
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
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
                case CombatMenuState.TargetSelection:
                    this.ConfirmTarget();
                    break;
                case CombatMenuState.Aiming:
                    this.aimView.Confirm();
                    break;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            if (this.state != CombatMenuState.TargetSelection) return;
            var dir = ctx.ReadValue<Vector2>();
            if (dir.x > 0.5f)       this.NavigateTarget(1);
            else if (dir.x < -0.5f) this.NavigateTarget(-1);
        }

        private void HandleOperatorFocused(int index)
        {
            if (this.state != CombatMenuState.OperatorSelection) return;
            this.battlefieldView.SetOperatorIndicator(index);
        }

        private void HandleOperatorSelected(int index)
        {
            this.selectedOperator = index;
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
                this.commandPanel.SetDimmed(true);
                this.menuView.SetDimmed(true);
                this.EnterTargetSelection();
                return;
            }

            this.commandPanel.SetDimmed(true);
            this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
            this.state = CombatMenuState.SubPanel;
        }

        private void HandleItemSelected(int index) { }

        private void HandleShotFired(Vector2 normalizedPos, ShotZone zone)
        {
            _ = normalizedPos;
            _ = zone;
            this.aimView.OnShotFired -= this.HandleShotFired;
            this.aimView.Hide();
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
            this.menuView.FocusOperator(this.selectedOperator);
            this.state = CombatMenuState.OperatorSelection;
        }

        #endregion

        #region Target Selection

        private void EnterTargetSelection()
        {
            this.occupiedEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();
            if (this.occupiedEnemySlots.Length == 0)
            {
                // No enemies in scene — go straight to aim
                this.aimView.ConfigureHitMask(null);
                this.aimView.OnShotFired += this.HandleShotFired;
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
            int targetSlot = this.occupiedEnemySlots[this.enemyTargetCursor];
            this.aimView.ConfigureHitMask(this.battlefieldView.GetEnemyHitMaskProfile(targetSlot));
            this.aimView.OnShotFired += this.HandleShotFired;
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

        #endregion
    }
}
