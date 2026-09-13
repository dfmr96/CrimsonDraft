#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Resident Evil-style tank controls: rotate in place, walk/run only along the character's
    // own current facing. See [[Sistema de Movimiento]] "Esquema Classic -- Tank Controls".
    public sealed class ClassicPlayerMovementStrategy : IPlayerMovementStrategy
    {
        // Placeholders -- no feel pass yet, see Sistema de Movimiento "Pendiente".
        private const float TurnSpeedDegPerSec = 180f;
        private const float AxisThreshold      = 0.1f;

        public PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime)
        {
            if (isAiming) return PlayerMovementResult.Idle;

            if (Mathf.Abs(rawInput.x) > AxisThreshold)
                playerTransform.Rotate(Vector3.up, rawInput.x * TurnSpeedDegPerSec * deltaTime, Space.World);

            if (rawInput.y > AxisThreshold)
                return new PlayerMovementResult(playerTransform.forward, allowSprint: true);

            // Backpedal is always walk speed -- running backward was never possible in the
            // classic control scheme this is modeled on.
            if (rawInput.y < -AxisThreshold)
                return new PlayerMovementResult(-playerTransform.forward, allowSprint: false);

            return PlayerMovementResult.Idle;
        }
    }
}
