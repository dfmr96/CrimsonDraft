#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    // No [CreateAssetMenu] on base -- use WeaponData, AmmoBoxData or ConsumableData.
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string   itemId      = string.Empty;
        [SerializeField] private ItemType itemType    = ItemType.Consumable;
        [SerializeField] private string   displayName = string.Empty;
        [SerializeField] private bool     stackable   = false;
        [SerializeField] private Sprite   icon        = null!;

        public string   ItemId      => this.itemId;
        public ItemType ItemType    => this.itemType;
        public string   DisplayName => this.displayName;
        public virtual bool Stackable => this.stackable;
        public Sprite?  Icon        => this.icon;
    }
}
