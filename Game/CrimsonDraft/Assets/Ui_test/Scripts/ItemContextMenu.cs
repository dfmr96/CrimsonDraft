#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup  canvasGroup  = null!;
        [SerializeField] private MenuOption[] options      = null!; // 0=Use, 1=Inspect, 2=Combine
        [SerializeField] private InspectPanel inspectPanel = null!;

        [Inject] private ICombineService? combineService;

        private int               selectedIndex = 0;
        private bool              isOpen        = false;
        private InventoryItemView? currentItem;
        private RectTransform     rectTransform = null!;

        public bool IsOpen => this.isOpen;
        public System.Action? OnClose;
        public System.Action<InventoryItemView>? OnUseRequested;
        public System.Action<InventoryItemView>? OnCombineRequested;

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

            bool canCombine = this.combineService != null
                ? this.combineService.HasAnyRecipe(item.Data)
                : item.Data.Combinable;
            this.options[2].SetDisabled(!canCombine);

            if (item.Data.ItemType == CrimsonDraft.Inventory.ItemType.Weapon)
                this.options[0].SetLabel(item.BoundItem.IsEquipped ? "Unequip" : "Equip");
            else
                this.options[0].SetLabel("Use");

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
            var item = this.currentItem;
            Close();

            switch (type)
            {
                case MenuOption.OptionType.Use:
                    if (item != null) OnUseRequested?.Invoke(item);
                    break;
                case MenuOption.OptionType.Inspect:
                    if (this.inspectPanel != null && item != null)
                        this.inspectPanel.Open(item);
                    else
                        Debug.LogWarning("[Menu] InspectPanel not assigned.");
                    break;
                case MenuOption.OptionType.Combine:
                    if (item != null) OnCombineRequested?.Invoke(item);
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
