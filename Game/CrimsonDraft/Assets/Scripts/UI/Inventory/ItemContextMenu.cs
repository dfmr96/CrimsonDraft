#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup  canvasGroup  = null!;
        [SerializeField] private MenuOption[] options      = null!; // 0=Use, 1=Inspect, 2=Combine
        [SerializeField] private InspectPanel inspectPanel = null!;

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

        public void Open(InventoryItemView item, ContextMenuOptions options)
        {
            this.currentItem = item;
            this.isOpen      = true;

            this.options[0].SetDisabled(!options.CanEquip && !options.CanUse);
            this.options[1].SetDisabled(!options.CanInspect);
            this.options[2].SetDisabled(!options.CanCombine);

            if (options.CanEquip)
                this.options[0].SetLabel(item.BoundItem.IsEquipped ? "Unequip" : "Equip");
            else
                this.options[0].SetLabel("Use");

            // Start on first selectable option
            this.selectedIndex = 0;
            for (int i = 0; i < this.options.Length; i++)
            {
                if (!this.options[i].IsDisabled) { this.selectedIndex = i; break; }
            }

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
            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            Camera cam = (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                ? rootCanvas.worldCamera : null;

            var itemRT = item.GetComponent<RectTransform>();
            Vector3[] wc = new Vector3[4];
            itemRT.GetWorldCorners(wc);

            // GetWorldCorners orders corners by local orientation (BL/TL/TR/BR).
            // When the item is rotated the local TL/TR are no longer the visual top edge.
            // Convert to screen space and pick the two corners with the highest Y —
            // those are always the visual top edge regardless of rotation.
            Vector2[] sc = new Vector2[4];
            for (int i = 0; i < 4; i++)
                sc[i] = RectTransformUtility.WorldToScreenPoint(cam, wc[i]);

            int t0 = 0;
            for (int i = 1; i < 4; i++)
                if (sc[i].y > sc[t0].y) t0 = i;

            int t1 = t0 == 0 ? 1 : 0;
            for (int i = 0; i < 4; i++)
                if (i != t0 && sc[i].y > sc[t1].y) t1 = i;

            int idxL = sc[t0].x < sc[t1].x ? t0 : t1;
            int idxR = sc[t0].x < sc[t1].x ? t1 : t0;

            // Default: menu to the right (pivot TL anchored to visual top-right)
            this.rectTransform.pivot    = new Vector2(0f, 1f);
            this.rectTransform.position = wc[idxR];

            Vector3[] menuCorners = new Vector3[4];
            this.rectTransform.GetWorldCorners(menuCorners);

            if (RectTransformUtility.WorldToScreenPoint(cam, menuCorners[2]).x > Screen.width)
            {
                this.rectTransform.pivot    = new Vector2(1f, 1f);
                this.rectTransform.position = wc[idxL];
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
