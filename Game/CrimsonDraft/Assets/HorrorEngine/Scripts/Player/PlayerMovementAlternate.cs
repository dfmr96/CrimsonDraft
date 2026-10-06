using UnityEngine;
using System.Collections.Generic;
using System;

namespace HorrorEngine
{
    [Serializable]
    public class CamLockedInput
    {
        public bool IsLockingEnabled = true;
        public float MinInputMovementThreshold = 0.5f;
        public float MinInputRotationThreshold = 0.15f;
        public float InputUnlockAngleThreshold = 15f;
        public bool AutoBlendToNewCamera;
        public float LerpTimeAfterCameraChange = 3f;

        private Behaviour m_LockedCam;
        private Behaviour m_LastRefreshedCam;
        private Vector2 m_LockedInput;
        private float m_LastCameraChange;

        public Vector3 CalculateDirFromCamera(Vector3 playerPos, Vector2 input)
        {
            // Ensure locked input is some direction for angle calculation
            if (m_LockedInput == Vector2.zero)
                m_LockedInput = Vector2.up;

            var cam = CameraSystem.Instance.ActiveCamera;

            if (IsLockingEnabled)
            {
                // Refresh locked input when entering a different camera to prevent an early change
                if (cam && cam != m_LastRefreshedCam && !m_LockedCam)
                {
                    m_LockedInput = input;
                    m_LockedCam = m_LastRefreshedCam;
                    m_LastRefreshedCam = cam;
                    m_LastCameraChange = Time.time;
                }

                // Clear cam if the stick when the input stops or changes too much
                float angle = Vector2.Angle(input, m_LockedInput);
                if (input.sqrMagnitude < MinInputMovementThreshold || angle > InputUnlockAngleThreshold)
                {
                    m_LockedCam = null;
                }
            }

            // No movement if input is not enough
            if (input.magnitude < MinInputMovementThreshold)
                return Vector3.zero;

            Transform inputCam = CameraSystem.Instance.MainCamera.transform;

            if (IsLockingEnabled)
            {
                inputCam = m_LockedCam ? m_LockedCam.transform : m_LastRefreshedCam.transform;
            }

            if (inputCam)
            {
                Vector3 fwd = inputCam.forward; 
                
                if (IsLockingEnabled && AutoBlendToNewCamera && m_LockedCam)
                {
                    float t = Mathf.InverseLerp(0, LerpTimeAfterCameraChange, Time.time - m_LastCameraChange);
                    fwd = Vector3.Lerp(m_LockedCam.transform.forward, m_LastRefreshedCam.transform.forward, t);
                }

                fwd.y = 0;
                fwd.Normalize();
                Vector3 right = -Vector3.Cross(fwd, Vector3.up);
                right.Normalize();

                // Use locked input when in different cam to avoid incorrect small rotation
                Vector3 movementDir = right * input.x + fwd * input.y;

                return movementDir;
            }
            else
            {
                return Vector3.zero;
            }
        }

        public void Clear()
        {
            m_LockedCam = null;
            m_LastRefreshedCam = null;
            m_LockedInput = Vector2.zero;
            m_LastCameraChange = 0f;
        }
    }

    public class PlayerMovementAlternate : MonoBehaviour, IPlayerMovementSettings
    {
        [SerializeField] AnimationCurve m_RotationSpeedOverAngle = AnimationCurve.Linear(0, 0, 360, 1384);
        [SerializeField] float m_AimingRotationSpeed = 180f;
        [SerializeField] List<ActorState> m_TankRotationStates;
        [SerializeField] CamLockedInput m_CameraLockedInput;

        private ActorStateController m_StateController;

        private void Awake()
        {
            m_StateController = GetComponent<ActorStateController>();
        }

        public PlayerMovementType GetMovementType()
        {
            return PlayerMovementType.Alternate;
        }

        public float GetFwdRate(PlayerMovement movement)
        {
            Vector3 moveAxis = movement.InputAxis;
            if (moveAxis.magnitude > 1f)
                moveAxis.Normalize();

            Vector3 movementDir = m_CameraLockedInput.CalculateDirFromCamera(movement.transform.position, moveAxis);
            Debug.DrawLine(movement.transform.position, movement.transform.position + movementDir, Color.magenta);
            return Vector3.Dot(movement.transform.forward, movementDir);
        }

        public float GetRightRate(PlayerMovement movement)
        {
            return 0f;
        }

        public void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
            if (m_TankRotationStates.Contains((ActorState)m_StateController.CurrentState))
            {
                if (Mathf.Abs(movement.InputAxis.x) < m_CameraLockedInput.MinInputRotationThreshold)
                {
                    sign = 0f;
                    rate = 0f;
                    return;
                }

                sign = movement.InputAxis.x;
                rate = m_AimingRotationSpeed;
                return;
            }
            else
            {
                sign = 0;
                rate = 0;

                if (movement.InputAxis.magnitude < m_CameraLockedInput.MinInputRotationThreshold)
                    return;

                Vector3 movementDir = m_CameraLockedInput.CalculateDirFromCamera(movement.transform.position, movement.InputAxis);
                float signedAngle = Vector3.SignedAngle(movement.transform.forward, movementDir, Vector3.up);
                //if (Mathf.Abs(signedAngle) > m_MinAngleRotationThreshold)
                {
                    sign = Mathf.Sign(signedAngle);
                    rate = m_RotationSpeedOverAngle.Evaluate(Mathf.Abs(signedAngle));
                }
            }
        }

        public void ResetMovement()
        {
            m_CameraLockedInput.Clear();
        }
    }
}