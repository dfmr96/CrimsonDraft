using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Items/Overrides/Animator Override Controller")]
    public class EquipableAnimOverrideCtrlOverrideData : ItemOverrideData
    {
        public AnimatorOverrideController Overrider;
        public override void Override(ItemData item)
        {
            EquipableItemData equipable = item as EquipableItemData;
            Debug.Assert(equipable, $"Animator Override '{name}', applied to an item that's not a EquipableItemData");
            if (equipable)
            {
                equipable.AnimatorOverride.SetOverride(Overrider);
            }
        }
    }
}