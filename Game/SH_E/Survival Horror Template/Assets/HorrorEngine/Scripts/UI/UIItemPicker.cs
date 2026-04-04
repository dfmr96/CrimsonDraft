using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIItemPicker : MonoBehaviour
    {
        private IUIInput m_Input;
        [SerializeField] private AudioClip m_ErrorClip;
        [SerializeField] private TextMeshProUGUI m_ItemName;

        private UIInventoryItem[] m_SelectableUIItems;
        private UIInventoryItem m_SelectedSlot;
        private UnityAction<ItemData> m_OnSelectCallback;
        private UnityAction m_OnCancelCallback;
        private List<ItemData> m_UsableItems;

        private void Start()
        {
            m_SelectableUIItems = GetComponentsInChildren<UIInventoryItem>();
            m_Input = GetComponentInParent<IUIInput>();

            gameObject.SetActive(false);

            foreach (var item in m_SelectableUIItems)
            {
                UISelectableCallbacks selectable = item.GetComponent<UISelectableCallbacks>();
                selectable.OnSelected.AddListener(OnSlotSelected);
            }
        }

        private void OnSlotSelected(GameObject slot)
        {
            m_SelectedSlot = slot.GetComponent<UIInventoryItem>();
            if (m_ItemName)
                m_ItemName.text = m_SelectedSlot.Data ? m_SelectedSlot.Data.Name : string.Empty;
        }

        private void OnDestroy()
        {
            foreach (var item in m_SelectableUIItems)
            {
                UISelectableCallbacks selectable = item.GetComponent<UISelectableCallbacks>();
                selectable.OnSelected.RemoveListener(OnSlotSelected);
            }
        }

        // TODO - Remove this method!
        public void Show(ItemData usableItem, string filterTag, UnityAction<ItemData> onSelectCallback, UnityAction onCancelCallback)
        {
            Show(new List<ItemData>() { usableItem }, filterTag, onSelectCallback, onCancelCallback);
        }

        public void Show(List<ItemData> usableItems, string filterTag, UnityAction<ItemData> onSelectCallback, UnityAction onCancelCallback)
        {
            CursorController.Instance.SetInUI(true);
            PauseController.Instance.Pause(this);
            m_OnSelectCallback = onSelectCallback;
            m_OnCancelCallback = onCancelCallback;
            FillSelectable(usableItems, filterTag);

            gameObject.SetActive(true);
        }


        private void FillSelectable(List<ItemData> usableItems, string filterTag)
        {
            m_UsableItems = usableItems;

            foreach (var selectable in m_SelectableUIItems)
            {
                selectable.Fill(new InventoryEntry());
            }

            var items = GameManager.Instance.Inventory.Items;
            int index = 0;
            bool noTagFilter = string.IsNullOrEmpty(filterTag);
            foreach (var item in items)
            {
                if (item.Item && (noTagFilter || (item.Item.Tags !=null && item.Item.Tags.Contains(filterTag))))
                {
                    //bool isInteractable = usableItems.Contains(item.Item);                  
                    //m_SelectableItems[index].GetComponent<Button>().interactable = isInteractable;
                    m_SelectableUIItems[index].Fill(item);

                    ++index;
                    if (index >= m_SelectableUIItems.Length)
                        break;
                }
            }
        }

        private void Update()
        {
            if (m_Input.IsConfirmDown())
            {
                if (m_SelectedSlot.Data && m_UsableItems.Contains(m_SelectedSlot.Data))
                {
                    m_OnSelectCallback?.Invoke(m_SelectedSlot.Data);
                    Hide();
                }
                else
                {
                    if (m_ErrorClip)
                        UIManager.Get<UIAudio>().Play(m_ErrorClip);
                }
            }

            if (m_Input.IsCancelDown())
            {
                m_OnCancelCallback?.Invoke();
                Hide();
            }

            if (m_SelectableUIItems.Length > 0)
                EventSystemUtils.SelectDefaultOnLostFocus(m_SelectableUIItems[0].gameObject);
        }

        public void Hide()
        {
            CursorController.Instance.SetInUI(false);
            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);
            m_OnSelectCallback = null;
            m_OnCancelCallback = null;
            UIManager.PopAction();
        }
    }
}