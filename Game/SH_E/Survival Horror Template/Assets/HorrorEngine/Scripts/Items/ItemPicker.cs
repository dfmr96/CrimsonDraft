using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class ItemPickerEntry
    {
        public ItemData Item;
        public UnityEvent<ItemData> OnItemUsed;
        public bool RemoveFromInventory = true;
    }

    public class ItemPicker : MonoBehaviour
    {
        [SerializeField] ItemPickerEntry[] m_UsableItems;
        [SerializeField] string m_ItemFilter;

        [Space]
        public UnityEvent OnSelected;
        public UnityEvent OnCanceled;

        private List<ItemData> m_UsableItemData = new List<ItemData>();

        private UnityAction<ItemData> m_OnItemPicked;
        private UnityAction m_OnPickerCancelled;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnItemPicked = OnItemPicked;
            m_OnPickerCancelled = OnPickerCancelled;

            foreach (var usable in m_UsableItems)
            {
                m_UsableItemData.Add(usable.Item);
            }
        }

        // --------------------------------------------------------------------

        public void ShowPicker()
        {
            UIManager.Get<UIItemPicker>().Show(m_UsableItemData, m_ItemFilter, m_OnItemPicked, m_OnPickerCancelled);
        }

        // --------------------------------------------------------------------

        private void OnItemPicked(ItemData itemData)
        {
            foreach (var usable in m_UsableItems)
            {
                if (itemData == usable.Item)
                {
                    if (usable.RemoveFromInventory)
                        GameManager.Instance.Inventory.Remove(itemData);

                    usable.OnItemUsed?.Invoke(itemData);
                }
            }
            OnSelected?.Invoke();
        }

        // --------------------------------------------------------------------

        private void OnPickerCancelled()
        {
            OnCanceled.Invoke();
        }
    }
}