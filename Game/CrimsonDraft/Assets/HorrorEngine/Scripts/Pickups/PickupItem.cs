#if UNITY_EDITOR
using System;
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class ItemPickedUpMessage : BaseMessage
    {
        public ItemData Data;
        public bool ShowItemSplash = true;
    }

    public class PickupItem : Pickup, ISavableObjectStateExtra
    {
        public InventoryEntry Entry;
        public bool ShowItemSplash = true;
        
        [FormerlySerializedAs("Data")]
        [HideInInspector] public ItemData Data_OBSOLETE;
        [FormerlySerializedAs("Amount")]
        [HideInInspector] public int Amount_OBSOLETE = 1;
        
        [SerializeField] public bool m_UseOnPickup;

        private ItemPickedUpMessage m_PickMsg = new ItemPickedUpMessage();

#if UNITY_EDITOR

        // --------------------------------------------------------------------

        public bool IsObsolete => !Entry.Item && Data_OBSOLETE;

        [ContextMenu("Retrieve pickup data")]
        public void RetrievePickupData()
        {
            Entry.Item = Data_OBSOLETE;
            Entry.Count = Amount_OBSOLETE;
            Entry.SecondaryCount = 0;
            Entry.Status = 0;

            Data_OBSOLETE = null;

            EditorUtility.SetDirty(this);
        }
#endif

        // --------------------------------------------------------------------

        private void Awake()
        {
#if UNITY_EDITOR
            Debug.Assert(!IsObsolete, $"PickupItem {name} data is obsolete, enter prefab mode to update automatically or use the \"Retrieve pickup data\" context action if this isn't a prefab", gameObject);
#endif
        }

        // --------------------------------------------------------------------

        public override void Take()
        {
            InventoryEntry entry = GameManager.Instance.Inventory.TryAddAndShowFullMessage(Entry);
            
            bool isInventoryExpansion = Entry.Item is InventoryExpansionItemData;
            if (entry == null && !isInventoryExpansion && !m_UseOnPickup)
                return;
            
            m_PickMsg.Data = Entry.Item;
            m_PickMsg.ShowItemSplash = ShowItemSplash;
            MessageBuffer<ItemPickedUpMessage>.Dispatch(m_PickMsg);
            gameObject.SetActive(false);

            // Some items might need to be used on pickup even if they're not added to the inventory (e.g. Hip Pouch)
            if (m_UseOnPickup)
            {
                Entry.Item.OnUse(entry, GameManager.Instance.Player);
            }

            base.Take();
        }

        //-----------------------------------------------------
        // ISavable implementation
        //-----------------------------------------------------

        public string GetSavableData()
        {
            InventoryEntrySaveData savable = new InventoryEntrySaveData()
            {
                ItemId = Entry.Item ? Entry.Item.UniqueId : "",
                Count = Entry.Count,
                SecondaryCount = Entry.SecondaryCount,
                Status = Entry.Status
            };

            return JsonUtility.ToJson(savable);
        }

        public void SetFromSavedData(string savedData)
        {
            
            InventoryEntrySaveData savable = JsonUtility.FromJson<InventoryEntrySaveData>(savedData);
            if (savable.ItemId != Entry.Item.UniqueId)
            {
                var itemDB = GameManager.Instance.GetDatabase<ItemDatabase>();
                if (itemDB)
                    Entry.Item = string.IsNullOrEmpty(savable.ItemId) ? null : itemDB.GetRegister(savable.ItemId);
            }

            Entry.Count = savable.Count;
            Entry.SecondaryCount = savable.SecondaryCount;
            Entry.Status = savable.Status;
            
        }

    }
}
