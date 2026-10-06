using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class PlayerEquipment : MonoBehaviour, IResetable
    {
        public struct EquipmentEntry
        {
            public GameObject Instance;
            public ItemData Data;
        }

        // --------------------------------------------------------------------

        private Dictionary<EquipmentSlot, EquipmentEntry> m_CurrentEquipment = new Dictionary<EquipmentSlot, EquipmentEntry>();
        private SocketController m_SocketController;
        private PlayerActor m_Actor;
        private AnimatorOverrider m_AnimOverrider;
        private AnimatorLayerBlendHandler m_AnimLayerBlendHandler;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Actor = GetComponentInParent<PlayerActor>();
            m_AnimOverrider = m_Actor.MainAnimator.GetComponent<AnimatorOverrider>();
            m_AnimLayerBlendHandler = m_Actor.MainAnimator.GetComponent<AnimatorLayerBlendHandler>();
            m_SocketController = GetComponentInChildren<SocketController>();
            MessageBuffer<EquippedItemChangedMessage>.Subscribe(OnEquippedItemChanged);
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            SetupCurrentEquipment();
        }

        // --------------------------------------------------------------------

        private void SetupCurrentEquipment()
        {
            Dictionary<EquipmentSlot, InventoryEntry> equipped = GameManager.Instance.Inventory.Equipped;
            foreach (var e in equipped)
            {
                var equipable = e.Value.Item as EquipableItemData;
                if (equipable.AttachOnEquipped)
                    Equip(equipable, equipable.Slot);
            }
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<EquippedItemChangedMessage>.Unsubscribe(OnEquippedItemChanged);
        }

        // --------------------------------------------------------------------

        private void OnEquippedItemChanged(EquippedItemChangedMessage msg)
        {
            if (msg.Character != m_Actor.Character)
                return;

            if (msg.InventoryEntry != null)
            {
                EquipableItemData equipable = msg.InventoryEntry.Item as EquipableItemData;
                if (equipable.AttachOnEquipped)
                    Equip(equipable, equipable.Slot);
            }
            else
            {
                Unequip(msg.Slot);
            }
        }

        // --------------------------------------------------------------------

        public GameObject Equip(EquipableItemData equipable, EquipmentSlot slot)
        {
            if (m_CurrentEquipment.ContainsKey(slot))
                Unequip(slot);

            var instance = GameObjectPool.Instance.GetFromPool(equipable.EquipPrefab);
            GameObject instanceGO = instance.gameObject;

            m_CurrentEquipment.Add(slot, new EquipmentEntry()
            {
                Instance = instanceGO,
                Data = equipable
            });

            
            m_SocketController.Attach(instanceGO, equipable.CharacterAttachment);

            instanceGO.SetActive(true);

            var animOverride = equipable.AnimatorOverride.Get();
            if (animOverride)
                m_AnimOverrider.AddOverride(animOverride);

            if (equipable.HasLayerOverrides)
            {
                foreach (var layerOverride in equipable.AnimatorLayerOverrides)
                {
                    m_AnimLayerBlendHandler.StartBlend(layerOverride.Layer, layerOverride.LayerWeight, layerOverride.BlendTime);
                }
            }

            return instanceGO;
        }

        // --------------------------------------------------------------------

        public void Unequip(EquipmentSlot type, bool destroy = true)
        {
            if (m_CurrentEquipment.TryGetValue(type, out EquipmentEntry entry))
            {
                if (destroy && Application.isPlaying)
                    Destroy(entry.Instance);

                EquipableItemData equipable = entry.Data as EquipableItemData;
                var animOverride = equipable.AnimatorOverride.Get();
                if (animOverride)
                    m_AnimOverrider.RemoveOverride(animOverride);

                if (equipable.HasLayerOverrides)
                {
                    foreach (var layerOverride in equipable.AnimatorLayerOverrides)
                    {
                        m_AnimLayerBlendHandler.RevertLayer(layerOverride.Layer, layerOverride.BlendTime);
                    }
                }

                m_CurrentEquipment.Remove(type);
            }
        }

        // --------------------------------------------------------------------

        public bool GetEquipped(EquipmentSlot type, out ItemData item, out GameObject instance)
        {
            item = null;
            instance = null;

            if (m_CurrentEquipment.TryGetValue(type, out EquipmentEntry entry))
            {
                item = entry.Data;
                instance = entry.Instance;
                return true;
            }

            return false;
        }

        // --------------------------------------------------------------------

        public GameObject GetWeaponInstance(EquipmentSlot type)
        {
            if (m_CurrentEquipment.TryGetValue(type, out EquipmentEntry entry))
            {
                if (entry.Data as WeaponData)
                    return entry.Instance;
            }
            return null;
        }

        // --------------------------------------------------------------------

        public void OnReset()
        {
            RemoveAllEquipment();
            SetupCurrentEquipment();
        }

        // --------------------------------------------------------------------

        void RemoveAllEquipment()
        {
            foreach (var e in m_CurrentEquipment)
            {
                Destroy(e.Value.Instance);
            }
            m_CurrentEquipment.Clear();
        }
    }
}
