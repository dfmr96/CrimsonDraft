#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    /// <summary>
    /// Where <see cref="IInventoryService.TryAdd(ItemData, int)"/> would put an item right now,
    /// worked out without touching the inventory. A new item or stack lands at
    /// <see cref="Origin"/> of <see cref="Container"/>; when the quantity only tops up a stack
    /// that is already carried, <see cref="MergeTarget"/> is that stack and
    /// <see cref="Origin"/> is where it sits.
    /// </summary>
    public readonly struct AddPreview
    {
        public AddPreview(ContainerId container, Vector2Int origin, InventoryItem? mergeTarget = null)
        {
            this.Container   = container;
            this.Origin      = origin;
            this.MergeTarget = mergeTarget;
        }

        public ContainerId   Container   { get; }
        public Vector2Int    Origin      { get; }
        public InventoryItem? MergeTarget { get; }
    }
}
