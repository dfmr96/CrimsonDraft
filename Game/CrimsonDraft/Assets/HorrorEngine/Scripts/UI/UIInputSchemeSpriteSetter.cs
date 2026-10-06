using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public abstract class UIInputSchemeSpriteSetter : MonoBehaviour
    {
        public UnityEvent<Sprite> SetSpriteEvent;
        public UnityEvent NotFoundEvent;
        private MessageBuffer<InputSchemeChangedMessage>.MessageCallback m_OnInputSchemeChangedCallback;

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            m_OnInputSchemeChangedCallback = OnInputSchemeChanged;
            MessageBuffer<InputSchemeChangedMessage>.Subscribe(m_OnInputSchemeChangedCallback);
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            IUIInput uiInput = UIManager.Get<UIInputNew>();

            Debug.Assert(ControlSchemeDetector.CurrentControlScheme, "Couldn't update sprite because current scheme is null", this);
            if (ControlSchemeDetector.CurrentControlScheme != null)
                UpdateSprite(ControlSchemeDetector.CurrentControlScheme);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<InputSchemeChangedMessage>.Unsubscribe(m_OnInputSchemeChangedCallback);
        }

        // --------------------------------------------------------------------

        private void OnInputSchemeChanged(InputSchemeChangedMessage msg)
        {
            UpdateSprite(msg.Scheme);
        }

        // --------------------------------------------------------------------

        protected abstract void UpdateSprite(InputSchemeHandle scheme);
    }
}