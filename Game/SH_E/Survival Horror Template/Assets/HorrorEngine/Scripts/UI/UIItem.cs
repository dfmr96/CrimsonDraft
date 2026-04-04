using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class UIItem : MonoBehaviour
    {
        [SerializeField] private GameObject m_InfoPanel;   
        [SerializeField] private UIItemFiller m_ItemFiller;

        [Header("Examination")]
        [SerializeField] private bool OpenExamination = true;
        [SerializeField] private bool InteractDuringExamination = true;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CloseClip;

        private IUIInput m_Input;
        

        // --------------------------------------------------------------------

        void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
            MessageBuffer<ItemPickedUpMessage>.Subscribe(OnItemPickedUp);
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<ItemPickedUpMessage>.Unsubscribe(OnItemPickedUp);
        }

        // --------------------------------------------------------------------

        void OnItemPickedUp(ItemPickedUpMessage msg)
        {
            if (!msg.ShowItemSplash)
                return;

            var itemData = msg.Data;
            float amount = msg.Count;

            if (gameObject.activeInHierarchy)
            {
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = () => { Show(itemData, amount); },
                    StopProcessingActions = true,
                    Name = "UIItem.OnItemPickeUp (Show)"
                });
            }
            else
            {
                Show(itemData, amount);
            }
        }

        // --------------------------------------------------------------------

        public void Show(ItemData item, float amount=1f)
        {
            PauseController.Instance.Pause(this);

            m_InfoPanel.SetActive(!OpenExamination);

            gameObject.SetActive(true);

            if (OpenExamination)
            {
                m_ItemFiller.Clear();
                this.InvokeActionNextFrame(() => // This is needed in case we come from UIExamineItem itself
                {
                    UIManager.Get<UIExamineItem>().Show(item, InteractDuringExamination, false);
                });
            }
            else
            {
                m_ItemFiller.Fill(item, 1f, 1f);
            }

            UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            bool isDismissed = m_Input.IsConfirmDown() || m_Input.IsCancelDown();
            if (isDismissed)
            {
                Hide();
            }
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            if (OpenExamination)
            {
                UIManager.Get<UIExamineItem>().Hide();
            }

            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            if (!UIManager.PopAction())
                UIManager.Get<UIAudio>().Play(m_CloseClip);
        }
    }
}