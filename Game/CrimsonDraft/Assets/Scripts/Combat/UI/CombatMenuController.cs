#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, System.IDisposable
    {
        #region Shared state (accessed by state objects)

        internal int   SelectedOperator      { get; set; } = 0;
        internal int   SelectedShotCount     { get; set; } = 1;
        internal int   CurrentTargetSlot     { get; set; } = -1;
        internal int[] ReloadAmmoBoxIndices  { get; set; } = System.Array.Empty<int>();
        internal ICombatOrchestrator Orchestrator { get; private set; } = null!;

        internal const int BaseDamage   = 20;
        internal const int MaxShotCount = 6;
        internal const int DefaultAmmo  = 6;

        #endregion

        #region State machine

        private ICombatMenuState currentState = null!;

        internal OperatorSelectionState  OperatorSelState  { get; private set; } = null!;
        internal CommandPanelState       CommandPanelState { get; private set; } = null!;
        internal SubPanelState           SubPanelState     { get; private set; } = null!;
        internal ShotCountSelectionState ShotCountState    { get; private set; } = null!;
        internal TargetSelectionState    TargetSelState    { get; private set; } = null!;
        internal AimingState             AimingState       { get; private set; } = null!;

        public void TransitionTo(ICombatMenuState next)
        {
            this.currentState?.Exit();
            this.currentState = next;
            this.currentState.Enter();
        }

        #endregion

        #region Dependency injection

        private readonly ICombatActionMenuView         menuView;
        private readonly ICommandPanelView             commandPanel;
        private readonly ISubPanelView                 subPanel;
        private readonly IShotCountView                shotCountView;
        private readonly IPublisher<CombatEndedEvent>  combatEndedPublisher;
        private readonly IInputService?                inputService;
        private readonly IAimView                      aimView;
        private readonly IBattlefieldView              battlefieldView;
        private readonly IOperatorRoster               roster;
        private readonly IInventoryService             inventory;
        private readonly ICombatOrchestrator                           orchestrator;
        private readonly ISubscriber<ShootConfigurationRequestedEvent> shootSubscriber;
        private IDisposable? shootSubscription;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView                          menuView,
            ICommandPanelView                              commandPanel,
            ISubPanelView                                  subPanel,
            IShotCountView                                 shotCountView,
            IPublisher<CombatEndedEvent>                   combatEndedPublisher,
            IAimView                                       aimView,
            IBattlefieldView                               battlefieldView,
            IOperatorRoster                                roster,
            IInventoryService                              inventory,
            IInputService                                  inputService,
            ICombatOrchestrator                            orchestrator,
            ISubscriber<ShootConfigurationRequestedEvent>  shootSubscriber)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.roster               = roster;
            this.inventory            = inventory;
            this.inputService         = inputService;
            this.orchestrator         = orchestrator;
            this.shootSubscriber      = shootSubscriber;
        }

        // Internal constructor for tests (no inputService)
        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IShotCountView               shotCountView,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView,
            IOperatorRoster              roster,
            IInventoryService            inventory,
            ICombatOrchestrator?         orchestrator    = null,
            ISubscriber<ShootConfigurationRequestedEvent>? shootSubscriber = null)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.roster               = roster;
            this.inventory            = inventory;
            this.orchestrator         = orchestrator!;
            this.shootSubscriber      = shootSubscriber!;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.OperatorSelState  = new OperatorSelectionState(this, this.menuView, this.commandPanel, this.battlefieldView, this.combatEndedPublisher, this.roster);
            this.CommandPanelState = new CommandPanelState(this, this.menuView, this.commandPanel, this.subPanel, this.battlefieldView, this.roster, this.inventory);
            this.SubPanelState     = new SubPanelState(this, this.subPanel);
            this.ShotCountState    = new ShotCountSelectionState(this, this.commandPanel, this.shotCountView, this.battlefieldView, this.aimView, this.roster);
            this.TargetSelState    = new TargetSelectionState(this, this.commandPanel, this.battlefieldView, this.aimView, this.roster);
            this.AimingState       = new AimingState(this, this.menuView, this.commandPanel, this.battlefieldView, this.aimView, this.combatEndedPublisher, this.roster);

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

            this.Orchestrator      = this.orchestrator;
            this.shootSubscription = this.shootSubscriber.Subscribe(e => BeginShootConfiguration(e.OperatorSlot));

            this.TransitionTo(this.OperatorSelState);
        }

        #endregion

        #region IDisposable

        void System.IDisposable.Dispose()
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

            this.shootSubscription?.Dispose();
        }

        #endregion

        #region Internal API (testable)

        internal void HandleCancelPressed() => this.currentState.OnCancel();

        internal void BeginShootConfiguration(int slot)
        {
            this.SelectedOperator = slot;
            this.TransitionTo(this.ShotCountState);
        }

        internal static int ComputeShotDamage(ShotZone zone, float precisionMultiplier)
        {
            float zoneMult = zone switch
            {
                ShotZone.Head  => 2.0f,
                ShotZone.Torso => 1.0f,
                ShotZone.Arms  => 0.7f,
                ShotZone.Legs  => 0.8f,
                ShotZone.Hit   => 1.0f,
                _              => 0.0f,
            };
            return Mathf.RoundToInt(BaseDamage * zoneMult * precisionMultiplier);
        }

        #endregion

        #region Event handlers (forward to current state)

        private void OnCancelPerformed(InputAction.CallbackContext _)     => this.currentState.OnCancel();
        private void OnConfirmPerformed(InputAction.CallbackContext _)    => this.currentState.OnConfirm();
        private void OnNavigatePerformed(InputAction.CallbackContext ctx)  =>
            this.currentState.OnNavigate(ctx.ReadValue<Vector2>());

        private void HandleOperatorSelected(int index)        => this.currentState.OnOperatorSelected(index);
        private void HandleOperatorFocused(int index)         => this.currentState.OnOperatorFocused(index);
        private void HandleCommandSelected(CombatCommand cmd) => this.currentState.OnCommandSelected(cmd);
        private void HandleItemSelected(int index)            => this.currentState.OnItemSelected(index);

        #endregion
    }
}
