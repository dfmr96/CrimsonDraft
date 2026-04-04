using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace HorrorEngine
{
    public class InputGlyphReplacer : TextReplacerBase
    {
        [SerializeField] private string m_Tag;
        [SerializeField] private InputActionAsset m_Actions;
        [SerializeField] private string m_ActionName;
        [SerializeField] private bool m_ShowDebug;

        private string m_BindingPath;
        private MessageBuffer<InputSchemeChangedMessage>.MessageCallback m_OnInputSchemeChangedCallback;

        private string m_SchemeName;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnInputSchemeChangedCallback = OnInputSchemeChanged;
            MessageBuffer<InputSchemeChangedMessage>.Subscribe(m_OnInputSchemeChangedCallback);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<InputSchemeChangedMessage>.Unsubscribe(m_OnInputSchemeChangedCallback);
        }

        // --------------------------------------------------------------------

        private void OnInputSchemeChanged(InputSchemeChangedMessage msg)
        {
            UpdateBindingPath(msg.Scheme);
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            Debug.Assert(ControlSchemeDetector.CurrentControlScheme, "Couldn't update sprite because current scheme is null", this);
            if (ControlSchemeDetector.CurrentControlScheme != null)
                UpdateBindingPath(ControlSchemeDetector.CurrentControlScheme);
        }

        // --------------------------------------------------------------------

        protected void UpdateBindingPath(InputSchemeHandle scheme)
        {
            m_SchemeName = scheme.SchemeName;


            InputAction action = m_Actions.FindAction(m_ActionName);
            if (action != null)
            {
                foreach (InputBinding binding in action.bindings)
                {
                    if (binding.groups.Contains(scheme.SchemeName))
                    {
                        m_BindingPath = binding.path;
                        m_BindingPath = m_BindingPath.Replace("<", "");
                        m_BindingPath = m_BindingPath.Replace(">", "");
                        m_BindingPath = m_BindingPath.Replace("/", "_");
                        return;
                    }
                }
            }

            m_BindingPath = "NotFound";
        }

        // --------------------------------------------------------------------

        public override string Replace(string text)
        {
            string replacement = $"<sprite=\"{m_SchemeName}\" name=\"{m_BindingPath}\">";
            if (m_ShowDebug)
                Debug.Log(replacement);
            return text.Replace(m_Tag, replacement);
        }
    }
}