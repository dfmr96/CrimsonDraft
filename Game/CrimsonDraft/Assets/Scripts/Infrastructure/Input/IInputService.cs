#nullable enable

using System;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    public interface IInputService : IDisposable
    {
        InputAction Move { get; }
        InputAction Interact { get; }
        InputAction OpenInventory { get; }
        InputAction OpenMap { get; }
        InputAction Aim { get; }
        InputAction Pause { get; }
        InputAction Sprint { get; }

        InputAction CombatNavigate { get; }
        InputAction CombatConfirm { get; }
        InputAction CombatCancel { get; }
        InputAction CombatUseItem { get; }

        InputAction UINavigate { get; }
        InputAction UIConfirm  { get; }
        InputAction UICancel   { get; }
        InputAction UIBack     { get; }

        InputAction DialogueAdvanceLine    { get; }
        InputAction DialogueCancelDialogue { get; }

        void SwitchToGameplay();
        void SwitchToCombat();
        void SwitchToUI();
        void SwitchToDialogue();
    }
}
