#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.UI
{
    public class InspectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup   = null!;
        [SerializeField] private Image       itemIcon      = null!;
        [SerializeField] private TMP_Text    itemName      = null!;
        [SerializeField] private TMP_Text    itemDescription = null!;
        [SerializeField] private PickupPreviewView? modelPreview;

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
            OpenInternal(item.Data);
            item.SetInspected(true);
        }

        // For items with no InventoryItemView -- e.g. a permanently-equipped melee weapon,
        // which never enters the spatial inventory grid.
        public void Open(ItemData data)
        {
            this.currentItem = null;
            OpenInternal(data);
        }

        void OpenInternal(ItemData data)
        {
            if (data.PreviewModel != null && this.modelPreview != null)
            {
                this.itemIcon.enabled = false;
                this.modelPreview.Show(data);
            }
            else
            {
                this.modelPreview?.Hide();
                this.itemIcon.sprite  = data.Icon;
                this.itemIcon.enabled = data.Icon != null;
            }

            this.itemName.text        = data.DisplayName;
            this.itemDescription.text = data.ExamineDialogue.nodeName;

            IsOpen = true;
            Show();
            this.sfx?.PlayDecide(gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            this.modelPreview?.Hide();
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
