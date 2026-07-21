#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using CrimsonDraft.Audio;
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
        internal ICombatOrchestrator Orchestrator { get; private set; } = null!;
        internal List<int> FocusFireMarked { get; } = new();

        internal const int BaseDamage   = 20;
        internal const int MaxShotCount = 6;
        internal const int DefaultAmmo  = 6;

        #endregion

        #region State machine

        private ICombatMenuState currentState = null!;

        internal OperatorSelectionState  OperatorSelState      { get; private set; } = null!;
        internal CommandPanelState       CommandPanelState     { get; private set; } = null!;
        internal CombatInventoryState    CombatInventoryState  { get; private set; } = null!;
        internal ShotCountSelectionState ShotCountState        { get; private set; } = null!;
        internal TargetSelectionState    TargetSelState        { get; private set; } = null!;
        internal AimingState             AimingState           { get; private set; } = null!;

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
        private readonly ICombatInventoryView          combatInventoryView;
        private readonly IShotCountView                shotCountView;
        private readonly IPublisher<CombatEndedEvent>  combatEndedPublisher;
        private readonly IInputService?                inputService;
        private readonly IAimView                      aimView;
        private readonly IBattlefieldView              battlefieldView;
        private readonly IOperatorRoster               roster;
        private readonly IInventoryService             inventory;
        private readonly ICombatOrchestrator                           orchestrator;
        private readonly ISubscriber<ShootConfigurationRequestedEvent> shootSubscriber;
        private readonly CombatSfxData?                                sfx;
        private IDisposable? shootSubscription;

        [UnityEngine.Scripting.Preserve]
        public CombatMenuController(
            ICombatActionMenuView                          menuView,
            ICommandPanelView                              commandPanel,
            ICombatInventoryView                           combatInventoryView,
            IShotCountView                                 shotCountView,
            IPublisher<CombatEndedEvent>                   combatEndedPublisher,
            IAimView                                       aimView,
            IBattlefieldView                               battlefieldView,
            IOperatorRoster                                roster,
            IInventoryService                              inventory,
            IInputService                                  inputService,
            ICombatOrchestrator                            orchestrator,
            CombatSfxData                                  sfx,
            ISubscriber<ShootConfigurationRequestedEvent>  shootSubscriber)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.combatInventoryView  = combatInventoryView;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.roster               = roster;
            this.inventory            = inventory;
            this.inputService         = inputService;
            this.orchestrator         = orchestrator;
            this.sfx                  = sfx;
            this.shootSubscriber      = shootSubscriber;
        }

        // Internal constructor for tests (no inputService)
        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ICombatInventoryView         combatInventoryView,
            IShotCountView               shotCountView,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView,
            IOperatorRoster              roster,
            IInventoryService            inventory,
            ICombatOrchestrator?         orchestrator    = null,
            ISubscriber<ShootConfigurationRequestedEvent>? shootSubscriber = null,
            CombatSfxData?               sfx             = null)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.combatInventoryView  = combatInventoryView;
            this.shotCountView        = shotCountView;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.roster               = roster;
            this.inventory            = inventory;
            this.orchestrator         = orchestrator!;
            this.shootSubscriber      = shootSubscriber!;
            this.sfx                  = sfx;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.OperatorSelState     = new OperatorSelectionState(this, this.menuView, this.commandPanel, this.battlefieldView, this.combatEndedPublisher, this.roster, this.sfx);
            this.CommandPanelState    = new CommandPanelState(this, this.menuView, this.commandPanel, this.battlefieldView, this.roster, this.sfx);
            this.CombatInventoryState = new CombatInventoryState(this, this.combatInventoryView, this.menuView);
            this.ShotCountState       = new ShotCountSelectionState(this, this.commandPanel, this.shotCountView, this.battlefieldView, this.aimView, this.roster, this.sfx);
            this.TargetSelState       = new TargetSelectionState(this, this.commandPanel, this.battlefieldView, this.aimView, this.roster, this.sfx);
            this.AimingState          = new AimingState(this, this.menuView, this.commandPanel, this.battlefieldView, this.aimView, this.roster, this.sfx);

            this.menuView.OnOperatorSelected    += this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     += this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected += this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    += this.HandleCommandEntryFocused;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed   += this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed  += this.OnConfirmPerformed;
                this.inputService.CombatNavigate.performed += this.OnNavigatePerformed;
            }

            this.Orchestrator      = this.orchestrator;
            this.shootSubscription = this.shootSubscriber?.Subscribe(e => BeginShootConfiguration(e.OperatorSlot));

            this.TransitionTo(this.OperatorSelState);
        }

        #endregion

        #region IDisposable

        void System.IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected    -= this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     -= this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected -= this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    -= this.HandleCommandEntryFocused;

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

        // Entry-triggered auto-focus (state Enter() selecting the first operator/command) fires
        // the same view events as real cursor navigation. CombatActionMenuView/FocusRequestPending
        // already guarantees only one such event fires per entry, so a one-shot suppression flag
        // — consumed by whichever focus event arrives first, however it arrives (synchronously from
        // Enter(), or later once ATB marks an operator ready) — is enough to silence it.
        private int            lastFocusedOperatorIndex = -1;
        private RectTransform? lastFocusedCommandAnchor;
        private bool           suppressNextOperatorFocusSfx;
        private bool           suppressNextCommandFocusSfx;

        internal void SuppressNextOperatorFocusSfx() => this.suppressNextOperatorFocusSfx = true;
        internal void SuppressNextCommandFocusSfx()  => this.suppressNextCommandFocusSfx  = true;

        internal void BeginShootConfiguration(int slot)
        {
            this.SelectedOperator = slot;
            this.commandPanel.RepositionTo(this.menuView.GetOperatorRect(slot));
            this.menuView.SetDimmed(true);
            this.TransitionTo(this.ShotCountState);
        }

        internal static int ComputeShotDamage(ShotZone zone, float precisionMultiplier, int baseDamage = BaseDamage)
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
            return Mathf.RoundToInt(baseDamage * zoneMult * precisionMultiplier);
        }

        internal static int ComputePoiseDamage(ShotZone zone, int weaponPoiseDamage) =>
            zone == ShotZone.Legs ? weaponPoiseDamage * 2 : weaponPoiseDamage;

        internal static bool ShouldStagger(int poiseAfterDamage, int currentHp, int maxHp, float staggerHpThresholdPct)
        {
            if (poiseAfterDamage > 0) return false;
            float hpPct = maxHp > 0 ? (float)currentHp / maxHp * 100f : 0f;
            return hpPct < staggerHpThresholdPct;
        }

        #endregion

        #region Event handlers (forward to current state)

        private void OnCancelPerformed(InputAction.CallbackContext _)     => this.currentState.OnCancel();
        private void OnConfirmPerformed(InputAction.CallbackContext _)    => this.currentState.OnConfirm();
        private void OnNavigatePerformed(InputAction.CallbackContext ctx)  =>
            this.currentState.OnNavigate(ctx.ReadValue<Vector2>());

        private void HandleOperatorSelected(int index)        => this.currentState.OnOperatorSelected(index);
        private void HandleCommandSelected(CombatCommand cmd) => this.currentState.OnCommandSelected(cmd);

        private void HandleOperatorFocused(int index)
        {
            if (this.suppressNextOperatorFocusSfx)
            {
                this.suppressNextOperatorFocusSfx = false;
                this.lastFocusedOperatorIndex      = index;
            }
            else if (index != this.lastFocusedOperatorIndex)
            {
                this.sfx?.PlayCursor(this.commandPanel.PanelRect.gameObject);
                this.lastFocusedOperatorIndex = index;
            }
            this.currentState.OnOperatorFocused(index);
        }

        private void HandleCommandEntryFocused(RectTransform anchor)
        {
            if (this.suppressNextCommandFocusSfx)
            {
                this.suppressNextCommandFocusSfx = false;
                this.lastFocusedCommandAnchor     = anchor;
            }
            else if (anchor != this.lastFocusedCommandAnchor)
            {
                this.sfx?.PlayCursor(this.commandPanel.PanelRect.gameObject);
                this.lastFocusedCommandAnchor = anchor;
            }
            this.menuView.MoveSelectorTo(anchor);
        }

        #endregion
    }
}
