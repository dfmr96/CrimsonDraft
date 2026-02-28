#nullable enable

using System;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        private const string GameplayMapName = "Gameplay";
        private const string CombatMapName   = "Combat";
        private const string UIMapName       = "UI";

        private const string NavigateAction = "Navigate";
        private const string ConfirmAction  = "Confirm";
        private const string CancelAction   = "Cancel";
        private const string UseItemAction  = "UseItem";

        private readonly InputActionAsset asset;
        private readonly InputActionMap gameplayMap;
        private readonly InputActionMap combatMap;
        private readonly InputActionMap uiMap;

        public InputAction Move { get; }
        public InputAction Interact { get; }
        public InputAction OpenInventory { get; }
        public InputAction Pause { get; }
        public InputAction CombatNavigate { get; }
        public InputAction CombatConfirm { get; }
        public InputAction CombatCancel { get; }
        public InputAction CombatUseItem { get; }

        [Preserve]
        public InputService(InputActionAsset asset)
        {
            this.asset = asset;
            this.gameplayMap = asset.FindActionMap(GameplayMapName, throwIfNotFound: true);
            this.combatMap   = asset.FindActionMap(CombatMapName,   throwIfNotFound: true);
            this.uiMap       = asset.FindActionMap(UIMapName,        throwIfNotFound: true);

            Move          = this.gameplayMap[nameof(Move)];
            Interact      = this.gameplayMap[nameof(Interact)];
            OpenInventory = this.gameplayMap[nameof(OpenInventory)];
            Pause         = this.gameplayMap[nameof(Pause)];

            CombatNavigate = this.combatMap[NavigateAction];
            CombatConfirm  = this.combatMap[ConfirmAction];
            CombatCancel   = this.combatMap[CancelAction];
            CombatUseItem  = this.combatMap[UseItemAction];
        }

        void IInitializable.Initialize() => SwitchToGameplay();

        public void SwitchToGameplay()
        {
            DisableAll();
            this.gameplayMap.Enable();
        }

        public void SwitchToCombat()
        {
            DisableAll();
            this.combatMap.Enable();
        }

        public void SwitchToUI()
        {
            DisableAll();
            this.uiMap.Enable();
        }

        void IDisposable.Dispose()
        {
            DisableAll();
            this.asset.Disable();
        }

        private void DisableAll()
        {
            this.gameplayMap.Disable();
            this.combatMap.Disable();
            this.uiMap.Disable();
        }
    }
}
