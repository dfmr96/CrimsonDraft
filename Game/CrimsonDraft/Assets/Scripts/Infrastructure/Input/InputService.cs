#nullable enable

using System;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        private const string GameplayMapName       = "Gameplay";
        private const string CombatMapName         = "Combat";
        private const string UIMapName             = "UI";
        private const string DialogueMapName       = "Dialogue";
        private const string DoorTransitionMapName = "DoorTransition";

        private const string NavigateAction = "Navigate";
        private const string ConfirmAction  = "Confirm";
        private const string CancelAction   = "Cancel";
        private const string UseItemAction  = "UseItem";

        private readonly InputActionAsset asset;
        private readonly InputActionMap gameplayMap;
        private readonly InputActionMap combatMap;
        private readonly InputActionMap uiMap;
        private readonly InputActionMap dialogueMap;
        private readonly InputActionMap doorTransitionMap;

        public InputAction Move { get; }
        public InputAction Interact { get; }
        public InputAction OpenInventory { get; }
        public InputAction OpenMap { get; }
        public InputAction Aim { get; }
        public InputAction Pause { get; }
        public InputAction Sprint { get; }
        public InputAction CombatNavigate { get; }
        public InputAction CombatConfirm { get; }
        public InputAction CombatCancel { get; }
        public InputAction CombatUseItem { get; }

        public InputAction UINavigate { get; }
        public InputAction UIConfirm  { get; }
        public InputAction UICancel   { get; }
        public InputAction UIBack     { get; }

        public InputAction DialogueAdvanceLine    { get; }
        public InputAction DialogueCancelDialogue { get; }

        public InputAction DoorTransitionSkip { get; }

        [Preserve]
        public InputService(InputActionAsset asset)
        {
            this.asset = asset;
            this.gameplayMap        = asset.FindActionMap(GameplayMapName,       throwIfNotFound: true);
            this.combatMap          = asset.FindActionMap(CombatMapName,         throwIfNotFound: true);
            this.uiMap              = asset.FindActionMap(UIMapName,             throwIfNotFound: true);
            this.dialogueMap        = asset.FindActionMap(DialogueMapName,       throwIfNotFound: true);
            this.doorTransitionMap  = asset.FindActionMap(DoorTransitionMapName, throwIfNotFound: true);

            Move          = this.gameplayMap[nameof(Move)];
            Interact      = this.gameplayMap[nameof(Interact)];
            OpenInventory = this.gameplayMap[nameof(OpenInventory)];
            OpenMap       = this.gameplayMap[nameof(OpenMap)];
            Aim           = this.gameplayMap[nameof(Aim)];
            Pause         = this.gameplayMap[nameof(Pause)];
            Sprint        = this.gameplayMap[nameof(Sprint)];

            CombatNavigate = this.uiMap[NavigateAction];
            CombatConfirm  = this.uiMap["Submit"];
            CombatCancel   = this.uiMap[CancelAction];
            CombatUseItem  = this.combatMap[UseItemAction];

            UINavigate = this.uiMap[NavigateAction];
            UIConfirm  = this.uiMap["Submit"];
            UICancel   = this.uiMap[CancelAction];
            UIBack     = this.uiMap["UIBack"];

            DialogueAdvanceLine    = this.dialogueMap["AdvanceLine"];
            DialogueCancelDialogue = this.dialogueMap["CancelDialogue"];

            DoorTransitionSkip = this.doorTransitionMap["Skip"];
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
            this.uiMap.Enable();
        }

        public void SwitchToUI()
        {
            DisableAll();
            this.uiMap.Enable();
        }

        public void SwitchToDialogue()
        {
            DisableAll();
            this.dialogueMap.Enable();
        }

        public void SwitchToDoorTransition()
        {
            DisableAll();
            this.doorTransitionMap.Enable();
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
            this.dialogueMap.Disable();
            this.doorTransitionMap.Disable();
        }
    }
}
