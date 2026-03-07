#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "CrimsonDraft/Inventory/Item Data")]
    public sealed class ItemData : ScriptableObject
    {
        [SerializeField] private string   itemId      = string.Empty;
        [SerializeField] private ItemType itemType    = ItemType.Consumable;
        [SerializeField] private string   displayName = string.Empty;
        [SerializeField] private string   caliber     = string.Empty; // empty if not applicable

        public string   ItemId      => this.itemId;
        public ItemType ItemType    => this.itemType;
        public string   DisplayName => this.displayName;
        public string   Caliber     => this.caliber;
    }
}
