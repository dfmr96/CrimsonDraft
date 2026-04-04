using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

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
            
            var cam = CameraSystem.Instance.ActiveCamera;

            // No movement if input is not enough
            float inputLength = input.magnitude;
            if (inputLength < MinInputMovementThreshold)
            {
                m_LockedCam = null; // Clear camera lock
                m_LastRefreshedCam = null;
                return Vector3.zero;
            }

            if (IsLockingEnabled)
            {
                // Lock input when entering a different camera
                if (cam && cam != m_LastRefreshedCam && !m_LockedCam && m_LastRefreshedCam != null)
                {
                    m_LockedInput = input;
                    m_LockedCam = m_LastRefreshedCam;
                    m_LastCameraChange = Time.time;
                }

                m_LastRefreshedCam = cam; // Cache last frame camera
                
                // Ensure locked input is some direction for angle calculation
                if (m_LockedInput == Vector2.zero)
                    m_LockedInput = Vector2.up;

                // Clear cam when the input changes too much
                float angle = Vector2.Angle(input, m_LockedInput);
                if (angle > InputUnlockAngleThreshold)
                {
                    m_LockedCam = null;
                }

                // When locked, the input can't change direction but can only be reduced in magnitude to prevent sudden movement direction change when stick is slightly moved while in a different cam
                if (m_LockedCam)
                {
                    m_LockedInput = m_LockedInput.normalized * inputLength;
                    input = m_LockedInput;
                }
            }



            Transform inputCam = CameraSystem.Instance.MainCamera.transform;

            if (IsLockingEnabled)
            {
                if (m_LockedCam) 
                {
                    inputCam = m_LockedCam.transform;
                }
                else if (m_LastRefreshedCam)
                {
                    inputCam = m_LastRefreshedCam.transform;
                }
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

        public void Copy(CamLockedInput camLocked)
        {
            IsLockingEnabled = camLocked.IsLockingEnabled;
            MinInputMovementThreshold = camLocked.MinInputMovementThreshold;
            MinInputRotationThreshold = camLocked.MinInputRotationThreshold;
            InputUnlockAngleThreshold = camLocked.InputUnlockAngleThreshold;
            AutoBlendToNewCamera = camLocked.AutoBlendToNewCamera;
            LerpTimeAfterCameraChange = camLocked.LerpTimeAfterCameraChange;
        }

        public void ShowDebug()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.yellow;

            GUILayout.BeginArea(new Rect(10, 10, Screen.width, Screen.height));

            GUILayout.Label("CamLockInput Controller", style);
            GUILayout.Label("-------------------------------", style);
            GUILayout.Label("LockedCam: " + (m_LockedCam ? m_LockedCam.transform.parent : "NULL"), style);
            GUILayout.Label("LockedInput: " + m_LockedInput, style);
            GUILayout.Label("LastRefreshedCam: " + (m_LastRefreshedCam ? m_LastRefreshedCam.transform.parent : "NULL"), style);

            GUILayout.EndArea();
        }
    }

    public class PlayerMovementAlternate : MonoBehaviour, IPlayerMovementSettings
    {
        private static readonly string k_DebugCategory = "PlayerMovementAlternate";

        [SerializeField] AnimationCurve m_RotationSpeedOverAngle = AnimationCurve.Linear(0, 0, 360, 1384);
        [SerializeField] float m_AimingRotationSpeed = 180f;
        [SerializeField] List<ActorState> m_TankRotationStates; // TODO - Replace this with StateRotationPolicy
        [SerializeField] List<PlayerStateRotationOverride> m_RotationOverrides;
        [SerializeField] CamLockedInput m_CameraLockedInput;

        private ActorStateController m_StateController;
        private ActorState m_CurrentState;
        private PlayerStateRotationOverride m_RotationOverride;
        

        public CamLockedInput CamLockedInput => m_CameraLockedInput;

        private void Awake()
        {
            m_StateController = GetComponent<ActorStateController>();
        }

        // --------------------------------------------------------------------

        public PlayerMovementType GetMovementType()
        {
            return PlayerMovementType.Alternate;
        }

        // --------------------------------------------------------------------

        public float GetFwdRate(PlayerMovement movement)
        {
            Vector3 moveAxis = movement.InputAxis;
            if (moveAxis.magnitude > 1f)
                moveAxis.Normalize();

            Vector3 movementDir = m_CameraLockedInput.CalculateDirFromCamera(movement.transform.position, moveAxis);
            RuntimeDebug.DrawLine(movement.transform.position, movement.transform.position + movementDir, Color.blue, 0, k_DebugCategory);
            return Vector3.Dot(movement.transform.forward, movementDir);
        }

        // --------------------------------------------------------------------

        public float GetRightRate(PlayerMovement movement)
        {
            Vector3 moveAxis = movement.InputAxis;
            if (moveAxis.magnitude > 1f)
                moveAxis.Normalize();

            Vector3 movementDir = m_CameraLockedInput.CalculateDirFromCamera(movement.transform.position, moveAxis);
            RuntimeDebug.DrawLine(movement.transform.position, movement.transform.position + movementDir, Color.green, 0, k_DebugCategory);
            RuntimeDebug.DrawWireSphere(movement.transform.position + movementDir, 0.25f, Color.green, Vector3.one, 0, k_DebugCategory);
            return Vector3.Dot(movement.transform.right, movementDir);
        }

        // --------------------------------------------------------------------

        public void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
           
            var currentState = (ActorState)m_StateController.CurrentState;

            if (currentState != m_CurrentState)
            {
                m_CurrentState = currentState;
                m_RotationOverride = GetStateRotationOverride(currentState);
            }

            if (m_RotationOverride)
            {
                m_RotationOverride.GetRotation(movement, out sign, out rate);
                return;
            }
            else if (m_TankRotationStates.Contains((ActorState)m_StateController.CurrentState)) // TODO - Make all these RotationOverrides
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

        // --------------------------------------------------------------------

        private PlayerStateRotationOverride GetStateRotationOverride(ActorState currentState)
        {
            foreach(var rotOverride in m_RotationOverrides)
            {
                if (rotOverride.States.Contains(currentState))
                {
                    return rotOverride;
                }
            }

            return null;
        }

        // --------------------------------------------------------------------

        public void ResetMovement()
        {
            m_CameraLockedInput.Clear();
        }

        private void OnGUI()
        {
            if (RuntimeDebug.IsCategoryEnabled(k_DebugCategory))
            {
                m_CameraLockedInput.ShowDebug();
            }
        }
    }
}