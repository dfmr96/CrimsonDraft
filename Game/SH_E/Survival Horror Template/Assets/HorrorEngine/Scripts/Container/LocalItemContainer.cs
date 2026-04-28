using System.Collections;
using UnityEngine;

namespace HorrorEngine
{

    public class LocalItemContainer : ItemContainerBase, ISavableObjectStateExtra
    {
        [SerializeField] private ContainerData m_Data;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Data.FillCapacityWithEmptyEntries();
        }

        // --------------------------------------------------------------------

        protected override ContainerData GetData()
        {
            return m_Data;
        }

        // --------------------------------------------------------------------

        public bool IsEmpty()
        {
            foreach(var item in m_Data.Items)
            {
                if (item.Item)
                    return false;
            }
            return true;
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        public string GetSavableData()
        {
            return JsonUtility.ToJson(m_Data.GetSavableData());
        }

        // --------------------------------------------------------------------

        public void SetFromSavedData(string savedData)
        {
            ContainerSaveData saveData = JsonUtility.FromJson<ContainerSaveData>(savedData);
            m_Data.SetFromSavedData(saveData);
        }

    }
}