#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ConsumableData", menuName = "CrimsonDraft/Inventory/Consumable Data")]
    public sealed class ConsumableData : ItemData
    {
        [SerializeField, Min(0)] private int healAmount;
        public int HealAmount => this.healAmount;
    }
}
