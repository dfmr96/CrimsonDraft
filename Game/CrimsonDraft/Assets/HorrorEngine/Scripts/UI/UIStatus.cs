using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIStatus : MonoBehaviour
    {
        [SerializeField] private UIStatusLine m_Line;
        [SerializeField] private Image m_StatusBg;
        [SerializeField] private Image m_StatusPortrait;
        [SerializeField] private TMPro.TextMeshProUGUI m_StatusText;

        protected Status m_Status;
        protected Health m_Health;

        protected Actor m_BoundToActor;

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            if (m_Health)
                UpdateStatus();
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            Clear();
        }

        // --------------------------------------------------------------------

        private void Clear()
        {
            m_BoundToActor = null;

            if (m_Health)
                m_Health.OnHealthAltered.RemoveListener(OnHealthAltered);

            if (m_Status)
                m_Status.OnStatusChanged.RemoveListener(OnStatusChanged);
        }

        // --------------------------------------------------------------------

        public void BindToActor(Actor actor)
        {
            if (m_BoundToActor)
            {
                Clear();
            }

            if (actor) 
            {
                m_BoundToActor = actor;

                m_Health = actor.GetComponent<Health>();
                m_Status = actor.GetComponent<Status>();

                if (m_Health)
                    m_Health.OnHealthAltered.AddListener(OnHealthAltered);

                if (m_Status)
                    m_Status.OnStatusChanged.AddListener(OnStatusChanged);

                if (m_StatusPortrait)
                {
                    PlayerActor player = m_BoundToActor as PlayerActor;
                    if (player && player.Character)
                    {
                        m_StatusPortrait.sprite = player.Character.Portrait;
                    }
                }

                UpdateStatus();
            }
        }

        // --------------------------------------------------------------------

        private void OnHealthAltered(float prev, float current)
        {
            UpdateStatus();
        }

        // --------------------------------------------------------------------

        private void OnStatusChanged(StatusData newStatus)
        {
            UpdateStatus();
        }

        // --------------------------------------------------------------------

        public void UpdateStatus()
        {
            if (m_Status == null)
                return;

            var currentStatus = m_Status.GetCurrentStatus();

            if (currentStatus != null)
            {
                m_StatusBg.color = currentStatus.Color;
                m_StatusText.text = currentStatus.Text;
                m_Line.SetStatus(currentStatus);
            }
        }
    }
}