using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;


namespace HorrorEngine
{
    //Obsolete. Delete once TP/FP is updated
    public enum ControlScheme
    {
        None,
        KeyboardAndMouse,
        Gamepad
    }

    class InputSchemeChangedMessage : BaseMessage
    {
        public InputSchemeHandle Scheme;
    }

    /**
     * This class is not ideal since it doesn't accound for multiple players but since the game
     * is assumed to be single player this class notifies when the control scheme changes for 
     * any existing user in favor of simplicity
     **/
    public class ControlSchemeDetector : MonoBehaviour
    {
        [Tooltip("The first scheme will be used as default")]
        [SerializeField] InputSchemeHandle[] m_Schemes;

        private InputSchemeChangedMessage m_SchemeChangedMsg = new InputSchemeChangedMessage();

        public static InputSchemeHandle CurrentControlScheme { get; private set; }

        // --------------------------------------------------------------------

        void Awake()
        {
            InputUser.onChange += InputUserOnChange;

            if (CurrentControlScheme == null)
            {
                if (InputUser.all.Count > 0)
                    UpdateControlScheme(InputUser.all[0].controlScheme.Value.name);
                else
                    UpdateControlScheme(string.Empty);
            }
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            InputUser.onChange -= InputUserOnChange;
        }

        // --------------------------------------------------------------------

        private void InputUserOnChange(InputUser user, InputUserChange change, InputDevice device)
        {
            if (change == InputUserChange.ControlSchemeChanged && user.controlScheme.HasValue)
            {
                UpdateControlScheme(user.controlScheme.Value.name);

                m_SchemeChangedMsg.Scheme = CurrentControlScheme;
                MessageBuffer<InputSchemeChangedMessage>.Dispatch(m_SchemeChangedMsg);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateControlScheme(string schemeName)
        {
            foreach (var scheme in m_Schemes)
            {
                if (scheme.SchemeName == schemeName)
                {
                    CurrentControlScheme = scheme;
                    return;
                }
            }

            // By default, switch to the first scheme if possible
            if (m_Schemes.Length > 0)
            {
                if (!string.IsNullOrEmpty(schemeName))
                    Debug.LogWarning($"ControlSchemeDetector: Scheme not found {schemeName}, using default scheme as control scheme", this);

                CurrentControlScheme = m_Schemes[0];
            }
        }
    }
}