using UnityEngine;

namespace HorrorEngine
{
    public class PlayerStateAttackWithItem : PlayerStateAttack
    {
        [SerializeField] private WeaponData m_Item;
        [SerializeField] private EquipmentSlot m_Slot;
        [SerializeField] private bool m_CheckItemIsInInventory = true;


        private PlayerEquipment m_Equipment;
        private EquipableItemData m_PreviouslyEquippedItem;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            m_Equipment = GetComponentInParent<PlayerEquipment>();
        }

        // --------------------------------------------------------------------

        protected override void InitializeWeapon()
        {
            Debug.Assert(m_Item, "Item has not been set in PlayerStateAttackWithItem");

            // This affects just the visual side of things, the inventory is not actually equipping/unequipping anything

            m_Equipment.GetEquipped(m_Slot, out ItemData item, out GameObject go);
            m_PreviouslyEquippedItem = item as EquipableItemData;

            // Try to find the item in the inventory to apply changes on status
            m_WeaponInventoryEntry = GameManager.Instance.Inventory.GetEquipped(m_Item.Slot);
            if (m_WeaponInventoryEntry == null || m_WeaponInventoryEntry.Item != m_Item)
            {
                GameManager.Instance.Inventory.TryGet(m_Item, out m_WeaponInventoryEntry);
            }

            if(m_CheckItemIsInInventory) 
                Debug.Assert(m_WeaponInventoryEntry != null, $"Item {m_Item} couldn't be found anywhere in the inventory");

            m_WeaponInstance = m_Equipment.Equip(m_Item, m_Slot);
            m_Weapon = m_Item;

            m_Attack = m_WeaponInstance.GetComponent<IAttack>();
            Debug.Assert(m_Attack != null, "Equiped weapon has no IAttack component");            
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (m_PreviouslyEquippedItem)
                m_Equipment.Equip(m_PreviouslyEquippedItem, m_Slot);
            else
                m_Equipment.Unequip(m_Slot);

            base.StateExit(intoState);
        }
    }
}