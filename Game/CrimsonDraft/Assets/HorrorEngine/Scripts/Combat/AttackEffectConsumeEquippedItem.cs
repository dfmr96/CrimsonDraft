using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Consume Equipped Item")]
    public class AttackEffectConsumeEquippedItem : AttackEffect
    {
        [SerializeField] private EquipmentSlot m_Slot;

        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            GameManager.Instance.Inventory.ClearEquipped(m_Slot);            
        }
    }
}