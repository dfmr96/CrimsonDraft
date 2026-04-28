
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Items/GameAttributeItem")]
    public class GameAttributeItemData : ItemData
    {
        [SerializeField] private GameAttribute m_Attribute;
        [SerializeField] private GameAttributeOp m_Operation;
        [SerializeField] private float m_Amount;

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);

            GameAttributeUtils.PerformOperation(m_Attribute, m_Amount, m_Operation);
        }
    }
}