using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class ShowInventoryMessage: BaseMessage { }
    public class HideInventoryMessage : BaseMessage { }

    [Serializable]
    public class UIEquippedItem
    {
        public UIInventoryItem UIItem;
        public string Slot;
        public bool IsSelectable = true;
    }

    public class UIInventory : MonoBehaviour
    {
        [SerializeField] private bool m_PauseGame = true;
        [SerializeField] private GameObject m_Content;
        [SerializeField] private UIInventoryContextMenu m_ContextMenu;
        [SerializeField] private UIInventoryItem[] m_ItemSlots;
        [SerializeField] private UIItemFiller m_ItemInfoFiller;
        [SerializeField] private UIEquippedItem[] m_Equipped;
        [SerializeField] private float m_ExpandingInteractionDelay = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_UseClip;
        [SerializeField] private AudioClip m_CantUseClip;
        [SerializeField] private AudioClip m_NavigateClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_CancelClip;
        [SerializeField] private AudioClip m_ExpandClip;

        [Header("Advanced Customization")]
        [SerializeField] private bool m_SelectWithOneClick;
        [SerializeField] private bool m_MoveContextMenuToItem;
        [SerializeField] private bool m_AlwaysRebuild;
        [SerializeField] private Transform m_RebuildItemParent;
        [SerializeField] private GameObject m_RebuildItemPrefab;

        public UnityEvent OnShow;

        private UIInventoryItem m_SelectedSlot;
        private UIInventoryItem m_CombiningSlot;

        private IUIInput m_Input;
        private bool m_Expanding;
        private UIContext m_UIContext;

        // --------------------------------------------------------------------

        protected void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();

            if (!m_AlwaysRebuild)
            {
                foreach (var slot in m_ItemSlots)
                {
                    RegisterItemCallbacks(slot);
                }
            }

            foreach (var equipped in m_Equipped)
            {
                if (equipped.IsSelectable)
                {
                    RegisterItemCallbacks(equipped.UIItem);
                }
                else
                {
                    var button = equipped.UIItem.GetComponent<Button>();
                    if (button)
                        button.enabled = false;
                }
            }

            m_ContextMenu.UseButton.onClick.AddListener(OnUse);
            m_ContextMenu.EquipButton.onClick.AddListener(OnEquip);
            m_ContextMenu.CombineButton.onClick.AddListener(OnCombine);
            m_ContextMenu.ExamineButton.onClick.AddListener(OnExamine);
            m_ContextMenu.DropButton.onClick.AddListener(OnDrop);

            MessageBuffer<ShowInventoryMessage>.Subscribe(OnShowInventoryMsg);
            MessageBuffer<HideInventoryMessage>.Subscribe(OnHideInventoryMsg);
        }

        private void OnDestroy()
        {
            MessageBuffer<ShowInventoryMessage>.Unsubscribe(OnShowInventoryMsg);
            MessageBuffer<HideInventoryMessage>.Unsubscribe(OnHideInventoryMsg);
        }

        // --------------------------------------------------------------------

        void RegisterItemCallbacks(UIInventoryItem slot)
        {
            UISelectableCallbacks selectable = slot.GetComponent<UISelectableCallbacks>();
            selectable.OnSelected.AddListener(OnSlotSelected);

            UIPointerClickEvents pointerEvents = slot.GetComponent<UIPointerClickEvents>();
            if (!m_SelectWithOneClick)
                pointerEvents.OnDoubleClick.AddListener(OnSubmit);
        }

        // --------------------------------------------------------------------

        void UnregisterItemCallbacks(UIInventoryItem slot)
        {
            UISelectableCallbacks selectable = slot.GetComponent<UISelectableCallbacks>();
            selectable.OnSelected.RemoveListener(OnSlotSelected);

            UIPointerClickEvents pointerEvents = slot.GetComponent<UIPointerClickEvents>();
            if (!m_SelectWithOneClick)
                pointerEvents.OnDoubleClick.RemoveListener(OnSubmit);
        }


        // --------------------------------------------------------------------

        void OnSlotSelected(GameObject obj)
        {
            if (m_SelectedSlot) // Cancel previous selection
            {
                if (m_ContextMenu.isActiveAndEnabled)
                {
                    m_ContextMenu.gameObject.SetActive(false);
                }
                
                if (m_SelectedSlot != m_CombiningSlot)
                    m_SelectedSlot.SetSelectionLocked(false);

                m_SelectedSlot = null;
            }

            UIInventoryItem slot = obj.GetComponent<UIInventoryItem>();
            m_SelectedSlot = slot;
            if (slot.InventoryEntry != null && slot.InventoryEntry.Item)
            {
                m_ItemInfoFiller.Fill(slot.InventoryEntry);
                UIManager.Get<UIAudio>().Play(m_NavigateClip);
            }
            else
            {
                m_ItemInfoFiller.Clear();
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

        void Start()
        {
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            if (m_Expanding)
                return;

            if (m_Input.IsCancelDown() || m_Input.IsToggleInventoryDown())
            {
                OnCancel();
            }

            if (m_SelectedSlot && m_SelectedSlot.InventoryEntry != null)
            {
                if (m_Input.IsConfirmDown())
                {
                    OnSubmit();
                }
            }

            if (!m_SelectWithOneClick)
            {
                GameObject defaultSelection = m_SelectedSlot ? m_SelectedSlot.gameObject : (m_ItemSlots.Length > 0 ? m_ItemSlots[0].gameObject : null);
                EventSystemUtils.SelectDefaultOnLostFocus(defaultSelection);
            }
        }

        // --------------------------------------------------------------------

        private void OnHideInventoryMsg(HideInventoryMessage ev)
        {
            if(isActiveAndEnabled)
                Hide();
        }

        // --------------------------------------------------------------------

        private void OnShowInventoryMsg(ShowInventoryMessage ev)
        {
            if (CanBeShown())
                Show();
        }

        // --------------------------------------------------------------------

        public void Show()
        {
            if (m_PauseGame)
                PauseController.Instance.Pause(this);

            CursorController.Instance.SetInUI(true);
            
            m_SelectedSlot = null;
            m_CombiningSlot = null;
            m_Expanding = false;

            gameObject.SetActive(true); // Needs to happen before fill or animations wont play

            Fill();
            FillEquipped();
            
            m_ContextMenu.gameObject.SetActive(false);

            UIManager.Get<UIAudio>().Play(m_ShowClip);

            if (m_ShowClip)
                UIManager.Get<UIAudio>().Play(m_ShowClip);

            if (m_Expanding)
            {
                if (m_ExpandClip)
                    UIManager.Get<UIAudio>().Play(m_ExpandClip);

                this.InvokeActionUnscaled(EndExpansion, m_ExpandingInteractionDelay);
            }

            m_UIContext.Activate();
            
            OnShow?.Invoke();
        }

        // --------------------------------------------------------------------

        void EndExpansion()
        {
            m_Expanding = false;
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            if (m_PauseGame)
                PauseController.Instance.Resume(this);

            CursorController.Instance.SetInUI(false);
            m_ContextMenu.gameObject.SetActive(false);

            gameObject.SetActive(false);

            m_UIContext.Deactivate();

            if (m_CloseClip)
                UIManager.Get<UIAudio>().Play(m_CloseClip);

            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        public void OnCancel()
        {
            if (m_CombiningSlot)
            {
                CancelCombine();
            }
            else if (m_ContextMenu.isActiveAndEnabled)
            {
                CloseContextMenu();
            }
            else
            {
                Hide();
            }
        }

        // --------------------------------------------------------------------

        public void CloseContextMenu()
        {
            if (m_CancelClip)
                UIManager.Get<UIAudio>().Play(m_CancelClip);

            m_ContextMenu.gameObject.SetActive(false);
            if (m_SelectedSlot)
                m_SelectedSlot.SetSelectionLocked(false);
        }

        // --------------------------------------------------------------------

        private void CancelCombine()
        {

            if (m_CancelClip)
                UIManager.Get<UIAudio>().Play(m_CancelClip);

            m_CombiningSlot.SetSelectionLocked(false);
            m_CombiningSlot = null;
        }

        // --------------------------------------------------------------------

        private void OnSubmit()
        {
            if (m_SelectedSlot)
                m_SelectedSlot.SetSelectionLocked(false);

            m_SelectedSlot = EventSystem.current.currentSelectedGameObject.GetComponent<UIInventoryItem>();
            if (!IsInventorySlot(m_SelectedSlot)) // Selected obj is not an inventory item (this can rarely happen if a quick slot gets selected)
            {
                m_SelectedSlot = null;
                if (m_CombiningSlot)
                    CancelCombine();

                return;
            }

            if (!m_SelectedSlot)
                return;

            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            if (m_CombiningSlot && m_SelectedSlot != m_CombiningSlot)
            {
                Combine();
            }
            else
            {
                if (!m_ContextMenu.Show(item, m_MoveContextMenuToItem ? m_SelectedSlot.transform : null))
                {
                    OnUse();
                }
                else
                {
                    m_SelectedSlot.SetSelectionLocked(true);
                }
            }
        }

        // --------------------------------------------------------------------

        private bool IsInventorySlot(UIInventoryItem slot)
        {
            return slot && (m_ItemSlots.Contains(slot) || IsEquipmentSlot(slot));
        }

        // --------------------------------------------------------------------

        private bool IsEquipmentSlot(UIInventoryItem slot)
        {
            foreach(var equipped in m_Equipped)
            {
                if (equipped.UIItem == slot)
                    return true;
            }
            return false;
        }

        // --------------------------------------------------------------------

        private void OnUse()
        {
            if (GameManager.Instance.Inventory.Use(m_SelectedSlot.InventoryEntry))
            {
                UIManager.Get<UIAudio>().Play(m_UseClip);

                ItemData item = m_SelectedSlot.InventoryEntry.Item;
                if (item && item.Flags.HasFlag(ItemFlags.UseOnInteractive))
                {
                    Hide();
                }
                else
                {
                    Fill();
                    FillEquipped();
                }
            }
            else
            {
                UIManager.Get<UIAudio>().Play(m_CantUseClip);
            }

            m_Input.Flush(); // Flush so an object is not immediately selected after this
        }

        // --------------------------------------------------------------------

        private void OnEquip()
        {
            EquipableItemData equipableItem = m_SelectedSlot.Data as EquipableItemData;
            bool wasSomethingEquipped = GameManager.Instance.Inventory.GetEquipped(equipableItem.SlotTag) != null;
            GameManager.Instance.Inventory.ToggleEquip(m_SelectedSlot.InventoryEntry);
            UIManager.Get<UIAudio>().Play(m_UseClip);

            if (equipableItem.MoveOutOfInventoryOnEquip || wasSomethingEquipped)
                Fill();

            FillEquipped();

            m_Input.Flush(); // Flush so an object is not immediately selected after this
        }

        // --------------------------------------------------------------------

        private void OnCombine()
        {
            if (m_CombiningSlot)
            {
                if (m_SelectedSlot.InventoryEntry.Item)
                    Combine();
                else
                    CancelCombine();
            }
            else if (m_SelectedSlot.InventoryEntry.Item)
            {
                // First item of the combination picked
                m_SelectedSlot.SetSelectionLocked(true);
                m_CombiningSlot = m_SelectedSlot;
                
                if (!m_SelectWithOneClick)
                    EventSystem.current.SetSelectedGameObject(m_SelectedSlot.gameObject);
            }
        }


        // --------------------------------------------------------------------

        private void OnExamine()
        {
            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            Hide();

            UIManager.Get<UIExamineItem>().Show(item);
        }

        // --------------------------------------------------------------------


        private void OnDrop()
        {
            ItemData item = m_SelectedSlot.InventoryEntry.Item;
            if (item == null)
                return;

            var gameMgr = GameManager.Instance;
            
            if (item.Flags.HasFlag(ItemFlags.CreatePickupOnDrop))
            {
                gameMgr.Player.GetComponentInChildren<PickupDropper>().Drop(m_SelectedSlot.InventoryEntry);
            }

            gameMgr.Inventory.Drop(m_SelectedSlot.InventoryEntry); 

            Hide();
        }

        // --------------------------------------------------------------------

        public void OnFilesCategory()
        {
            Hide();
            UIManager.Get<UIDocs>().Show();
        }

        // --------------------------------------------------------------------

        public void OnMapCategory()
        {
            Hide();
            UIManager.Get<UIMap>().Show();
        }


        // --------------------------------------------------------------------

        private void Combine()
        {
            InventoryEntry highlightedEntry = GameManager.Instance.Inventory.Combine(m_SelectedSlot.InventoryEntry, m_CombiningSlot.InventoryEntry);
            
            Fill();
            FillEquipped();

            if (highlightedEntry != null)
            {
                for (int i = 0; i < m_ItemSlots.Length; ++i)
                {
                    if (m_ItemSlots[i].InventoryEntry == highlightedEntry)
                    {
                        EventSystem.current.SetSelectedGameObject(m_ItemSlots[i].gameObject);
                    }
                }
            }

            m_CombiningSlot = null;
        }

        // --------------------------------------------------------------------

        private void Fill()
        {
            var inventory = GameManager.Instance.Inventory;
            var items = inventory.Items;
            int itemsCount = items.Count;

            m_Expanding = inventory.Expanded;
            inventory.Expanded = false;

            if (m_AlwaysRebuild)
            {
                RebuildItems();
            }

            for (int i = 0; i < items.Count; ++i)
            {
                m_ItemSlots[i].gameObject.SetActive(i < itemsCount);

                if (i >= inventory.PreExpansionSize && m_Expanding)
                {
                    m_ItemSlots[i].GetComponent<Animator>().Play("Expand");
                }

                m_ItemSlots[i].Fill(items[i]);
            }

            for (int i = items.Count; i < m_ItemSlots.Length; ++i)
            {
                m_ItemSlots[i].gameObject.SetActive(false);
            }
        }

        // --------------------------------------------------------------------

        private void DestroyItems()
        {
            for (int i = m_ItemSlots.Length - 1; i >= 0; --i)
            {
                UnregisterItemCallbacks(m_ItemSlots[i]);
                Destroy(m_ItemSlots[i].gameObject);
            }
        }

        // --------------------------------------------------------------------

        private void RebuildItems()
        {
            DestroyItems();

            int itemCount = GameManager.Instance.Inventory.Items.Count;
            Array.Resize(ref m_ItemSlots, itemCount);
            for (int i = 0; i < itemCount; ++i)
            {
                var pooledItem = GameObjectPool.Instance.GetFromPool(m_RebuildItemPrefab);
                GameObject instance = pooledItem.gameObject;
                instance.transform.SetParent(m_RebuildItemParent, false);

                var inventoryItem = instance.GetComponent<UIInventoryItem>();
                Debug.Assert(inventoryItem, "Inventory item prefab didn't have an expected UIInventoryItem component");
                RegisterItemCallbacks(inventoryItem);

                m_ItemSlots[i] = inventoryItem;
            }
        }

        // --------------------------------------------------------------------

        private void FillEquipped()
        {
            foreach (var equiped in m_Equipped)
            {
                FillEquipped(equiped);
            }
        }

        // --------------------------------------------------------------------

        private void FillEquipped(UIEquippedItem equipped)
        {
            InventoryEntry equippedPrim = GameManager.Instance.Inventory.GetEquipped(equipped.Slot);
            if (equippedPrim != null)
                equipped.UIItem.Fill(equippedPrim);
            else
                equipped.UIItem.Fill(null);
        }


        // --------------------------------------------------------------------

        public int GetSelectedSlotIndex()
        {
            return m_SelectedSlot ? GameManager.Instance.Inventory.IndexOf(m_SelectedSlot.InventoryEntry) : -1;
        }

        // --------------------------------------------------------------------

        public bool CanBeShown()
        {
            return !m_UIContext.IsBlocked();
        }
    }
}