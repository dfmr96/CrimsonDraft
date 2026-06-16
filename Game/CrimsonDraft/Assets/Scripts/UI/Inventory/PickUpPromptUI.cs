#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class PickUpPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject root        = null!;
        [SerializeField] private TMP_Text   itemNameText = null!;
        [SerializeField] private Image      itemIcon     = null!;
        [SerializeField] private Button     pickUpButton = null!;
        [SerializeField] private Button     cancelButton = null!;
        [SerializeField] private GameObject fullMessage  = null!;

        private ItemData?     pendingItem;
        private IItemSpawner? pendingSpawner;
        private Action?       onPickedUp;

        void Awake()
        {
            pickUpButton.onClick.AddListener(OnPickUp);
            cancelButton.onClick.AddListener(Hide);
            root.SetActive(false);
        }

        public void Show(ItemData item, IItemSpawner spawner, Action? onPickedUp = null)
        {
            this.pendingItem    = item;
            this.pendingSpawner = spawner;
            this.onPickedUp     = onPickedUp;

            itemNameText.text = item.DisplayName;
            if (item.Icon != null)
            {
                itemIcon.sprite  = item.Icon;
                itemIcon.enabled = true;
            }
            else
            {
                itemIcon.enabled = false;
            }

            fullMessage.SetActive(false);
            root.SetActive(true);
        }

        public void Hide()
        {
            root.SetActive(false);
            pendingItem    = null;
            pendingSpawner = null;
            onPickedUp     = null;
        }

        private void OnPickUp()
        {
            if (pendingItem == null || pendingSpawner == null) return;

            if (!pendingSpawner.HasSpace(pendingItem))
            {
                fullMessage.SetActive(true);
                return;
            }

            pendingSpawner.Spawn(pendingItem);
            onPickedUp?.Invoke();
            Hide();
        }
    }
}
