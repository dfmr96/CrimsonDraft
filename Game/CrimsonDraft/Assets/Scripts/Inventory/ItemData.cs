#nullable enable

using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Inventory
{
    // No [CreateAssetMenu] on base -- use WeaponData, AmmoBoxData or ConsumableData.
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string            itemId          = string.Empty;
        [SerializeField] private ItemType          itemType        = ItemType.Consumable;
        [SerializeField] private string            displayName     = string.Empty;
        [SerializeField] private bool              stackable       = false;
        [SerializeField] private Sprite            icon            = null!;
        [SerializeField] private DialogueReference examineDialogue = new();

        [SerializeField] private string     secondaryName = string.Empty;
        [SerializeField] private Vector2Int gridSize      = Vector2Int.one;
        [SerializeField] private bool       combinable    = false;
        [SerializeField] private int        maxStack      = 999;

        public string            ItemId          => this.itemId;
        public ItemType          ItemType        => this.itemType;
        public string            DisplayName     => this.displayName;
        public string            SecondaryName   => this.secondaryName;
        public Vector2Int        GridSize        => this.gridSize;
        public bool              Combinable      => this.combinable;
        public virtual bool      Stackable       => this.stackable;
        public int               MaxStack        => this.maxStack;
        public Sprite?           Icon            => this.icon;
        public DialogueReference ExamineDialogue => this.examineDialogue;
    }
}
