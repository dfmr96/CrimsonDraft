using UnityEngine;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class UIInputListener : MonoBehaviour
    {
        private IUIInput m_Input;

        private UIMap m_Map;
        private UIPause m_Pause;

        private HashSet<Object> m_BlockingContext = new HashSet<Object>();

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponent<IUIInput>();
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            // TODO - This can be redone a bit better now that we support UIContexts

            if (GameManager.Instance.IsPlaying)
            {
                if (m_Input.IsToggleInventoryDown())
                {
                    MessageBuffer<ShowInventoryMessage>.Dispatch();
                }

                if (m_Input.IsToggleMapDown())
                {
                    if (!m_Map)
                        m_Map = GetComponentInChildren<UIMap>(true);

                    if (m_Map.CanBeShown())
                        m_Map?.Show();
                }

                if (m_Input.IsTogglePauseDown())
                {
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (m_Pause.CanBeShown())
                        m_Pause?.Show();
                }
            }
            else
            {
                if (m_Input.IsTogglePauseDown())
                {
                     
                    if (!m_Pause)
                        m_Pause = GetComponentInChildren<UIPause>(true);

                    if (PauseController.Instance.Context == m_Pause)
                        m_Pause?.Hide();
                }
            }
        }

        // --------------------------------------------------------------------

        public void AddBlockingContext(Object context)
        {
            m_BlockingContext.Add(context);

            enabled = false;
        }

        // --------------------------------------------------------------------

        public void RemoveBlockingContext(Object context)
        {
            m_BlockingContext.Remove(context);

            if (m_BlockingContext.Count == 0)
            {
                enabled = true;
            }
        }

        // --------------------------------------------------------------------

        internal bool IsBlocked()
        {
            return !enabled;
        }
    }
}