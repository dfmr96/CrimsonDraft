using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [System.Serializable]
    public struct ContainerSaveData
    {
        public List<InventoryEntrySaveData> Items;

    }

    [System.Serializable]
    public class ContainerData : ISavable<ContainerSaveData>
    {
        public LocalizableText Label;

        // TODO - Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED;
        // --------


        public int Capacity;
        public List<InventoryEntry> Items = new List<InventoryEntry>();

        // --------------------------------------------------------------------

        public void Copy(ContainerData other)
        {
            Label = other.Label;
            Capacity = other.Capacity;
            Items = new List<InventoryEntry>(other.Items);
        }

        // --------------------------------------------------------------------

        public void FillCapacityWithEmptyEntries()
        {
            Debug.Assert(Items.Count <= Capacity, "Capacity can't be smaller than the pre-existing item count in the container");
            
            while (Items.Count < Capacity)
            {
                Items.Add(new InventoryEntry());
            }
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            Items.Clear();
        }

        // --------------------------------------------------------------------

        public ContainerSaveData GetSavableData()
        {
            ContainerSaveData saveData = new ContainerSaveData();
            saveData.Items = new List<InventoryEntrySaveData>();
            foreach (var item in Items)
            {
                if (item.Item != null)
                {
                    saveData.Items.Add(new InventoryEntrySaveData()
                    {
                        ItemId = item.Item.UniqueId,
                        Count = item.Count,
                        SecondaryCount = item.SecondaryCount
                    });
                }
            }
            return saveData;
        }


        // --------------------------------------------------------------------

        public void SetFromSavedData(ContainerSaveData savedData)
        {
            Items.Clear();

            if (savedData.Items != null)
            {
                var itemsDB = GameManager.Instance.GetDatabase<ItemDatabase>();
                foreach (var item in savedData.Items)
                {
                    Items.Add(new InventoryEntry()
                    {
                        Item = itemsDB.GetRegister(item.ItemId),
                        Count = item.Count,
                        SecondaryCount = item.SecondaryCount
                    });
                }
            }

            FillCapacityWithEmptyEntries();
        }


#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            if (!string.IsNullOrEmpty(Name_DEPRECATED))
            {
                Label.Unlocalized = Name_DEPRECATED;
                Name_DEPRECATED = "";
            }
        }
#endif
    }

    public abstract class ItemContainerBase : MonoBehaviour
    {
        [SerializeField] protected float m_OpenDelay = 1f;
        [SerializeField] private AudioClip m_OpenClip;
        [SerializeField] private AudioClip m_CloseClip;

        public UnityEvent OnOpen;
        public UnityEvent OnClose;

        private AudioSource m_AudioSource;

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            m_AudioSource = GetComponent<AudioSource>();
        }

        // --------------------------------------------------------------------

        public void Open()
        {
            StartCoroutine(OpenContainerRoutine());
        }

        // --------------------------------------------------------------------

        protected abstract ContainerData GetData();

        // --------------------------------------------------------------------

        IEnumerator OpenContainerRoutine()
        {
            if (m_OpenClip)
                m_AudioSource.PlayOneShot(m_OpenClip);

            PauseController.Instance.Pause(this);

            OnOpen?.Invoke();

            yield return Yielders.UnscaledTime(m_OpenDelay);

            UIManager.Get<UIItemContainer>().Show(GetData(), OnUIClosed);
        }

        // --------------------------------------------------------------------

        private void OnUIClosed()
        {
            if (m_CloseClip)
                m_AudioSource.PlayOneShot(m_CloseClip);

            OnClose?.Invoke();

            PauseController.Instance.Resume(this);
        }

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            GetData().EditorOnly_MigrateUnlocalizedData();
        }
#endif
    }
}
