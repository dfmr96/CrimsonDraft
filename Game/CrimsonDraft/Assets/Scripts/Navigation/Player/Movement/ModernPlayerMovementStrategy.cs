#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Camera-relative movement -- extracted from PlayerController unchanged. See
    // ICameraRelativeMovementService for the "held direction survives a fixed-camera cut"
    // policy this wraps.
    public sealed class ModernPlayerMovementStrategy : IPlayerMovementStrategy
    {
        private readonly ICameraRelativeMovementService cameraRelativeMovementService;

        public ModernPlayerMovementStrategy(ICameraRelativeMovementService cameraRelativeMovementService)
        {
            this.cameraRelativeMovementService = cameraRelativeMovementService;
        }

        public PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime)
        {
            var direction = lastDevice is Gamepad
                ? rawInput.normalized
                : Quantize8Way(rawInput);

            // Always ticked, even while aiming or at rest -- a held direction that changes has
            // to be caught the instant it happens, whether or not the player can currently act
            // on it (see CameraRelativeMovementService.ShouldResampleBasis).
            this.cameraRelativeMovementService.Tick(direction);

            if (isAiming) return PlayerMovementResult.Idle;

            var moveDir = this.cameraRelativeMovementService.Right * direction.x
                        + this.cameraRelativeMovementService.Forward * direction.y;
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            if (moveDir != Vector3.zero)
                playerTransform.forward = moveDir;

            return new PlayerMovementResult(moveDir, allowSprint: true);
        }

        private static Vector2 Quantize8Way(Vector2 input) =>
            new Vector2(Mathf.Round(input.x), Mathf.Round(input.y)).normalized;
    }
}
