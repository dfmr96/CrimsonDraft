using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace CrimsonDraft.UI
{
    public class InspectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image       itemIcon;
        [SerializeField] private TMP_Text    itemName;
        [SerializeField] private TMP_Text    itemDescription;

        private InputAction closeAction;
        private InventoryItem currentItem;

        public bool IsOpen { get; private set; }
        public System.Action OnClose;

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            closeAction = new InputAction("InspectClose", InputActionType.Button);
            closeAction.AddBinding("<Keyboard>/v");
            closeAction.AddBinding("<Gamepad>/buttonEast");   // B / Circle
            closeAction.AddBinding("<Gamepad>/buttonWest");   // Square
            closeAction.AddBinding("<Keyboard>/c");
            closeAction.performed += _ => Close();

            IsOpen = false;
            Hide();
        }

        void OnDestroy()
        {
            closeAction.Dispose();
        }

        public void Open(InventoryItem item)
        {
            currentItem = item;

            itemIcon.sprite        = item.Data.icon;
            itemIcon.enabled       = item.Data.icon != null;
            itemName.text          = item.Data.primaryName;
            itemDescription.text   = item.Data.description;

            // Mark item as inspected
            item.SetInspected(true);

            IsOpen = true;
            Show();
            closeAction.Enable();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            closeAction.Disable();
            OnClose?.Invoke();
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
            closeAction.Disable();
        }
    }
}
