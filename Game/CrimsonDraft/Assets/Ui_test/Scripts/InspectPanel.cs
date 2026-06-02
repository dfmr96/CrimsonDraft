#nullable enable

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    public class InspectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup   = null!;
        [SerializeField] private Image       itemIcon      = null!;
        [SerializeField] private TMP_Text    itemName      = null!;
        [SerializeField] private TMP_Text    itemDescription = null!;

        [Header("Audio")]
        [SerializeField] private InventorySoundManager sfx = null!;

        [Header("Standalone (sin VContainer)")]
        [SerializeField] private InputActionAsset? standaloneInputAsset;

        [Inject] private IInputService? inputService;

        private InputAction?    cancelFallback;
        private InputActionMap? standaloneMap;

        private InventoryItemView? currentItem;

        public bool IsOpen { get; private set; }
        public System.Action? OnClose;

        void Awake()
        {
            if (this.canvasGroup == null)
                this.canvasGroup = GetComponent<CanvasGroup>();

            if (this.standaloneInputAsset != null)
            {
                this.standaloneMap   = this.standaloneInputAsset.FindActionMap("Inventory");
                this.cancelFallback  = this.standaloneMap?["Cancel"];
            }

            IsOpen = false;
            Hide();
        }

        void OnEnable()
        {
            if (this.inputService != null)
                this.inputService.InventoryCancel.performed += OnCancelPressed;
            else if (this.cancelFallback != null)
                this.cancelFallback.performed += OnCancelPressed;
        }

        void OnDisable()
        {
            if (this.inputService != null)
                this.inputService.InventoryCancel.performed -= OnCancelPressed;
            else if (this.cancelFallback != null)
                this.cancelFallback.performed -= OnCancelPressed;
        }

        private void OnCancelPressed(InputAction.CallbackContext ctx) => Close();

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
            this.sfx?.PlayInspectOpen();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            this.sfx?.PlayInspectClose();
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
