using UnityEngine;

namespace HorrorEngine
{

    [RequireComponent(typeof(UIItemFiller))]
    public class UIInventoryItem : MonoBehaviour
    {
        [SerializeField] private GameObject m_SelectionLocked;
        
        public InventoryEntry InventoryEntry { get; private set; }

        public ItemData Data => InventoryEntry.Item;

        private UIItemFiller m_Filler;

        public void Fill(InventoryEntry entry)
        {
            InventoryEntry = entry;
           
            FillItem(entry);
           
            if (m_SelectionLocked)
                m_SelectionLocked.gameObject.SetActive(false);
        }

        public void FillItem(InventoryEntry entry)
        {
            if (!m_Filler)
                m_Filler = GetComponent<UIItemFiller>();

            if (entry != null && entry.Item != null)
            {
                m_Filler.Fill(entry);
            }
            else
            {
                m_Filler.Clear();
               
                if (m_SelectionLocked)
                    m_SelectionLocked.gameObject.SetActive(false);
            }
        }


        public void SetSelectionLocked(bool islocked)
        {
            if (m_SelectionLocked)
                m_SelectionLocked.gameObject.SetActive(islocked);
        }
    }
}