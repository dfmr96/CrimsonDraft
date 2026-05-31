using UnityEngine;

namespace CrimsonDraft.UI
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "CrimsonDraft/Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string primaryName;
        [Tooltip("Shown when the item is inspected up close.")]
        public string secondaryName;

        [Header("Visual")]
        public Sprite icon;

        [Header("Grid")]
        [Tooltip("How many cells this item occupies. X = columns, Y = rows.")]
        public Vector2Int gridSize = Vector2Int.one;

        [Header("Info")]
        [TextArea(2, 5)]
        public string description;
        public bool combinable;
    }
}
