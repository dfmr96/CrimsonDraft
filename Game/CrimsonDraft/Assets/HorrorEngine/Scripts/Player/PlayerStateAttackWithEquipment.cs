using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    public class PlayerStateAttackWithEquipment : PlayerStateAttack
    {
        private PlayerEquipment m_Equipment;

        protected override void Awake()
        {
            base.Awake();

            m_Equipment = GetComponentInParent<PlayerEquipment>();
        }

        protected override void InitializeWeapon()
        {
            Inventory inventory = GameManager.Instance.Inventory;
            m_WeaponInventoryEntry = inventory.GetEquippedWeapon();
            m_Weapon = m_WeaponInventoryEntry.Item as WeaponData;
            Debug.Assert(m_Weapon, $"Trying to attack without an equipped weapon");

            m_WeaponInstance = m_Equipment.GetWeaponInstance(EquipmentSlot.Primary);
        }       
    }
}