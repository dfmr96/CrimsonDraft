#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "KeyItemData", menuName = "CrimsonDraft/Inventory/Key Item Data")]
    public sealed class KeyItemData : ItemData
    {
        [SerializeField] [Min(1)] private int maxUses = 1;

        public int MaxUses => this.maxUses;
    }
}
