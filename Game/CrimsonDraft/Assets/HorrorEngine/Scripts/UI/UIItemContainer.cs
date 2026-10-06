using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class UIItemContainer : MonoBehaviour
    {
        [SerializeField] private Transform m_ContainerItemsParent;
        [SerializeField] private List<UIInventoryItem> m_InventorySlots;
        [SerializeField] private GameObject m_ContainerItemPrefab;
        [SerializeField] private int m_InitialCapacity = 64;
        [SerializeField] private TMPro.TextMeshProUGUI m_ContainerName;

        [SerializeField] private TMPro.TextMeshProUGUI m_ItemName;
        [SerializeField] private TMPro.TextMeshProUGUI m_ItemDesc;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CancelClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_NavigateClip;

        [SerializeField] private bool m_SelectWithOneClick;

        private IUIInput m_Input;
        private ContainerData m_ContainerData;
        private List<GameObject> m_ContainerItems = new List<GameObject>();
        private UIInventoryItem m_SelectedItem;
        private UIInventoryItem m_LockedItem;
        private Action m_OnCloseCallback;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();

            foreach (var slot in m_InventorySlots)
            {
                UISelectableCallbacks selectable = slot.GetComponent<UISelectableCallbacks>();
                selectable.OnSelected.AddListener(OnSlotSelected);

                UIPointerClickEvents pointerEvents = slot.GetComponent<UIPointerClickEvents>();
                if (!m_SelectWithOneClick)
                    pointerEvents.OnDoubleClick.AddListener(OnSubmit);
            }


            SetCapacity(m_InitialCapacity);

            gameObject.SetActive(false);

        }


        // --------------------------------------------------------------------

        void OnSlotSelected(GameObject obj)
        {
            UIInventoryItem slot = obj.GetComponent<UIInventoryItem>();
            m_SelectedItem = slot;
            //m_SelectedSlot = slot;
            if (slot.InventoryEntry != null && slot.InventoryEntry.Item)
            {
                m_ItemName.text = slot.InventoryEntry.Item.Name;
                m_ItemDesc.text = slot.InventoryEntry.Item.Description;
                if (m_NavigateClip)
                    UIManager.Get<UIAudio>().Play(m_NavigateClip);
            }
            else
            {
                m_ItemName.text = "";
                m_ItemDesc.text = "";
            }

            if (m_SelectWithOneClick)
            {
                StartCoroutine(SubmitNextFrame());
            }
        }

        // --------------------------------------------------------------------

        IEnumerator SubmitNextFrame()
        {
            yield return null;
            OnSubmit();
        }

        // --------------------------------------------------------------------

        private void FillEquipped(EquipmentSlot slot, UIInventoryItem uiItem)
        {
            InventoryEntry equippedPrim = GameManager.Instance.Inventory.GetEquipped(slot);
            if (equippedPrim != null)
                uiItem.Fill(equippedPrim);
            else
                uiItem.Fill(null);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            if (m_Input.IsConfirmDown())
            {
                OnSubmit();
            }
            if (m_Input.IsCancelDown())
            {
                if (m_LockedItem)
                {
                    if (m_CancelClip)
                        UIManager.Get<UIAudio>().Play(m_CancelClip);

                    ReleaseLocked();
                }
                else
                {
                    Close();
                }
            }

            if (!m_SelectWithOneClick)
                EventSystemUtils.SelectDefaultOnLostFocus(m_InventorySlots[0].gameObject);
        }

        // --------------------------------------------------------------------
        private void OnSubmit()
        {
            if (m_LockedItem)
            {
                if (m_LockedItem != m_SelectedItem)
                {
                    InventoryEntry equipped = null;
                    if (GetEquippedMovedOutOfInventory(ref equipped))
                    {
                        var equipable = equipped.Item as EquipableItemData;
                        GameManager.Instance.Inventory.Unequip(equipable.Slot);
                    }

                    InventoryEntry.Swap(m_LockedItem.InventoryEntry, m_SelectedItem.InventoryEntry);
                    FillContainer();
                    FillInventory();
                }

                ReleaseLocked();
            }
            else if (m_SelectedItem)
            {
                m_LockedItem = m_SelectedItem;
                m_LockedItem.SetSelectionLocked(true);
            }
        }

        // --------------------------------------------------------------------

        private void ReleaseLocked()
        {
            m_LockedItem.SetSelectionLocked(false);
            m_LockedItem = null;
        }

        // --------------------------------------------------------------------

        private bool GetEquippedMovedOutOfInventory(ref InventoryEntry outEquipped)
        {
            var equippedDict = GameManager.Instance.Inventory.Equipped;
            foreach (var e in equippedDict)
            {
                InventoryEntry equipped = e.Value;
                if (equipped == m_LockedItem.InventoryEntry ||
                    equipped == m_SelectedItem.InventoryEntry)
                {
                    outEquipped = e.Value;
                    return (m_LockedItem.InventoryEntry == equipped && !m_InventorySlots.Contains(m_SelectedItem)) ||
                           (m_SelectedItem.InventoryEntry == equipped) && !m_InventorySlots.Contains(m_LockedItem);
                }
            }

            return false;
        }
        
        // --------------------------------------------------------------------

        private void Close()
        {
            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);

            if (m_CloseClip)
                UIManager.Get<UIAudio>().Play(m_CloseClip);

            gameObject.SetActive(false);

            m_OnCloseCallback?.Invoke();
            m_OnCloseCallback = null;
        }

        // --------------------------------------------------------------------

        public void Show(ContainerData data, Action closeCallback = null)
        {
            m_LockedItem = null;
            m_SelectedItem = null;
            m_OnCloseCallback = closeCallback;
            m_ContainerData = data;
            m_ContainerName.text = data.Label;

            PauseController.Instance.Pause(this);
            CursorController.Instance.SetInUI(true);

            if (m_ShowClip)
                UIManager.Get<UIAudio>().Play(m_ShowClip);

            FillContainer();
            FillInventory();

            gameObject.SetActive(true);          
        }


        // --------------------------------------------------------------------

        private void FillInventory()
        {
            var items = GameManager.Instance.Inventory.Items;

            for (int i = 0; i < items.Count; ++i)
            {
                m_InventorySlots[i].Fill(items[i]);
                m_InventorySlots[i].gameObject.SetActive(true);
            }

            for (int i = items.Count; i < m_InventorySlots.Count; ++i)
            {
                m_InventorySlots[i].gameObject.SetActive(false);
            }
        }

        // --------------------------------------------------------------------

        private void FillContainer()
        {
            SetCapacity(m_ContainerData.Capacity);
            for (int i = 0; i < m_ContainerData.Capacity; ++i)
            {
                m_ContainerItems[i].GetComponent<UIInventoryItem>().Fill(m_ContainerData.Items[i]);   
            }
        }

        // --------------------------------------------------------------------

        private void SetCapacity(int count)
        {
            // Enable or spawn items to match new capacity
            int lastIndex = 0;
            for (int i = 0; i < count; ++i)
            {
                if (i >= m_ContainerItems.Count)
                {
                    PooledGameObject item = GameObjectPool.Instance.GetFromPool(m_ContainerItemPrefab, m_ContainerItemsParent);
                    item.gameObject.SetActive(true);
                    m_ContainerItems.Add(item.gameObject);

                    UISelectableCallbacks selectable = item.GetComponent<UISelectableCallbacks>();
                    selectable.OnSelected.AddListener(OnSlotSelected);

                    UIPointerClickEvents pointerEvents = item.GetComponent<UIPointerClickEvents>();
                    pointerEvents.OnDoubleClick.AddListener(OnSubmit);
                }
                else
                {
                    m_ContainerItems[i].SetActive(true);
                }
                lastIndex = i;
            }

            // Disable remaining items
            for (int j = lastIndex+1; j < m_ContainerItems.Count; ++j)
            {
                m_ContainerItems[j].SetActive(false);
            }
        }
    }
}
