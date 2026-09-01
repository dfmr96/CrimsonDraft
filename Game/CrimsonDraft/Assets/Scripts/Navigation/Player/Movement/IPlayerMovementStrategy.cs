#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Called every FixedUpdate, unconditionally -- including frames where the caller won't
    // act on the result (aiming, stick at rest). ModernPlayerMovementStrategy relies on this:
    // its internal camera-basis bookkeeping must never miss a held-direction change, even
    // while the player is aiming. A strategy may mutate playerTransform's rotation directly
    // (Modern snaps facing to the move direction; Classic turns gradually) as a side effect.
    public interface IPlayerMovementStrategy
    {
        PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime);
    }
}
