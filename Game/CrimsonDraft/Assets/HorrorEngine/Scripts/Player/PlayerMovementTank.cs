using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerMovementTank : MonoBehaviour, IPlayerMovementSettings
    {
        [SerializeField] float m_RotationSpeed = 180f;
        [SerializeField] float m_MinInputMovementThreshold = 0.5f;
        [SerializeField] float m_MinInputRotationThreshold = 0.15f;
        
        public PlayerMovementType GetMovementType()
        {
            return PlayerMovementType.Tank;
        }

        public float GetFwdRate(PlayerMovement movement)
        {
            if (Mathf.Abs(movement.InputAxis.y) < m_MinInputMovementThreshold)
                return 0f;

            return movement.InputAxis.y;
        }

        public float GetRightRate(PlayerMovement movement)
        {
            return 0f;
        }

        public void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
            if (Mathf.Abs(movement.InputAxis.x) < m_MinInputRotationThreshold)
            {
                sign = 0f;
                rate = 0f;
                return;
            }

            sign = movement.InputAxis.x;
            rate = m_RotationSpeed;
        }

        public void ResetMovement()
        {
            // Do nothing
        }
    }
}