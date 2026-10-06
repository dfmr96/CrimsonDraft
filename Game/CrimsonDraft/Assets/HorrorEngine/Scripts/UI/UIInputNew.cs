using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorEngine
{
#if ENABLE_INPUT_SYSTEM
    public class UIInputNew : MonoBehaviour, IUIInput, UIActions.IGameplayActions, UIActions.INavigationActions
    {
        private UIActions m_Actions;
        private Vector2 m_InputAxis;

        private InputActionProcessor m_CancelP = new InputActionProcessor();
        private InputActionProcessor m_ConfirmP = new InputActionProcessor();
        private InputActionProcessor m_ToggleInventoryP = new InputActionProcessor();
        private InputActionProcessor m_ToggleMapP = new InputActionProcessor();
        private InputActionProcessor m_ToggleMapListP = new InputActionProcessor();
        private InputActionProcessor m_TogglePauseP = new InputActionProcessor();
        private InputActionProcessor m_NextSubmapP = new InputActionProcessor();
        private InputActionProcessor m_PrevSubmapP = new InputActionProcessor();

        private InputSchemeChangedMessage m_SchemeChangedMsg = new InputSchemeChangedMessage();

        private void Start()
        {
            m_Actions = new UIActions();
            m_Actions.Navigation.SetCallbacks(this);
            m_Actions.Gameplay.SetCallbacks(this);
            m_Actions.Enable();
        }

        // ------------------------------------- UIActions.IGameplayActions Implementation

        public void OnToggleMapList(InputAction.CallbackContext context)
        {
            OnToggleMapList(context.phase == InputActionPhase.Performed);
        }

        public void OnToggleMap(InputAction.CallbackContext context)
        {
            OnToggleMap(context.phase == InputActionPhase.Performed);
        }

        public void OnToggleInventory(InputAction.CallbackContext context)
        {
            OnToggleInventory(context.phase == InputActionPhase.Performed);
        }

        public void OnPrimaryAxis(InputAction.CallbackContext context)
        {
            OnPrimaryAxis(context.ReadValue<Vector2>());
        }

        public void OnTogglePause(InputAction.CallbackContext context)
        {
            OnTogglePause(context.phase == InputActionPhase.Performed);
        }

        public void OnNextSubmap(InputAction.CallbackContext context)
        {
            OnNextSubmap(context.phase == InputActionPhase.Performed);
        }

        public void OnPrevSubmap(InputAction.CallbackContext context)
        {
            OnPrevSubmap(context.phase == InputActionPhase.Performed);
        }

        public void OnSubmit(InputAction.CallbackContext context)
        {
            OnSubmit(context.phase == InputActionPhase.Performed);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            OnCancel(context.phase == InputActionPhase.Performed);
        }


        // ------------------------------------ SendMessages from PlayerInput component

        private void OnCancel(bool pressed)
        {
            m_CancelP.Process(pressed);
        }

        private void OnSubmit(bool pressed)
        {
            m_ConfirmP.Process(pressed);
        }

        private void OnToggleInventory(bool pressed)
        {
            m_ToggleInventoryP.Process(pressed);
        }

        private void OnToggleMap(bool pressed)
        {
            m_ToggleMapP.Process(pressed);
        }
        private void OnToggleMapList(bool pressed)
        {
            m_ToggleMapListP.Process(pressed);
        }

        private void OnTogglePause(bool pressed)
        {
            m_TogglePauseP.Process(pressed);
        }

        private void OnNextSubmap(bool pressed)
        {
            m_NextSubmapP.Process(pressed);
        }

        private void OnPrevSubmap(bool pressed)
        {
            m_PrevSubmapP.Process(pressed);
        }


        private void OnPrimaryAxis(Vector2 axis)
        {
            m_InputAxis = axis;
        }

        // ------------------------------------- IUIInput Implementation

        public bool IsCancelDown()
        {
            return m_CancelP.IsDown();
        }
        public bool IsConfirmDown()
        {
            return m_ConfirmP.IsDown();
        }

        public bool IsTogglePauseDown()
        {
            return m_TogglePauseP.IsDown();
        }

        public bool IsToggleInventoryDown()
        {
            return m_ToggleInventoryP.IsDown();
        }

        public bool IsToggleMapDown()
        {
            return m_ToggleMapP.IsDown();
        }

        public bool IsToggleMapListDown()
        {
            return m_ToggleMapListP.IsDown();
        }

        public Vector2 GetPrimaryAxis()
        {
            return m_InputAxis;
        }

        public bool IsPrevSubmapDown()
        {
            return m_PrevSubmapP.IsDown();
        }

        public bool IsNextSubmapDown()
        {
            return m_NextSubmapP.IsDown();
        }

        public void Flush()
        {
            m_CancelP.Clear();
            m_ConfirmP.Clear();
            m_ToggleInventoryP.Clear();
            m_TogglePauseP.Clear();
            m_ToggleMapP.Clear();
            m_ToggleMapListP.Clear();
        }


        // ------------------------------ Ignored Navigation callbacks

        public void OnNavigate(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnClick(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnScrollWheel(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnMiddleClick(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnTrackedDevicePosition(InputAction.CallbackContext context)
        {
            // Do Nothing
        }

        public void OnTrackedDeviceOrientation(InputAction.CallbackContext context)
        {
            // Do Nothing
        }
    }
#else
    public class UIInputNew : MonoBehaviour { }
#endif
}