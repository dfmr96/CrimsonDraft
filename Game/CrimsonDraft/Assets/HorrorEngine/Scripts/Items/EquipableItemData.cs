using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    [Serializable]
    public class AnimatorLayerOverride
    {
        public AnimatorLayerHandle Layer;
        public float LayerWeight;
        public float BlendTime = 0.25f;
    }

    [CreateAssetMenu(menuName = "Horror Engine/Items/Equipable")]
    public class EquipableItemData : ItemData
    {
        [FormerlySerializedAs("Prefab")]
        public GameObject EquipPrefab;
        public EquipmentSlot Slot = EquipmentSlot.Primary;
        public SocketAttachment CharacterAttachment;
        [Tooltip("If true, this item will be attached to the character when used from the inventory selecting the Equip option. In some cases, items might not need to be attached until an action happens")]
        public bool AttachOnEquipped = true;
        [Tooltip("If true, this item will be removed from the current inventory slot when equipped")]
        public bool MoveOutOfInventoryOnEquip;
        [Header("Animation")]
        public RuntimeOverridableReference<AnimatorOverrideController> AnimatorOverride;
        public List<AnimatorLayerOverride> AnimatorLayerOverrides;
        [SerializeField] public AudioClip m_OnEquipAudioClip;

        public bool HasLayerOverrides => AnimatorLayerOverrides != null && AnimatorLayerOverrides.Count > 0;

        public void OnEquip(InventoryEntry entry)
        {
            if (m_OnEquipAudioClip)
                UIManager.Get<UIAudio>().Play(m_OnEquipAudioClip);
        }


    }
}