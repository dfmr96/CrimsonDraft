using System;
using UnityEngine;

namespace HorrorEngine
{
    public interface IPlayerMovementSettings
    {
        public PlayerMovementType GetMovementType();
        public float GetFwdRate(PlayerMovement movement);
        public float GetRightRate(PlayerMovement movement);
        public void GetRotation(PlayerMovement movement, out float sign, out float rate);
        void ResetMovement();
    }

    public class PlayerMovement : MonoBehaviour, IDeactivateWithActor
    {
        [Flags]
        public enum MovementConstrain
        {
            None            = 0,
            Movement        = 1,
            Rotation        = 2,
            MovementToAxis  = 4
        }

        public enum MovementInputType
        {
            Digital,
            Analog,
            AnalogAutoRun
        }

        [HideInInspector]
        public MovementConstrain Constrain = 0;

        [SerializeField] SettingsElementContent m_MovementTypeSetting;

        [Header("Main settings")]
        [SerializeField] float m_MovementSpeed;
        [SerializeField] float m_MovementRunSpeed;
        [SerializeField] float m_MovementBackwardsSpeed;
        [SerializeField] float m_MovementLateralSpeed;
        [SerializeField] float m_NavMeshCheckDistance;
        [SerializeField] MovementInputType m_MovementInputType;
        [SerializeField] Vector3 m_Gravity = new Vector3(0, -9.8f, 0);
        [SerializeField] bool m_RunInputAsToggle;
        

        [Header("Health Modifiers")]
        [SerializeField] bool m_ChangeWalkSpeedBasedOnHealth;
        [SerializeField] AnimationCurve m_NormalizedHealthSpeedScalar = AnimationCurve.Linear(0, 1f, 1f, 1f);
        [SerializeField] bool m_ChangeRunSpeedBasedOnHealth;
        [SerializeField] AnimationCurve m_NormalizedHealthRunSpeedScalar = AnimationCurve.Linear(0, 1f, 1f, 1f);

        private Vector2 m_InputAxis;
        private Vector2 m_InputSecondaryAxis;
        private IPlayerInput m_Input;
        private bool m_Running;
        private Health m_Health;
        private IPlayerMovementSettings m_Settings;
        private CharacterController m_CharacterCtrl;
        private PlayerStamina m_Stamina;
        private float m_DigitalThreshold = 0.5f;
        public Vector3 LockedAxis { get; set; }
        public Vector3 IntendedMovement { get; private set; }
        public Vector2 InputAxis => m_InputAxis;
        public Vector2 InputSecondaryAxis => m_InputSecondaryAxis;

        //Do not enable/disable the CharacterController here in OnEnable and OnDisable since this component
        //is disabled all the time (on all states where movement is not possible)
        //Doing so will cause the player cameras to not detect the player as CC is also a collider

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponent<IPlayerInput>();
            m_Health = GetComponent<Health>();
            m_Stamina = GetComponent<PlayerStamina>();
            m_CharacterCtrl = GetComponent<CharacterController>();
            
            UpdateMovementSettings();
            MessageBuffer<SettingsSavedMessage>.Subscribe(OnSettingsSavedMessage);
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_Settings.ResetMovement();
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<SettingsSavedMessage>.Unsubscribe(OnSettingsSavedMessage);
        }

        // --------------------------------------------------------------------

        private void OnSettingsSavedMessage(SettingsSavedMessage msg)
        {
            UpdateMovementSettings();
        }

        // --------------------------------------------------------------------

        private void UpdateMovementSettings()
        {
            var settings = GetComponentsInChildren<IPlayerMovementSettings>();
            Debug.Assert(settings.Length > 0, "Character doesn't have any movement settings component");
            if (m_MovementTypeSetting)
            {
                PlayerMovementType movementType = m_MovementTypeSetting.GetAsEnum<PlayerMovementType>();
                foreach (var moveSettings in settings)
                {
                    if (moveSettings.GetMovementType() == movementType)
                    {
                        m_Settings = moveSettings;
                        break;
                    }
                }

                Debug.Assert(m_Settings != null, $"Movement settings of type {movementType} couldn't be found on the player");
            }

            if (m_Settings == null && settings.Length > 0)
            {
                m_Settings = settings[0];
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_MovementInputType != MovementInputType.AnalogAutoRun)
            {
                if (!m_RunInputAsToggle)
                    m_Running = m_Input.IsRunHeld();
                else if (m_Input.IsRunDown())
                    m_Running = !m_Running;
            }

            m_InputAxis = m_Input.GetPrimaryAxis();
            m_InputSecondaryAxis = m_Input.GetSecondaryAxis();
        }

        // --------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (!Constrain.HasFlag(MovementConstrain.Movement))
                UpdateMovement();

            if (!Constrain.HasFlag(MovementConstrain.Rotation))
                UpdateRotation();
        }

        // --------------------------------------------------------------------

        private void UpdateRotation()
        {
            m_Settings.GetRotation(this, out float sign, out float rate);
            if (rate > 0)
                Rotate(sign, rate);
        }

        // --------------------------------------------------------------------

        public void Rotate(float dir, float speed)
        {
            Vector3 rotation = Vector3.up * dir * Time.deltaTime * speed;
            if (rotation != Vector3.zero)
            {
                transform.rotation = transform.rotation * Quaternion.Euler(rotation);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateMovement()
        {
            Vector3 fwdMove = GetForwardMovement(out float fwdSpeed);
            Vector3 rightMove = GetRightMovement(out float rightSpeed);

            Vector3 movement = fwdMove + rightMove;
            IntendedMovement = movement;

            float maxSpeed = Mathf.Max(fwdSpeed, rightSpeed) * Time.deltaTime;
            if (IntendedMovement.magnitude > maxSpeed)
            {
                IntendedMovement = IntendedMovement.normalized * maxSpeed;
            }
            
            if (Constrain.HasFlag(MovementConstrain.MovementToAxis))
            {
                IntendedMovement = Vector3.Project(IntendedMovement, LockedAxis);
            }

            Vector3 prevPos = transform.position;
            Vector3 newPos = prevPos + IntendedMovement;

            RuntimeDebug.DrawLine(prevPos, prevPos + IntendedMovement * 10, Color.red, 0, "PlayerMovement");

            Vector3 finalMove = newPos - prevPos;
            m_CharacterCtrl.Move(finalMove + m_Gravity * Time.deltaTime);
        }

        // --------------------------------------------------------------------

        Vector3 GetForwardMovement(out float speed)
        {
            float fwd = m_Settings.GetFwdRate(this);
            float absFwd = Mathf.Abs(fwd);
            
            speed = 0f;
            if (m_MovementInputType == MovementInputType.AnalogAutoRun)
            {
                if (fwd > Mathf.Epsilon)
                {
                    float fwdT = absFwd;
                    fwdT *= DepleteStamina(fwdT) ? 1f : 0f;

                    speed = Mathf.Lerp(m_MovementSpeed * absFwd, m_MovementRunSpeed * absFwd, fwdT);
                }
                else if (fwd < -Mathf.Epsilon)
                {
                    speed = m_MovementBackwardsSpeed * absFwd;
                }
            }
            else
            {
                float digitalInput = absFwd > m_DigitalThreshold ? 1f : 0f;
                if (fwd > Mathf.Epsilon)
                {
                    speed = m_Running && DepleteStamina(1f) ? m_MovementRunSpeed : m_MovementSpeed * (m_MovementInputType == MovementInputType.Analog ? absFwd : digitalInput);
                }
                else if (fwd < -Mathf.Epsilon)
                {
                    speed = m_MovementBackwardsSpeed * (m_MovementInputType == MovementInputType.Analog ? absFwd : digitalInput);
                }
            }

            if (speed >= m_MovementRunSpeed && m_ChangeRunSpeedBasedOnHealth)
                speed *= m_NormalizedHealthRunSpeedScalar.Evaluate(m_Health.Normalized);
            else if (m_ChangeRunSpeedBasedOnHealth)
                speed *= m_NormalizedHealthSpeedScalar.Evaluate(m_Health.Normalized);

            return transform.forward * Time.deltaTime * speed * Mathf.Sign(fwd);
        }

        // --------------------------------------------------------------------

        bool DepleteStamina(float rate)
        {
            if (m_Stamina)
                return m_Stamina.Deplete(rate);
            else
                return true;
        }

        // --------------------------------------------------------------------

        Vector3 GetRightMovement(out float speed)
        {
            float right = m_Settings.GetRightRate(this);
            float absRight = Mathf.Abs(right);

            speed = 0f;
            float digitalInput = absRight > m_DigitalThreshold ? 1f : 0f;
            if (right > Mathf.Epsilon || right < -Mathf.Epsilon)
                speed = m_MovementLateralSpeed * (m_MovementInputType == MovementInputType.Digital ? digitalInput : absRight);

            if (speed >= m_MovementRunSpeed && m_ChangeRunSpeedBasedOnHealth)
                speed *= m_NormalizedHealthRunSpeedScalar.Evaluate(m_Health.Normalized);
            else if (m_ChangeRunSpeedBasedOnHealth)
                speed *= m_NormalizedHealthSpeedScalar.Evaluate(m_Health.Normalized);

            return transform.right * Time.deltaTime * speed * Mathf.Sign(right);
        }

        // --------------------------------------------------------------------

        public void AddConstrain(MovementConstrain constrain) { Constrain |= constrain; }
        public void RemoveConstrain(MovementConstrain constrain) { Constrain &= ~constrain; }


        // --------------------------------------------------------------------

        void OnDrawGizmos()
        {
            Gizmos.DrawLine(transform.position - transform.right, transform.position + transform.right);
            Gizmos.DrawLine(transform.position - transform.forward, transform.position + transform.forward);
        }

    }
}