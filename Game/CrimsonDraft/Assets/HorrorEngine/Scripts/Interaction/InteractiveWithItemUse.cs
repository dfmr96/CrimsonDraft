using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    [RequireComponent(typeof(Interactive))]
    public class InteractiveWithItemUse : MonoBehaviour
    {
        [Serializable]
        public class ItemUseEntry
        {
            public ItemData Item;
            public int Priority;
            public UnityEvent<ItemData> OnItemUsed;
        }

        //[Tooltip("Item that can be used with this interactable")]
        //[SerializeField] private ItemData m_Item;

        [Tooltip("Items that can be used with this interactable")]
        [SerializeField] private ItemUseEntry[] m_UsableItems;

        [Tooltip("This indicates if the item can be used automatically if present in the inventory")]
        [SerializeField] private bool m_AutoUseOnInteraction = true;
        
        [Tooltip("This indicates if the item will be removed from the inventory after use")]
        [SerializeField] private bool m_RemoveItemFromInventory;

        [Tooltip("This choice will be shown to the player before using the time. Leave empty to ignore. Call Use() method on the affirmative choice")]
        [SerializeField] private Choice m_BeforeUseChoice;

        [Tooltip("This tag will be replaced in choice and dialog with the item name")]
        [SerializeField] private string m_ItemNameTag = "{ITEMNAME}";

        [HideInInspector]
        [FormerlySerializedAs("m_NoItemDialog")]
        [SerializeField] private string[] m_NoItemDialog_DEPRECATED;

        public UnityEvent OnItemNotInInventory;
        public UnityEvent<ItemData> OnItemUsed;

        private Interactive m_Interactive;
        private ItemUseEntry m_UsingItem;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Interactive = GetComponentInChildren<Interactive>();
            m_Interactive.OnInteract.AddListener(OnInteract);

            Debug.Assert(m_UsableItems != null && m_UsableItems.Length > 0, "Item has not been set on InteractWithItemUse", gameObject);

        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            m_Interactive.OnInteract.RemoveListener(OnInteract);
        }

        // --------------------------------------------------------------------

        private void OnInteract(IInteractor interactor)
        {
            m_UsingItem = null;
            ItemUseEntry selectedItem = null;
            foreach (var item in m_UsableItems)
            {
                if ((selectedItem == null|| item.Priority > selectedItem.Priority) && GameManager.Instance.Inventory.Contains(item.Item))
                {
                    selectedItem = item;
                }
            }

            if (selectedItem != null && m_AutoUseOnInteraction)
            {
                UseWithItem(selectedItem.Item);
            }
            else
            {
                OnItemNotInInventory?.Invoke();
            }
        }

        // --------------------------------------------------------------------

        public bool UseWithItem(ItemData item)
        {
            foreach (var itemEntry in m_UsableItems)
            {
                if (itemEntry.Item == item)
                {
                    UseWithEntry(itemEntry);
                    return true;
                }
            }

            return true;
        }


        // --------------------------------------------------------------------

        private void UseWithEntry(ItemUseEntry entry)
        {
            m_UsingItem = entry;

            if (m_BeforeUseChoice)
            {
                m_BeforeUseChoice.Data.ChoiceDialog.SetTagReplacement(m_ItemNameTag, entry.Item.Name);
                m_BeforeUseChoice.Choose();
            }
            else
            {
                Use();
            }
        }

        // --------------------------------------------------------------------

        public void Use()
        {
            Debug.Assert(m_UsingItem != null, "No item has been preselected by InteractiveWithItemUse. Use function can only be used after UseWithItem has been called");

            OnItemUsed?.Invoke(m_UsingItem.Item);
            m_UsingItem.OnItemUsed?.Invoke(m_UsingItem.Item);

            if (m_RemoveItemFromInventory)
            {
                GameManager.Instance.Inventory.Remove(m_UsingItem.Item);
            }

            m_UsingItem = null;
        }

        // --------------------------------------------------------------------

        public void Use(ItemData item) // Just an alias to expose it on the inspector
        {
            UseWithItem(item);
        }
    }
}