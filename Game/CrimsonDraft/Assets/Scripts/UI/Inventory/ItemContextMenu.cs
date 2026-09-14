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

        private static readonly int[] DefaultSelectionPriority = { 0, 2, 1 };

        private int               selectedIndex = 0;
        private bool              isOpen        = false;
        private bool              currentCanSplit;
        private InventoryItemView? currentItem;
        private MeleeWeaponData?   currentMeleeItem;
        private RectTransform     rectTransform = null!;

        public bool IsOpen => this.isOpen;
        public System.Action? OnClose;
        public System.Action<InventoryItemView>? OnUseRequested;
        public System.Action<InventoryItemView>? OnCombineRequested;
        public System.Action<InventoryItemView>? OnSplitRequested;

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
            this.currentItem     = item;
            this.currentMeleeItem = null;
            this.isOpen          = true;
            this.currentCanSplit = options.CanSplit;

            this.options[0].SetDisabled(!options.CanEquip && !options.CanUse && !options.CanSplit);
            this.options[1].SetDisabled(!options.CanInspect);
            this.options[2].SetDisabled(!options.CanCombine);

            if (options.CanEquip)
                this.options[0].SetLabel(item.BoundItem.IsEquipped ? "Unequip" : "Equip");
            else if (options.CanSplit)
                this.options[0].SetLabel("Split");
            else
                this.options[0].SetLabel("Use");

            // Start on the first selectable action option, preferring Use/Equip/Split (0)
            // then Combine (2) over Inspect (1) -- Inspect is available on virtually every
            // item, so left in raw index order it would always steal default focus away
            // from the action the player actually opened the menu for (e.g. Combine on an
            // ammo box) the moment CanInspect is true.
            this.selectedIndex = 0;
            foreach (int i in DefaultSelectionPriority)
            {
                if (!this.options[i].IsDisabled) { this.selectedIndex = i; break; }
            }

            PositionNextToItem(item);
            RefreshVisuals();
            Show();
        }

        // For items with no InventoryItemView -- e.g. a permanently-equipped melee weapon,
        // which never enters the spatial inventory grid. Only Inspect is ever meaningful here
        // (nothing to Use/Equip/Split or Combine), so the other two stay disabled/hidden.
        public void OpenForMeleeInspectOnly(RectTransform anchor, MeleeWeaponData meleeData)
        {
            this.currentItem      = null;
            this.currentMeleeItem = meleeData;
            this.isOpen           = true;
            this.currentCanSplit  = false;

            this.options[0].SetDisabled(true);
            this.options[1].SetDisabled(false);
            this.options[2].SetDisabled(true);
            this.options[0].SetLabel("Use");

            this.selectedIndex = 1; // Inspect is the only enabled option

            PositionNextToRect(anchor);
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

            // dir comes from Vector2Int.up/down (y = +1 / -1), but options are laid out
            // top-to-bottom at increasing array index -- so "down" (-1) must INCREASE the
            // index. Without the negation this stepped backwards (e.g. Use -> Combine ->
            // Inspect instead of Use -> Inspect -> Combine), which only looked "roughly
            // right" when the skipped-disabled-option logic happened to mask it.
            for (int i = 1; i <= count; i++)
            {
                next = (this.selectedIndex - dir * i + count) % count;
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
            var item      = this.currentItem;
            var meleeData = this.currentMeleeItem;
            Close();

            switch (type)
            {
                case MenuOption.OptionType.Use:
                    if (item == null) break;
                    if (this.currentCanSplit) OnSplitRequested?.Invoke(item);
                    else                       OnUseRequested?.Invoke(item);
                    break;
                case MenuOption.OptionType.Inspect:
                    if (this.inspectPanel == null)
                    {
                        Debug.LogWarning("[Menu] InspectPanel not assigned.");
                        break;
                    }
                    if (item != null)           this.inspectPanel.Open(item);
                    else if (meleeData != null) this.inspectPanel.Open(meleeData);
                    break;
                case MenuOption.OptionType.Combine:
                    if (item != null) OnCombineRequested?.Invoke(item);
                    break;
            }
        }

        void PositionNextToItem(InventoryItemView item) =>
            PositionNextToRect(item.GetComponent<RectTransform>());

        void PositionNextToRect(RectTransform itemRT)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            Camera cam = (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                ? rootCanvas.worldCamera : null;

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
