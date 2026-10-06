using UnityEngine;

namespace HorrorEngine
{
    public class PlayerInputListenerOld : MonoBehaviour, IPlayerInput
    {
        static readonly float k_AxisActionThreshold = 0.5f;

        [SerializeField] string m_XPrimaryAxis;
        [SerializeField] string m_YPrimaryAxis;
        [SerializeField] string m_XSecondaryAxis;
        [SerializeField] string m_YSecondaryAxis;
        [SerializeField] string m_Aiming;
        [SerializeField] string m_Attack;
        [SerializeField] string m_Interact;
        [SerializeField] string m_Run;
        [SerializeField] string m_Reload;
        [SerializeField] string m_Turn180;
        [SerializeField] string m_ChangeAimTarget;

        private void Awake()
        {
            MessageBuffer<GameUnpausedMessage>.Subscribe(OnGameUnpaused);
        }

        void OnGameUnpaused(GameUnpausedMessage msg)
        {
            Flush();
        }

        public Vector2 GetPrimaryAxis()
        {
            return new Vector2(Input.GetAxis(m_XPrimaryAxis), Input.GetAxis(m_YPrimaryAxis));
        }
        public Vector2 GetSecondaryAxis()
        {
            return new Vector2(Input.GetAxis(m_XSecondaryAxis), Input.GetAxis(m_YSecondaryAxis));
        }
        public bool IsAimingHeld()
        {
            return Input.GetAxis(m_Aiming) > k_AxisActionThreshold || Input.GetButton(m_Aiming);
        }

        public bool IsAttackDown()
        {
            return Input.GetButtonDown(m_Attack);
        }

        public bool IsAttackUp()
        {
            return Input.GetButtonUp(m_Attack);
        }


        public bool IsInteractingDown()
        {
            return Input.GetButtonDown(m_Interact);
        }

        public bool IsRunHeld()
        {
            return Input.GetButton(m_Run);
        }

        public bool IsReloadDown()
        {
            return Input.GetButtonDown(m_Reload);
        }

        public bool IsTurn180Down()
        {
            return Input.GetAxis(m_Turn180) > k_AxisActionThreshold || Input.GetButtonDown(m_Turn180);
        }

        public bool IsChangeAimTargetDown()
        {
            return Input.GetButtonDown(m_ChangeAimTarget);
        }

        public void Flush()
        {
            Input.ResetInputAxes();
        }

        public InputSchemeHandle GetControlScheme()
        {
            return null;
        }
    }
}