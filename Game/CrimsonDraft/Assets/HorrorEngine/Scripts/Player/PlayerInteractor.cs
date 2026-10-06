using UnityEngine;

namespace HorrorEngine
{
    public interface IInteractor { }

    public class PlayerInteractor : MonoBehaviour, IInteractor, IDeactivateWithActor
    {
        [SerializeField] InteractionColliderDetector m_Detector;

        private IPlayerInput m_Input;

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
    }
}