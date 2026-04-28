using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class UIPause : MonoBehaviour
    {
        [SerializeField] private GameObject m_MainContent;
        [SerializeField] private GameObject m_PauseHint;
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private GameObject m_DefaultSelection;

        public UnityEvent OnShow;
        public UnityEvent OnHide;

        private IUIInput m_Input;
        private UIContext m_Context;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Context = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();
        }

        // --------------------------------------------------------------------

        void Start()
        {
            m_MainContent.SetActive(false);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            bool isPlaying = GameManager.Instance.IsPlaying;

            if (m_PauseHint)
                m_PauseHint.SetActive(isPlaying);

            if (m_DefaultSelection.activeInHierarchy)
                EventSystemUtils.SelectDefaultOnLostFocus(m_DefaultSelection);
        }

        // --------------------------------------------------------------------

        public void Show()
        {
            PauseController.Instance.Pause(this);
            m_MainContent.SetActive(true);
            UIManager.Get<UIAudio>().Play(m_ShowClip);
            CursorController.Instance.SetInUI(true);
            m_Context.Activate();

            OnShow?.Invoke();
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);
            m_MainContent.SetActive(false);
            m_Context.Deactivate();
            OnHide?.Invoke();
        }

        // --------------------------------------------------------------------

        public void QuitGame()
        {
            Hide();
            GameManager.Instance.QuitGame();
        }

        public bool CanBeShown()
        {
            return !m_Context.IsBlocked();
        }
    }
}