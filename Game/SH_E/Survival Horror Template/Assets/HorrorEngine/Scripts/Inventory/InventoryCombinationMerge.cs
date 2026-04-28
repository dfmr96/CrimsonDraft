using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{

    [CreateAssetMenu(menuName = "Horror Engine/Combinations/Merge")]
    public class InventoryCombinationMerge : InventoryItemCombination
    {
        [SerializeField] InventoryEntry m_ResultEntry;

        // OBSOLETE Data (2.3) --- Remove in future update
        [FormerlySerializedAs("m_ResultItem")]
        [SerializeField] [HideInInspector] public ItemData m_ResultItem_OBSOLETE;
        [FormerlySerializedAs("m_ResultItemAmount")]
        [SerializeField] [HideInInspector] public int m_ResultItemAmount_OBSOLETE = 1;
        [FormerlySerializedAs("m_ResultItemSecondaryAmount")]
        [SerializeField] [HideInInspector] public int m_ResultItemSecondaryAmount_OBSOLETE = 0;
        [FormerlySerializedAs("m_ResultItemStatus")]
        [SerializeField] [HideInInspector] public float m_ResultItemStatus_OBSOLETE = 0f;
        // ---


        [SerializeField] public int m_ConsumeAmountItem1 = 1;
        [SerializeField] public int m_ConsumeAmountItem2 = 1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!m_ResultEntry.Item && m_ResultItem_OBSOLETE)
            {
                m_ResultEntry.Item = m_ResultItem_OBSOLETE;
                m_ResultEntry.Count = m_ResultItemAmount_OBSOLETE;
                m_ResultEntry.SecondaryCount = m_ResultItemSecondaryAmount_OBSOLETE;
                m_ResultEntry.Status = m_ResultItemStatus_OBSOLETE;

                m_ResultItem_OBSOLETE = null;

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
        }
#endif

        public override InventoryEntry OnCombine(InventoryEntry item1, InventoryEntry item2)
        {
            GameManager.Instance.Inventory.Remove(item1, m_ConsumeAmountItem1);
            GameManager.Instance.Inventory.Remove(item2, m_ConsumeAmountItem2);
            return GameManager.Instance.Inventory.Add(m_ResultEntry);
        }
    }
}