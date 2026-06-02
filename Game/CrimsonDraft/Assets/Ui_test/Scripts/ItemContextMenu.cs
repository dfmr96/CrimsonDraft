#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup  canvasGroup  = null!;
        [SerializeField] private MenuOption[] options      = null!; // 0=Use, 1=Inspect, 2=Combine
        [SerializeField] private InspectPanel inspectPanel = null!;

        private int              selectedIndex = 0;
        private bool             isOpen        = false;
        private InventoryItemView? currentItem;
        private RectTransform    rectTransform = null!;

        public bool IsOpen => this.isOpen;
        public System.Action? OnClose;

        void Awake()
        {
            this.rectTransform = GetComponent<RectTransform>();

            if (this.canvasGroup == null)
                this.canvasGroup = GetComponent<CanvasGroup>();

            this.isOpen = false;
            Hide();
        }

        public void Open(InventoryItemView item)
        {
            this.currentItem   = item;
            this.selectedIndex = 0;
            this.isOpen        = true;

            this.options[2].SetDisabled(!item.Data.Combinable);

            PositionNextToItem(item);
            RefreshVisuals();
            Show();
        }

        public void Close()
        {
            this.isOpen = false;
            Hide();
            OnClose?.Invoke();
        }

        public void NavigateMenu(int dir)
        {
            int count = this.options.Length;
            int next  = this.selectedIndex;

            for (int i = 1; i <= count; i++)
            {
                next = (this.selectedIndex + dir * i + count) % count;
                if (!this.options[next].IsDisabled) break;
            }

            this.selectedIndex = next;
            RefreshVisuals();
        }

        public void ConfirmSelection()
        {
            if (!this.isOpen) return;
            ExecuteOption(this.options[this.selectedIndex].Type);
        }

        void ExecuteOption(MenuOption.OptionType type)
        {
            Close();

            switch (type)
            {
                case MenuOption.OptionType.Use:
                    Debug.Log($"[Menu] Use: {this.currentItem?.Data.DisplayName}");
                    break;
                case MenuOption.OptionType.Inspect:
                    if (this.inspectPanel != null && this.currentItem != null)
                        this.inspectPanel.Open(this.currentItem);
                    else
                        Debug.LogWarning("[Menu] InspectPanel not assigned.");
                    break;
                case MenuOption.OptionType.Combine:
                    Debug.Log($"[Menu] Combine: {this.currentItem?.Data.DisplayName}");
                    break;
            }
        }

        void PositionNextToItem(InventoryItemView item)
        {
            var itemRT = item.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            itemRT.GetWorldCorners(corners);
            // corners: 0=BL  1=TL  2=TR  3=BR

            this.rectTransform.pivot    = new Vector2(0f, 1f);
            this.rectTransform.position = corners[2];

            Vector3[] menuCorners = new Vector3[4];
            this.rectTransform.GetWorldCorners(menuCorners);

            if (menuCorners[2].x > Screen.width)
            {
                this.rectTransform.pivot    = new Vector2(1f, 1f);
                this.rectTransform.position = corners[1];
            }
        }

        void RefreshVisuals()
        {
            for (int i = 0; i < this.options.Length; i++)
                this.options[i].SetState(i == this.selectedIndex && !this.options[i].IsDisabled);
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
