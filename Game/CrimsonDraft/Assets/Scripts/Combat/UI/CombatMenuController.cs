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

        private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel, Aiming }
        private CombatMenuState state           = CombatMenuState.OperatorSelection;
        private int             selectedOperator = 0;

        #endregion

        #region Dependency Injection

        private readonly ICombatActionMenuView          menuView;
        private readonly ICommandPanelView              commandPanel;
        private readonly ISubPanelView                  subPanel;
        private readonly IPublisher<CombatEndedEvent>   combatEndedPublisher;
        private readonly IInputService?                 inputService;
        private readonly IAimView                       aimView;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IInputService                inputService)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.inputService         = inputService;
        }

        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnOperatorSelected     += this.HandleOperatorSelected;
            this.commandPanel.OnCommandSelected  += this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused     += this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected         += this.HandleItemSelected;
            this.subPanel.OnEntryFocused         += this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed  += this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed += this.OnConfirmPerformed;
            }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected     -= this.HandleOperatorSelected;
            this.commandPanel.OnCommandSelected  -= this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused     -= this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected         -= this.HandleItemSelected;
            this.subPanel.OnEntryFocused         -= this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed  -= this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed -= this.OnConfirmPerformed;
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

                case CombatMenuState.CommandPanel:
                    this.commandPanel.Hide();
                    this.menuView.SetDimmed(false);
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

        private void HandleOperatorSelected(int index)
        {
            this.selectedOperator = index;
            this.commandPanel.Show(this.menuView.GetOperatorRect(index));
            this.menuView.SetDimmed(true);
            this.state = CombatMenuState.CommandPanel;
        }

        private void HandleCommandSelected(CombatCommand command)
        {
            if (this.state != CombatMenuState.CommandPanel) return;

            if (command == CombatCommand.Shoot)
            {
                this.commandPanel.SetDimmed(true);
                this.aimView.OnShotFired += this.HandleShotFired;
                this.aimView.Show();
                this.state = CombatMenuState.Aiming;
                return;
            }

            this.commandPanel.SetDimmed(true);
            this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
            this.state = CombatMenuState.SubPanel;
        }

        private void HandleItemSelected(int index) { }

        private void OnConfirmPerformed(InputAction.CallbackContext _)
        {
            if (this.state == CombatMenuState.Aiming)
                this.aimView.Confirm();
        }

        private void HandleShotFired(Vector2 _)
        {
            this.aimView.OnShotFired -= this.HandleShotFired;
            this.aimView.Hide();
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            this.menuView.FocusOperator(this.selectedOperator);
            this.state = CombatMenuState.OperatorSelection;
        }

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
