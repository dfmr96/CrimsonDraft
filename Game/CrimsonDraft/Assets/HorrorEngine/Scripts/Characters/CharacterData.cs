using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public class CharacterMessage : BaseMessage
    {
        public CharacterData Character;
    }

    [Serializable]
    public struct CharacterStateSaveData
    {
        public string HandleId;
        public InventorySaveData Inventory;
        public ContainerSaveData StorageBox;
        public GameAttributesSavable Attributes;
    }

    [Serializable]
    public class CharacterState : ISavable<CharacterStateSaveData>
    {
        public Inventory Inventory = new Inventory();
        public ContainerData StorageBox; // TODO - Add an option to share this between players?
        public GameAttributes Attributes = new();

        private CharacterData m_Data;

        public CharacterData Data => m_Data;

        // --------------------------------------------------------------------

        public CharacterState(CharacterData data)
        {
            m_Data = data;

            Inventory.Init(data);

            StorageBox = new ContainerData();
            StorageBox.Copy(data.InitialStorageBox);
            StorageBox.FillCapacityWithEmptyEntries();

            Attributes.Init(data.InitialAttributes);
        }

        // --------------------------------------------------------------------

        public void SetupInitialEquipment(PlayerActor player)
        {
            PlayerEquipment equipment = player.GetComponentInChildren<PlayerEquipment>();
            foreach (var e in m_Data.InitialEquipment)
            {
                if (Inventory.TryGet(e, out InventoryEntry entry))
                    Inventory.Equip(entry);
                else
                    equipment.Equip(e, e.Slot);
            }
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            StorageBox.Clear();
            Inventory.Clear();
        }

        // --------------------------------------------------------------------

        public CharacterStateSaveData GetSavableData()
        {
            CharacterStateSaveData characterSavedData = new CharacterStateSaveData();
            characterSavedData.HandleId = m_Data.UniqueId;
            characterSavedData.Inventory = Inventory.GetSavableData();
            characterSavedData.StorageBox = StorageBox.GetSavableData();
            characterSavedData.Attributes = Attributes.GetSavableData();
            return characterSavedData;
        }

        public void SetFromSavedData(CharacterStateSaveData savedData)
        {
            Inventory.SetFromSavedData(savedData.Inventory);
            StorageBox.SetFromSavedData(savedData.StorageBox);
            Attributes.SetFromSavedData(savedData.Attributes);
        }

    }

    [CreateAssetMenu(fileName = "CharacterData", menuName = "Horror Engine/Character", order = -1)]
    public class CharacterData : Register
    {
        public LocalizableText Name;
        public LocalizableText CodeName;
        public GameObject Prefab;
        public Sprite Portrait;

        [Header("Initial State")]
        public int InitialInventorySize = 8;
        public InventoryEntry[] InitialInventory;
        public ContainerData InitialStorageBox;
        public DocumentData[] InitialDocuments;
        public MapData[] InitialMaps;
        public EquipableItemData[] InitialEquipment;
        public GameAttributeValueEntry[] InitialAttributes;

        // TODO - Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED;
        [Tooltip("This is a shorten version to identify the player save game")]
        [FormerlySerializedAs("CodeName")]
        [HideInInspector]
        public string CodeName_DEPRECATED;
        // ----------------

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Name_DEPRECATED))
            {
                Name.Unlocalized = Name_DEPRECATED;
                Name_DEPRECATED = "";
                dirty = true;
            }

            if (!string.IsNullOrEmpty(CodeName_DEPRECATED))
            {
                CodeName.Unlocalized = CodeName_DEPRECATED;
                CodeName_DEPRECATED = "";
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }

        }
#endif
    }
}