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
        InputAction Pause { get; }

        InputAction CombatNavigate { get; }
        InputAction CombatConfirm { get; }
        InputAction CombatCancel { get; }
        InputAction CombatUseItem { get; }

        void SwitchToGameplay();
        void SwitchToCombat();
        void SwitchToUI();
    }
}
