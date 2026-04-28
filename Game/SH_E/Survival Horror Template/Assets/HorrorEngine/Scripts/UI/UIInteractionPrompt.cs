using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIInteractionPrompt : MonoBehaviour
    {
        [SerializeField] Transform m_Prompt;
        [SerializeField] TMPro.TextMeshProUGUI m_Text;
        [SerializeField] Image m_Icon;
        [SerializeField] bool FillInteractionName;
        [SerializeField] bool FillInteractionIcon;

        private Interactive m_Interactive;
        private Action<OnDisableNotifier> m_OnInteractorDisabled;

        private bool ShouldShow => m_Interactive != null && m_Interactive.ShowPrompt && GameManager.Instance.IsPlaying;

        // --------------------------------------------------------------------

        private void Awake()
        {
            MessageBuffer<InteractionChangedMessage>.Subscribe(OnInteractorChanged);
            MessageBuffer<InteractionStartMessage>.Subscribe(OnInteractionStart);
            MessageBuffer<InteractionEndMessage>.Subscribe(OnInteractionEnd);
            MessageBuffer<GameUnpausedMessage>.Subscribe(OnGameUnpaused);
            MessageBuffer<GamePausedMessage>.Subscribe(OnGamePaused);

            m_OnInteractorDisabled = OnInteractorDisabled;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_Prompt.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<InteractionChangedMessage>.Unsubscribe(OnInteractorChanged);
            MessageBuffer<InteractionStartMessage>.Unsubscribe(OnInteractionStart);
            MessageBuffer<InteractionEndMessage>.Unsubscribe(OnInteractionEnd);
            MessageBuffer<GameUnpausedMessage>.Unsubscribe(OnGameUnpaused);
            MessageBuffer<GamePausedMessage>.Unsubscribe(OnGamePaused);
        }

        // --------------------------------------------------------------------

        private void OnGamePaused(GamePausedMessage msg)
        {
            m_Prompt.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnGameUnpaused(GameUnpausedMessage msg)
        {
            m_Prompt.gameObject.SetActive(ShouldShow);
        }

        // --------------------------------------------------------------------

        private void OnInteractionStart(InteractionStartMessage msg)
        {
            m_Prompt.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnInteractionEnd(InteractionEndMessage msg)
        {
            m_Prompt.gameObject.SetActive(ShouldShow);
        }

        // --------------------------------------------------------------------

        private void OnInteractorChanged(InteractionChangedMessage msg)
        {
            if (m_Interactive)
            {
                var disableNotif = m_Interactive.GetComponentInChildren<OnDisableNotifier>();
                disableNotif.RemoveCallback(m_OnInteractorDisabled);
            }
            
            m_Interactive = msg.Interactive;

            if (m_Interactive && m_Interactive.ShowPrompt)
            {
                if (m_Interactive.Data)
                {
                    if (FillInteractionName)
                        m_Text.text = m_Interactive.Data.Prompt;
                    if (FillInteractionIcon)
                        m_Icon.sprite = m_Interactive.Data.Icon;
                }

                if (m_Prompt.TryGetComponent(out UIWorldAttached worldAttach))
                {
                    worldAttach.Attach(m_Interactive.transform);
                }

                var disableNotif = m_Interactive.GetComponentInChildren<OnDisableNotifier>();
                disableNotif.AddCallback(m_OnInteractorDisabled);
            }

            m_Prompt.gameObject.SetActive(ShouldShow);
        }

        // --------------------------------------------------------------------

        private void OnInteractorDisabled(OnDisableNotifier notifier)
        {
            if (notifier.TryGetComponent(out Interactive interactive)) 
            {
                if (interactive == m_Interactive)
                {
                    notifier.RemoveCallback(m_OnInteractorDisabled);
                    m_Interactive = null;
                    m_Prompt.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("OnDisableNotifier was called but Interactor was not found. Please make sure it is on the gameObject with the Interactive component");
            }
        }
    }

}