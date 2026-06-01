using UnityEngine;

namespace CrimsonDraft.UI
{
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup  canvasGroup;
        [SerializeField] private MenuOption[] options;   // 0=Use, 1=Inspect, 2=Combine
        [SerializeField] private InspectPanel inspectPanel;

        private int selectedIndex = 0;
        private bool isOpen = false;
        private InventoryItem currentItem;
        private RectTransform rectTransform;

        public bool IsOpen => isOpen;
        public System.Action OnClose;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            isOpen = false;
            Hide();
        }

        public void Open(InventoryItem item)
        {
            currentItem   = item;
            selectedIndex = 0;
            isOpen        = true;

            options[2].SetDisabled(!item.Data.combinable);

            PositionNextToItem(item);
            RefreshVisuals();
            Show();
        }

        public void Close()
        {
            isOpen = false;
            Hide();
            OnClose?.Invoke();
        }

        // Called by GridCursor when menu is open
        public void NavigateMenu(int dir)
        {
            int count = options.Length;
            int next  = selectedIndex;

            for (int i = 1; i <= count; i++)
            {
                next = (selectedIndex + dir * i + count) % count;
                if (!options[next].IsDisabled) break;
            }

            selectedIndex = next;
            RefreshVisuals();
        }

        public void ConfirmSelection()
        {
            if (!isOpen) return;
            ExecuteOption(options[selectedIndex].Type);
        }

        void ExecuteOption(MenuOption.OptionType type)
        {
            Close();

            switch (type)
            {
                case MenuOption.OptionType.Use:
                    Debug.Log($"[Menu] Use: {currentItem.Data.primaryName}");
                    break;
                case MenuOption.OptionType.Inspect:
                    if (inspectPanel != null)
                        inspectPanel.Open(currentItem);
                    else
                        Debug.LogWarning("[Menu] InspectPanel not assigned.");
                    break;
                case MenuOption.OptionType.Combine:
                    Debug.Log($"[Menu] Combine: {currentItem.Data.primaryName}");
                    break;
            }
        }

        void PositionNextToItem(InventoryItem item)
        {
            var itemRT = item.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            itemRT.GetWorldCorners(corners);
            // corners: 0=BL  1=TL  2=TR  3=BR

            // Try right side: pivot top-left (0,1)
            rectTransform.pivot    = new Vector2(0f, 1f);
            rectTransform.position = corners[2];

            // Check if right edge goes off screen — flip to left if so
            Vector3[] menuCorners = new Vector3[4];
            rectTransform.GetWorldCorners(menuCorners);

            if (menuCorners[2].x > Screen.width)
            {
                // Flip to left side: pivot top-right (1,1)
                rectTransform.pivot    = new Vector2(1f, 1f);
                rectTransform.position = corners[1];
            }
        }

        void RefreshVisuals()
        {
            for (int i = 0; i < options.Length; i++)
                options[i].SetState(i == selectedIndex && !options[i].IsDisabled);
        }

        void Show()
        {
            canvasGroup.alpha          = 1f;
            canvasGroup.interactable   = true;
            canvasGroup.blocksRaycasts = true;
        }

        void Hide()
        {
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
