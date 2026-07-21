#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

namespace CrimsonDraft.UI
{
    public class InspectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup   = null!;
        [SerializeField] private Image       itemIcon      = null!;
        [SerializeField] private TMP_Text    itemName      = null!;
        [SerializeField] private TMP_Text    itemDescription = null!;

        [Inject] private InventorySfxData sfx = null!;

        private InventoryItemView? currentItem;

        public bool IsOpen { get; private set; }
        public System.Action? OnClose;

        void Awake()
        {
            if (this.canvasGroup == null)
                this.canvasGroup = GetComponent<CanvasGroup>();

            IsOpen = false;
            Hide();
        }


        public void Open(InventoryItemView item)
        {
            this.currentItem = item;

            this.itemIcon.sprite      = item.Data.Icon;
            this.itemIcon.enabled     = item.Data.Icon != null;
            this.itemName.text        = item.Data.DisplayName;
            this.itemDescription.text = item.Data.ExamineDialogue.nodeName;

            item.SetInspected(true);

            IsOpen = true;
            Show();
            this.sfx?.PlayDecide(gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            this.sfx?.PlayCancel(gameObject);
            OnClose?.Invoke();
        }

        void Show()
        {
            this.canvasGroup.alpha          = 1f;
            this.canvasGroup.interactable   = true;
            this.canvasGroup.blocksRaycasts = true;
        }

        void Hide()
        {
            this.canvasGroup.alpha          = 0f;
            this.canvasGroup.interactable   = false;
            this.canvasGroup.blocksRaycasts = false;
        }
    }
}
