using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorEngine
{

    

#if ENABLE_INPUT_SYSTEM
    public class InputActionProcessor
    {
        public int FrameDown;
        public int FrameUp;
        
        public bool IsDown() { return FrameDown == Time.frameCount; }
        public bool IsUp() { return FrameUp == Time.frameCount; }
        public bool IsHeld() { return FrameDown != -1 && FrameUp == -1; }

        public void Process(bool pressed)
        {
            if (IsHeld() && !pressed)
            {
                FrameDown = -1;
                FrameUp = Time.frameCount;
            }
            else if (pressed)
            {
                FrameUp = -1;
                FrameDown = Time.frameCount;
            }
        }

        public void Clear()
        {
            FrameDown = -1;
            FrameUp = -1;
        }
    }

    public class PlayerInputListener : MonoBehaviour, IPlayerInput, PlayerActions.IGameplayActions
    {
        private Vector2 m_InputAxis;
        private Vector2 m_InputSecondaryAxis;
        private InputActionProcessor m_AimingP = new InputActionProcessor();
        private InputActionProcessor m_AttackP = new InputActionProcessor();
        private InputActionProcessor m_InteractP = new InputActionProcessor();
        private InputActionProcessor m_RunP = new InputActionProcessor();
        private InputActionProcessor m_ReloadP = new InputActionProcessor();
        private InputActionProcessor m_Turn180P = new InputActionProcessor();
        private InputActionProcessor m_ChangeAimTargetP = new InputActionProcessor();

        private PlayerActions m_Actions;

        private void Awake()
        {
            MessageBuffer<GameUnpausedMessage>.Subscribe(OnGameUnpaused);
        }

        private void Start()
        {
            m_Actions = new PlayerActions();
            m_Actions.Gameplay.SetCallbacks(this);
            m_Actions.Enable();
        }

        void OnGameUnpaused(GameUnpausedMessage msg)
        {
            Flush();
        }

        // ------------------------------------ SendMessages from PlayerInput component

        public void OnPrimaryAxis(InputAction.CallbackContext context)
        {
            m_InputAxis = context.ReadValue<Vector2>();
        }
        public void OnSecondaryAxis(InputAction.CallbackContext context)
        {
            m_InputSecondaryAxis = context.ReadValue<Vector2>();
        }

        public void OnAiming(InputAction.CallbackContext context)
        {
            m_AimingP.Process(context.ReadValueAsButton());
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            m_AttackP.Process(context.ReadValueAsButton());
        }
        public void OnInteract(InputAction.CallbackContext context)
        {
            m_InteractP.Process(context.ReadValueAsButton());
        }
        public void OnRun(InputAction.CallbackContext context)
        {
            m_RunP.Process(context.ReadValueAsButton());
        }
        public void OnReload(InputAction.CallbackContext context)
        {
            m_ReloadP.Process(context.ReadValueAsButton());
        }

        public void OnTurn180(InputAction.CallbackContext context)
        {
            m_Turn180P.Process(context.ReadValueAsButton());
        }

        public void OnChangeAimTarget(InputAction.CallbackContext context)
        {
            m_ChangeAimTargetP.Process(context.ReadValueAsButton());
        }

        // ------------------------------------ IPlayerInput implementation

        public Vector2 GetPrimaryAxis()
        {
            return m_InputAxis;
        }

        public Vector2 GetSecondaryAxis()
        {
            return m_InputSecondaryAxis;
        }

        public bool IsAimingHeld()
        {
            return m_AimingP.IsHeld();
        }

        public bool IsAttackDown()
        {
            return m_AttackP.IsDown();
        }

        public bool IsAttackUp()
        {
            return m_AttackP.IsUp();
        }

        public bool IsInteractingDown()
        {
            return m_InteractP.IsDown();
        }

        public bool IsRunHeld()
        {
            return m_RunP.IsHeld();
        }

        public bool IsReloadDown()
        {
            return m_ReloadP.IsDown();
        }

        public bool IsTurn180Down()
        {
            return m_Turn180P.IsDown();
        }


        public bool IsChangeAimTargetDown()
        {
            return m_ChangeAimTargetP.IsDown();
        }

        public void Flush()
        {
            m_AimingP.Clear();
            m_AttackP.Clear();
            m_InteractP.Clear();
            m_RunP.Clear();
            m_ReloadP.Clear();
            m_Turn180P.Clear();
            m_ChangeAimTargetP.Clear();
        }

        public InputSchemeHandle GetControlScheme()
        {
            return ControlSchemeDetector.CurrentControlScheme;   
        }
    }
#else
    public class PlayerInputNew : MonoBehaviour { }
#endif
}

