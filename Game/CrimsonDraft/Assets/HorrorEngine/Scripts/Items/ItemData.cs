using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [Flags]
    public enum InventoryMainAction
    {
        None,
        Use,
        Equip
    }

    [Flags]
    public enum ItemFlags
    {
        Stackable           = 1 << 1,
        ConsumeOnUse        = 1 << 2,
        Combinable          = 1 << 3,
        Examinable          = 1 << 4,
        Droppable           = 1 << 5,
        CreatePickupOnDrop  = 1 << 6,
        UseOnInteractive    = 1 << 7,
        Depletable          = 1 << 8,
    }

    [CreateAssetMenu(menuName = "Horror Engine/Items/Item")]
    public class ItemData : Register
    {
        public Sprite Image;
        public GameObject ExamineModel;
        public LocalizableText Name;
        public LocalizableText Description;
        public InventoryMainAction InventoryAction = InventoryMainAction.Use;
        public ItemFlags Flags;
        public SpawnableSavable DropPrefab;
        [ShowIf("Flags", Op.BitmaskContain, ItemFlags.Stackable)]
        public int MaxStackSize;
        [SerializeField] public AudioClip m_OnUseAudioClip;

        // TODO - Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED;
        [FormerlySerializedAs("Description")]
        [HideInInspector]
        public string Description_DEPRECATED;
        // -----

        private void OnEnable()
        {
            ItemOverride[] itemOverrides = Resources.LoadAll<ItemOverride>("");
            foreach (var iOverride in itemOverrides)
            {
                if (iOverride.Item == this && iOverride.ApplyOnAwake)
                {
                    foreach (ItemOverrideData propOverride in iOverride.Overrides)
                    {
                        propOverride.Override(this);
                    }
                }
            }   
        }

        public virtual void OnUse(InventoryEntry entry, Actor user)
        {
            if (m_OnUseAudioClip)
                UIManager.Get<UIAudio>().Play(m_OnUseAudioClip);
        }
        
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

            if (!string.IsNullOrEmpty(Description_DEPRECATED))
            {
                Description.Unlocalized = Description_DEPRECATED;
                Description_DEPRECATED = "";
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