using UnityEngine;

namespace HorrorEngine
{
    public interface IInteractor 
    {
    }

    public class PlayerInteractor : MonoBehaviour, IInteractor, IDeactivateWithActor
    {
        [SerializeField] InteractionColliderDetector m_Detector;

        private IPlayerInput m_Input;
        private Interactive m_LastFocusedInteractive;

        public bool IsInteracting { get; private set; }

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IPlayerInput>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            IsInteracting = CheckIsInteracting();
                m_Detector.enabled = true;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            m_Detector.enabled = false;
        }

        // --------------------------------------------------------------------

        void Update()
        {
            IsInteracting = CheckIsInteracting();
        }

        // --------------------------------------------------------------------

        private bool CheckIsInteracting()
        {
            if (!PauseController.Instance.IsPaused && m_Detector.FocusedInteractive && m_Input.IsInteractingDown())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // --------------------------------------------------------------------

        public void CacheInteractive()
        {
            m_LastFocusedInteractive = m_Detector.FocusedInteractive;
        }

        // --------------------------------------------------------------------

        public Interactive GetInteractive()
        {
            if (!m_Detector.enabled)
                return m_LastFocusedInteractive;
            else
                return m_Detector.FocusedInteractive;
        }
    }
}